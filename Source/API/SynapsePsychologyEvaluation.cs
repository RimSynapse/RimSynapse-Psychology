using System;
using System.Linq;
using System.Collections.Generic;
using Verse;
using RimWorld;
using RimSynapse.Psychology.Comps;
using RimSynapse.Models;
using Newtonsoft.Json;

namespace RimSynapse.Psychology.API
{
    /// <summary>
    /// Nightly clinical evaluation: Queues the pawn's daily mood, memories, and statistics
    /// for LLM processing to update their long-term psychological profile.
    /// </summary>
    public static partial class SynapsePsychology
    {
        /// <summary>
        /// Triggered when the pawn goes to sleep. Queues their daily events and average mood
        /// for LLM processing to update long-term context modifiers and break severity.
        /// </summary>
        public static void QueueDailyPsychologyReview(Pawn pawn, float averageMood, System.Collections.Generic.List<RimSynapse.Models.WeightedMemory> dailyEvents, Action<bool> onComplete = null, bool isOpportunistic = false)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var pawnComp = pawn.TryGetComp<SynapsePawnComp>();
            var coreComp = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (pawnComp == null || coreComp == null) return;

            string systemPrompt = @"You are a clinical psychologist in the RimWorld universe writing a formal medical evaluation.
Based on this colonist's average mood today, their recent memories, their survival skills, and the state of the colony, assess the following 8 categories (write 1-2 sentences each):
- Relationships (How they feel about others)
- Trauma (Recent pain or historical suffering)
- ShapingEvents (Major life events, e.g., wedding, birth, deaths)
- Disorders (Psychological conditions or 'None')
- Satisfaction (General contentment with their life)
- Fulfillment (Whether their work aligns with their passions)
- Arrogance (Ego related to their titles or skills)
- Dedication (Are they discontent? Likely to rebel or leave?)

You MUST analyze the 'Tags' attached to their recent memories. If you see recurring themes (e.g. 'Food', 'Starving', 'Safety'), you must explicitly address this growing concern in their Satisfaction or Dedication assessment.

You must also provide an 'AbandonmentRiskScore' (0-100) representing how likely they are to permanently abandon the colony (or rebel if a slave). High survival skills and low satisfaction increase this risk.

Finally, output a 'SocialAdjustments' object reflecting changes in their Trust (-100 to +100) and Familiarity (0 to 100) with other colonists based on recent events. Use the colonist's short name as the key. Trust offsets should be between -15 and +15. Familiarity offsets should be positive (0 to +10).

Additionally, evaluate if their recent experiences are profound enough to change their personality traits. If so, return a 'TraitChanges' object with an 'Add' array (containing RimWorld trait defNames to add, e.g. 'Bloodlust', 'Nerves') and/or a 'Remove' array (trait defNames to remove). Keep this rare; leave arrays empty if no profound change occurred.
The colonist currently has the following dynamically added traits (which you previously added): {DYNAMIC_TRAITS}. If you determine the pawn has moved past the psychological phase that caused these traits, you should include them in the 'Remove' array so they can decay.

You MUST respond strictly in valid JSON format. Do not include markdown formatting or extra text.
{
  ""Relationships"": ""1-2 sentences..."",
  ""Trauma"": ""1-2 sentences..."",
  ""ShapingEvents"": ""1-2 sentences..."",
  ""Disorders"": ""1-2 sentences..."",
  ""Satisfaction"": ""1-2 sentences..."",
  ""Fulfillment"": ""1-2 sentences..."",
  ""Arrogance"": ""1-2 sentences..."",
  ""Dedication"": ""1-2 sentences..."",
  ""AbandonmentRiskScore"": 0,
  ""TraitChanges"": {
    ""Add"": [""Bloodlust""],
    ""Remove"": [""Kind""]
  },
  ""SocialAdjustments"": {
    ""ColonistName1"": {
      ""trustOffset"": 2.5,
      ""familiarityOffset"": 1.0
    }
  }
}";

            string recentEvents = dailyEvents == null || dailyEvents.Count == 0 
                ? "No significant memories today." 
                : string.Join("\n", dailyEvents.Select(e => $"- {e.summary} [Tags: {string.Join(", ", e.tags)}]"));

            float threshold = RimSynapsePsychologyMod.Settings != null ? RimSynapsePsychologyMod.Settings.sensitivityThreshold : 0.5f;
            string lifetimeBurdens = coreComp.GetTopMemoryBurdens(5, threshold);

            int colonySize = pawn.Map?.mapPawns?.FreeColonistsCount ?? 1;
            int melee = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            int shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
            int cooking = pawn.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? 0;
            int medicine = pawn.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
            
            int lifetimeKills = pawn.records?.GetAsInt(RecordDefOf.KillsHumanlikes) ?? 0;
            float damageTaken = pawn.records?.GetValue(RecordDefOf.DamageTaken) ?? 0f;
            float timeAsColonist = (pawn.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) / 60000f; // in days

