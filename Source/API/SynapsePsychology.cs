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
    public static class SynapsePsychology
    {
        /// <summary>
        /// Public API for other mods (primarily Storyteller) to add a memory
        /// to a pawn's SynapsePawnComp.
        /// </summary>
        public static string GenerateContextSummary(Pawn pawn, List<RimSynapse.Models.WeightedMemory> customMemories = null)
        {
            var comp = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (comp == null)
            {
                Log.Warning($"[RimSynapse-Psychology] Cannot add memory — SynapseCorePawnComp not found on {pawn.Name}.");
                return "";
            }

            var memoriesToProcess = customMemories ?? comp.memories;
            return JsonConvert.SerializeObject(memoriesToProcess);
        }

        public static void AddMemory(Pawn pawn, WeightedMemory memory)
        {
            var comp = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (comp == null)
            {
                Log.Warning($"[RimSynapse-Psychology] Cannot add memory — SynapseCorePawnComp not found on {pawn.Name}.");
                return;
            }

            comp.memories.Add(memory);
            Log.Message($"[RimSynapse-Psychology] Memory added to {pawn.Name}: \"{memory.summary}\" (type: {memory.memoryType}, weight: {memory.weight})");
        }

        /// <summary>
        /// Bump a memory's weight when the LLM references it.
        /// </summary>
        public static void BumpMemory(Pawn pawn, string memorySummaryFragment, float bumpAmount = 0.2f)
        {
            var comp = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (comp == null) return;

            var match = comp.memories.FirstOrDefault(m =>
                m.summary.Contains(memorySummaryFragment));

            if (match != null)
            {
                match.weight = Math.Min(1.0f, match.weight + bumpAmount);
                match.timesReferenced++;
            }
        }

        /// <summary>
        /// Get the opinion integral (moving average) for a pawn→target relationship.
        /// Returns null if Psychology mod is not loaded or no samples exist.
        /// </summary>
        public static float? GetOpinionIntegral(Pawn pawn, Pawn target)
        {
            var comp = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (comp == null) return null;

            var samples = comp.opinionHistory
                .Where(s => s.targetPawnId == target.ThingID)
                .ToList();

            if (samples.Count == 0) return null;

            return (float)samples.Average(s => s.opinion);
        }

        /// <summary>
        /// Get the relationship trajectory (current opinion minus integral).
        /// Positive = improving, negative = deteriorating.
        /// </summary>
        public static float? GetRelationshipTrajectory(Pawn pawn, Pawn target)
        {
            float? integral = GetOpinionIntegral(pawn, target);
            if (integral == null) return null;

            if (pawn.relations == null) return null;
            
            int currentOpinion = pawn.relations.OpinionOf(target);
            return currentOpinion - integral.Value;
        }

        /// <summary>
        /// Checks if a pawn needs a backstory memory generated.
        /// Used by Storyteller to safely queue backstory creation when processing time is available.
        /// </summary>
        public static bool NeedsBackstory(Pawn pawn)
        {
            var comp = pawn.TryGetComp<SynapsePawnComp>();
            if (comp == null) return false;
            
            return !comp.hasBackstoryMemory;
        }

        /// <summary>
        /// Marks the backstory as generated for a pawn.
        /// </summary>
        public static void MarkBackstoryCreated(Pawn pawn)
        {
            var comp = pawn.TryGetComp<SynapsePawnComp>();
            if (comp != null)
            {
                comp.hasBackstoryMemory = true;
            }
        }

        /// <summary>
        /// Update the thought sensitivities based on AI evaluation.
        /// </summary>
        public static void UpdateSensitivities(Pawn pawn, System.Collections.Generic.Dictionary<string, float> newSensitivities)
        {
            var comp = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (comp != null)
            {
                comp.thoughtSensitivities = newSensitivities;
            }
        }

        /// <summary>
        /// Update the pawn's mental break category and zealotry.
        /// </summary>
        public static void UpdateBreakProfile(Pawn pawn, BreakCategory category, bool isZealot)
        {
            var comp = pawn.TryGetComp<SynapsePawnComp>();
            if (comp != null)
            {
                comp.breakCategory = category;
                comp.ideologyZealot = isZealot;
            }
        }

        /// <summary>
        /// Applies a trait directive from the AI, adding or removing a trait dynamically.
        /// Sends a letter to the player explaining the psychological reasoning.
        /// </summary>
        public static void ApplyTraitDirective(Pawn pawn, string traitDefName, bool add, string aiReasoning)
        {
            if (pawn.story == null || pawn.story.traits == null) return;

            TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(traitDefName);
            if (traitDef == null)
            {
                Log.Warning($"[RimSynapse-Psychology] Trait {traitDefName} not found.");
                return;
            }

            bool changed = false;

            if (add && !pawn.story.traits.HasTrait(traitDef))
            {
                pawn.story.traits.GainTrait(new Trait(traitDef));
                changed = true;
            }
            else if (!add && pawn.story.traits.HasTrait(traitDef))
            {
                var trait = pawn.story.traits.GetTrait(traitDef);
                pawn.story.traits.allTraits.Remove(trait);
                changed = true;
            }

            if (changed)
            {
                string title = $"Personality Shift: {pawn.Name.ToStringShort}";
                if (Prefs.DevMode)
                {
                    title = "[RimSynapse Psychology] " + title;
                }

                string actionStr = add ? $"gained the trait '{traitDef.degreeDatas[0].label}'" : $"lost the trait '{traitDef.degreeDatas[0].label}'";
                string letterText = $"{pawn.Name.ToStringShort} has {actionStr}.\n\nReasoning:\n{aiReasoning}";

                Find.LetterStack.ReceiveLetter(title, letterText, LetterDefOf.NeutralEvent, pawn);
            }
        }

        /// <summary>
        /// Called by the LLM Bridge when it has determined what specific mental break
        /// a pawn will suffer if they snap. This displays a warning threat to the player.
        /// </summary>
        public static void PredictMentalBreak(Pawn pawn, string breakDefName, string warningText)
        {
            var comp = pawn.TryGetComp<SynapsePawnComp>();
            if (comp == null) return;

            MentalStateDef breakDef = DefDatabase<MentalStateDef>.GetNamedSilentFail(breakDefName);
            if (breakDef == null)
            {
                Log.Warning($"[RimSynapse-Psychology] Predicted break {breakDefName} not found.");
                return;
            }

            comp.predictedBreakState = breakDef;
            comp.currentBreakWarning = warningText;

            string title = $"Break Risk: {pawn.Name.ToStringShort}";
            Find.LetterStack.ReceiveLetter(title, warningText, LetterDefOf.ThreatSmall, pawn);
        }

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

        /// <summary>
        /// Triggered by the Core framework when the LLM queue is idle.
        /// Generates a low-priority background memory for 1-3 colonists based on the oldest PastEvent.
        /// </summary>
        public static void TriggerOpportunisticMemory()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;

            var coreComp = Find.World.GetComponent<SynapseCoreWorldComponent>();
            if (coreComp == null || !coreComp.TryDequeuePastEvent(out PastEvent pastEvent)) return;

            // Pick up to 3 colonists to generate memories for
            var pawns = Find.CurrentMap.mapPawns.FreeColonists.OrderBy(_ => Rand.Value).Take(3).ToList();
            if (pawns.Count == 0) return;

            string systemPrompt = @"You are the internal monologues of the specified RimWorld colonists.
A significant event just occurred. Write a very short (1-2 sentences) personal memory/journal entry for EACH colonist about this event.
You must also provide a list of 1-3 single-word tags (keywords) for each memory (e.g. 'Food', 'Combat', 'Resentment', 'Safety').
And assign a memory weight (0.1 to 5.0) and decay rate (0.01 to 0.5) based on how traumatic/important it was.

CRITICAL INSTRUCTION FOR DEFINING MEMORIES: 
Desensitization matters. Look at their lifetime statistics. If they have 25 kills, another kill isn't traumatic. If it's their first, it is life-altering.
If this event is life-altering (e.g., first kill, death of a close friend, marriage, creating a legendary artifact), you MUST set the `Decay` value strictly to `0.0` so it never fades. Defining memories do not decay.

You MUST respond strictly in valid JSON format:
{
  ""Memories"": [
    {
      ""PawnId"": ""ThingID_Here"",
      ""Summary"": ""I was starving, and we just gave our food to beggars..."",
      ""Tags"": [""Food"", ""Resentment""],
      ""Weight"": 1.5,
      ""Decay"": 0.05
    }
  ]
}";

            string userMessage = $"Event: {pastEvent.eventDescription}\nColony Status at the time: {pastEvent.colonySnapshot}\n\nTarget Pawns:\n";
            foreach (var pawn in pawns)
            {
                var pCoreComp = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
                float threshold = RimSynapsePsychologyMod.Settings != null ? RimSynapsePsychologyMod.Settings.sensitivityThreshold : 0.5f;
                string lifetimeBurdens = pCoreComp != null ? pCoreComp.GetTopMemoryBurdens(3, threshold) : "None";
                
                int lifetimeKills = pawn.records?.GetAsInt(RecordDefOf.KillsHumanlikes) ?? 0;
                
                string snapshot = pastEvent.pawnSnapshots != null && pastEvent.pawnSnapshots.ContainsKey(pawn.ThingID) 
                    ? pastEvent.pawnSnapshots[pawn.ThingID] 
                    : "Unknown";
                    
                userMessage += $"- Name: {pawn.Name.ToStringShort}, ID: {pawn.ThingID}\n  Status at the time: {snapshot}\n  Lifetime Kills: {lifetimeKills}\n  Current Psychological Burdens: {lifetimeBurdens}\n\n";
            }

            // Use priority -1 so it stays at the absolute bottom of the queue and yields to real events
            var options = new ChatOptions { priority = -1 };

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
                            string json = result.content.Trim();
                            if (json.StartsWith("```json")) json = json.Substring(7);
                            if (json.StartsWith("```")) json = json.Substring(3);
                            if (json.EndsWith("```")) json = json.Substring(0, json.Length - 3);
                            json = json.Trim();

                            var parsed = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, object>>>>(json);
                            if (parsed != null && parsed.ContainsKey("Memories"))
                            {
                                foreach (var memDict in parsed["Memories"])
                                {
                                    string pawnId = memDict["PawnId"].ToString();
                                    Pawn targetPawn = pawns.FirstOrDefault(p => p.ThingID == pawnId);
                                    if (targetPawn != null)
                                    {
                                        var tagsList = new List<string>();
                                        if (memDict.ContainsKey("Tags") && memDict["Tags"] is Newtonsoft.Json.Linq.JArray arr)
                                        {
                                            tagsList = arr.Select(t => t.ToString()).ToList();
                                        }

                                        AddMemory(targetPawn, new WeightedMemory
                                        {
                                            summary = memDict["Summary"].ToString(),
                                            weight = Convert.ToSingle(memDict["Weight"]),
                                            baseWeight = Convert.ToSingle(memDict["Weight"]),
                                            decayRate = Convert.ToSingle(memDict["Decay"]) * RimSynapsePsychologyMod.Settings.memoryDecayMultiplier,
                                            tags = tagsList,
                                            memoryType = "EventReflection",
                                            gameTick = pastEvent.gameTick
                                        });
                                    }
                                }
                                Log.Message($"[RimSynapse-Psychology] Opportunistic memories generated for {pastEvent.eventDescription}.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"[RimSynapse-Psychology] Failed to parse JSON memories: {ex.Message}\nContent: {result.content}");
                        }
                    }
                },
                options
            );
        }

        /// <summary>
        /// Triggered by the Core framework when the LLM queue is idle.
        /// Generates a low-priority background backstory for an important non-colonist (visitor/raider/prisoner).
        /// </summary>
        public static void TriggerOpportunisticVisitorBackstory()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;

            // Find an important non-colonist who doesn't have a backstory yet
            var targetPawn = Find.CurrentMap.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Humanlike && !p.IsColonist && IsImportantPawn(p) && NeedsBackstory(p))
                .RandomElementWithFallback();

            if (targetPawn == null) return;

            var coreComp = targetPawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (coreComp == null) return;

            string factionName = targetPawn.Faction?.Name ?? "Outlander";
            string title = targetPawn.royalty?.MostSeniorTitle?.def?.label ?? "Wanderer";

            string systemPrompt = $@"You are {targetPawn.Name.ToStringShort}, a {title} from {factionName} in the RimWorld universe.
Write a very short (2-3 sentences) autobiographical backstory about your past. Write in the first person ('I', 'me').";

            string userMessage = $"Generate my backstory. I am currently at {Find.CurrentMap.Parent.Label}.";

            // Use priority -1 so it stays at the bottom of the queue
            var options = new ChatOptions { priority = -1 };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => 
                {
                    if (result.success)
                    {
                        AddMemory(targetPawn, new WeightedMemory
                        {
                            summary = result.content.Trim(),
                            weight = 0.8f,
                            memoryType = "Backstory"
                        });
                        MarkBackstoryCreated(targetPawn);
                        Log.Message($"[RimSynapse-Psychology] Opportunistic visitor backstory generated for {targetPawn.Name.ToStringShort} (Important Pawn).");
                    }
                },
                options
            );
        }

        private static bool IsImportantPawn(Pawn p)
        {
            if (p.Faction != null && p.Faction.leader == p) return true;
            if (p.IsPrisonerOfColony) return true;
            if (p.royalty != null && p.royalty.AllTitlesForReading.Any()) return true;
            if (p.relations != null && p.relations.FamilyByBlood.Any(r => r.Faction == Faction.OfPlayer || r.IsPrisonerOfColony)) return true;
            return false;
        }
    }
}
