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
        public static void QueueDailyPsychologyReview(Pawn pawn, float averageMood, System.Collections.Generic.List<RimSynapse.Models.WeightedMemory> dailyEvents, Action<bool> onComplete = null)
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
  ""AbandonmentRiskScore"": 0
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

            string userMessage = $@"Patient Name: {pawn.Name.ToStringShort}
Status: {statusText}
Colony Size: {colonySize}
Time as Colonist: {timeAsColonist:F1} days
Survival Stats: Melee {melee}, Shooting {shooting}, Medicine {medicine}, Lifetime Human Kills: {lifetimeKills}, Damage Taken: {damageTaken:F0}
Average Mood Today: {averageMood:F2}{suppression}
Psychological Burdens (Sensitivity): {lifetimeBurdens}
Recent Memories:
{recentEvents}";

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
                                    if (kvp.Key == "AbandonmentRiskScore")
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
                                    else
                                    {
                                        pawnComp.medicalProfile[kvp.Key] = kvp.Value.ToString();
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"[RimSynapse-Psychology] Failed to parse JSON clinical assessment for {pawn.Name.ToStringShort}: {ex.Message}\nContent: {result.content}");
                        }
                        
                        onComplete?.Invoke(true);
                    }
                    else
                    {
                        Log.Warning($"[RimSynapse-Psychology] Failed to generate clinical assessment for {pawn.Name.ToStringShort}: {result.error}");
                        onComplete?.Invoke(false);
                    }
                    sw.Stop();
                    RimSynapse.Utils.SynapseFileLogger.LogMetric("Psychology", pawn, "QueueDailyPsychologyReview", sw.ElapsedMilliseconds);
                }
            );
        }
    }
}