            string statusText = pawn.IsColonist ? "Colonist" : (pawn.IsPrisoner ? "Prisoner" : (pawn.IsSlave ? "Slave" : "Guest"));
            NeedDef suppressionDef = DefDatabase<NeedDef>.GetNamedSilentFail("Suppression");
            string suppression = (pawn.IsSlave && suppressionDef != null) ? $"\nSuppression: {pawn.needs?.TryGetNeed(suppressionDef)?.CurLevelPercentage:P0}" : "";

            // Initialize missing colonists in social network
            if (pawn.Map != null)
            {
                foreach (Pawn p in pawn.Map.mapPawns.FreeColonists)
                {
                    if (p != pawn && !pawnComp.socialNetwork.ContainsKey(p.GetUniqueLoadID()))
                    {
                        pawnComp.socialNetwork[p.GetUniqueLoadID()] = new RimSynapse.Psychology.Models.SocialRecord();
                    }
                }
            }

            string socialNetworkStr = "None";
            if (pawnComp.socialNetwork.Count > 0)
            {
                var allPawns = (pawn.Map?.mapPawns?.AllPawnsSpawned ?? Enumerable.Empty<Pawn>()).Concat(Find.WorldPawns.AllPawnsAliveOrDead);
                var socialLines = new List<string>();
                foreach (var kvp in pawnComp.socialNetwork)
                {
                    Pawn target = allPawns.FirstOrDefault(p => p.GetUniqueLoadID() == kvp.Key);
                    if (target != null)
                    {
                        int affinity = target.relations != null ? pawn.relations.OpinionOf(target) : 0;
                        socialLines.Add($"- {target.Name.ToStringShort} ({target.gender}): Trust {kvp.Value.trust:F0}, Familiarity {kvp.Value.familiarity:F0}, Affinity {affinity}");
                    }
                }
                if (socialLines.Count > 0)
                {
                    socialNetworkStr = string.Join("\n", socialLines);
                }
            }

            string dynamicTraitsStr = "None";
            if (pawnComp.dynamicTraits.Count > 0)
            {
                dynamicTraitsStr = string.Join(", ", pawnComp.dynamicTraits.Select(t => $"{t.traitDef.defName} (Added {((Find.TickManager.TicksGame - t.tickAdded)/60000f):F1} days ago for: {t.reason})"));
            }
            systemPrompt = systemPrompt.Replace("{DYNAMIC_TRAITS}", dynamicTraitsStr);

            string userMessage = $@"Patient Name: {pawn.Name.ToStringShort}
Gender: {pawn.gender}
Status: {statusText}
Colony Size: {colonySize}
Time as Colonist: {timeAsColonist:F1} days
Survival Stats: Melee {melee}, Shooting {shooting}, Medicine {medicine}, Lifetime Human Kills: {lifetimeKills}, Damage Taken: {damageTaken:F0}
Average Mood Today: {averageMood:F2}{suppression}
Psychological Burdens (Sensitivity): {lifetimeBurdens}
Current Social Network (Trust, Familiarity, Affinity):
{socialNetworkStr}
Recent Memories:
{recentEvents}";

            var options = new ChatOptions { priority = isOpportunistic ? -1 : 0, requestName = "Daily Psychology Review", targetName = pawn.Name.ToStringShort };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => 
                {
                    if (result.success)
                    {
                        try
                        {
                            // Strip markdown if the LLM wraps it in ```json
                            string json = result.content.Trim();
                            if (json.StartsWith("```json")) json = json.Substring(7);
                            if (json.StartsWith("```")) json = json.Substring(3);
                            if (json.EndsWith("```")) json = json.Substring(0, json.Length - 3);
                            json = json.Trim();

                            var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                            if (parsed != null)
                            {
                                foreach (var kvp in parsed)
                                {
                                    if (kvp.Key == "SocialAdjustments" && kvp.Value is Newtonsoft.Json.Linq.JObject socialObj)
                                    {
                                        var allPawns = (pawn.Map?.mapPawns?.AllPawnsSpawned ?? Enumerable.Empty<Pawn>()).Concat(Find.WorldPawns.AllPawnsAliveOrDead);
                                        foreach (var property in socialObj.Properties())
                                        {
                                            string targetName = property.Name;
                                            var offsets = property.Value as Newtonsoft.Json.Linq.JObject;
                                            if (offsets != null)
                                            {
                                                Pawn targetPawn = allPawns.FirstOrDefault(p => p.Name != null && p.Name.ToStringShort.Equals(targetName, StringComparison.OrdinalIgnoreCase));
                                                if (targetPawn != null)
                                                {
                                                    string loadId = targetPawn.GetUniqueLoadID();
                                                    if (!pawnComp.socialNetwork.ContainsKey(loadId))
                                                    {
                                                        pawnComp.socialNetwork[loadId] = new RimSynapse.Psychology.Models.SocialRecord();
                                                    }
                                                    
                                                    float trustOff = (float?)offsets["trustOffset"] ?? 0f;
                                                    float famOff = (float?)offsets["familiarityOffset"] ?? 0f;
                                                    
                                                    pawnComp.socialNetwork[loadId].trust = UnityEngine.Mathf.Clamp(pawnComp.socialNetwork[loadId].trust + trustOff, -100f, 100f);
                                                    pawnComp.socialNetwork[loadId].familiarity = UnityEngine.Mathf.Clamp(pawnComp.socialNetwork[loadId].familiarity + famOff, 0f, 100f);
                                                }
                                            }
                                        }
                                    }
                                    else if (kvp.Key == "AbandonmentRiskScore")
                                    {
                                        int riskScore = Convert.ToInt32(kvp.Value);
                                        // Trigger abandonment state if risk is extremely high and they are a free colonist
                                        if (riskScore > 90 && pawn.IsColonist && !pawn.Downed && !pawn.InMentalState)
                                        {
                                            SynapseGameComponent.Enqueue(() =>
                                            {
                                                var stateDef = DefDatabase<MentalStateDef>.GetNamedSilentFail("Synapse_MentalState_AbandonColony");
                                                if (stateDef != null)
                                                {
                                                    pawn.mindState.mentalStateHandler.TryStartMentalState(stateDef, "Psychological evaluation", true);
                                                }
                                            });
                                        }
                                    }
                                    else if (kvp.Key == "TraitChanges" && kvp.Value is Newtonsoft.Json.Linq.JObject traitObj)
                                    {
                                        SynapseGameComponent.Enqueue(() =>
                                        {
                                            if (pawn.story == null || pawn.story.traits == null) return;
                                            
                                            var addArr = traitObj["Add"] as Newtonsoft.Json.Linq.JArray;
                                            var removeArr = traitObj["Remove"] as Newtonsoft.Json.Linq.JArray;
                                            
                                            if (removeArr != null)
                                            {
                                                foreach (var traitName in removeArr)
                                                {
                                                    string tName = traitName.ToString();
                                                    SynapsePsychology.ApplyTraitDirective(pawn, tName, false, "The psychological evaluation determined this trait has decayed or is no longer relevant.");
                                                }
                                            }
                                            
                                            if (addArr != null)
                                            {
                                                foreach (var traitName in addArr)
                                                {
                                                    string tName = traitName.ToString();
                                                    SynapsePsychology.ApplyTraitDirective(pawn, tName, true, "The psychological evaluation determined a profound personality shift.");
                                                }
                                            }
                                        });
                                    }
                                    else
                                    {
                                        pawnComp.medicalProfile[kvp.Key] = kvp.Value.ToString();
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            RimSynapse.SynapseLogger.Warn("psychology", $"[RimSynapse-Psychology] Failed to parse JSON clinical assessment for {pawn.Name.ToStringShort}: {ex.Message}\nContent: {result.content}");
                        }
                        
                        onComplete?.Invoke(true);
                    }
                    else
                    {
                        RimSynapse.SynapseLogger.Warn("psychology", $"[RimSynapse-Psychology] Failed to generate clinical assessment for {pawn.Name.ToStringShort}: {result.error}");
                        onComplete?.Invoke(false);
                    }
                    sw.Stop();
//
                },
                options
            );
        }
        public static void SummarizeTherapySession(Pawn initiator, Pawn target, List<string> chatLog)
        {
            if (chatLog == null || chatLog.Count == 0) return;

            string fullTranscript = string.Join("\n", chatLog);
            string systemPrompt = @"You are a clinical psychologist summarizing a therapy session between two colonists.
Analyze the transcript and extract the key psychological insights, breakthroughs, or recurring themes.
Return a brief, profound 2-3 sentence summary that will be stored permanently as context for future interactions between these two pawns.
Do not include markdown or formatting, just the plain text summary.";

            string userMessage = $@"Initiator (Counselor): {initiator.NameShortColored}
Target (Patient): {target.NameShortColored}

Transcript:
{fullTranscript}";

            var options = new ChatOptions { priority = -1, requestName = "Therapy Summary", targetName = $"{initiator.NameShortColored} -> {target.NameShortColored}" };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => 
                {
                    if (result.success)
                    {
                        string summary = result.content.Trim();
                        // Store the summary in the core memory network or pawn comp for future context
                        // TriggerOpportunisticMemoryGeneration(target, $"Had a breakthrough in therapy with {initiator.LabelShort}: {summary}", "Therapy, Insight", null);
                        // TriggerOpportunisticMemoryGeneration(initiator, $"Provided therapy for {target.LabelShort}. Key takeaway: {summary}", "Therapy, Insight", null);
                    }
                    else
                    {
                        RimSynapse.SynapseLogger.Warn("psychology", $"[RimSynapse-Psychology] Failed to summarize therapy session: {result.error}");
                    }
                },
                options
            );
        }
    }
}





