using System;
using System.Linq;
using System.Collections.Generic;
using Verse;
using RimWorld;
using RimSynapse.Psychology.Comps;
using RimSynapse.Models;
using RimSynapse.Utils;
using Newtonsoft.Json;

namespace RimSynapse.Psychology.API
{
    /// <summary>
    /// Opportunistic background tasks: Memory generation from PastEvents,
    /// visitor backstory generation, and importance filtering.
    /// </summary>
    public static partial class SynapsePsychology
    {
        /// <summary>
        /// Triggered by the Core framework when the LLM queue is idle.
        /// Generates a low-priority background memory for 1-3 colonists based on the oldest PastEvent.
        /// </summary>
        public static bool TriggerOpportunisticMemory()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return false;

            var coreComp = Find.World.GetComponent<SynapseCoreWorldComponent>();
            if (coreComp == null || !coreComp.TryDequeuePastEvent(out PastEvent pastEvent)) return false;

            // Pick up to 3 colonists to generate memories for
            var pawns = Find.CurrentMap.mapPawns.FreeColonists.OrderBy(_ => Rand.Value).Take(3).ToList();
            if (pawns.Count == 0) return false;

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
                            string json = JsonHelper.ExtractJson(result.content);
                            if (json == null) { Log.Warning("[RimSynapse-Psychology] No JSON found in memory response."); return; }

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
            return true;
        }

        /// <summary>
        /// Triggered by the Core framework when the LLM queue is idle.
        /// Generates a low-priority background backstory for an important non-colonist.
        /// </summary>
        public static bool TriggerOpportunisticVisitorBackstory()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return false;

            var targetPawn = Find.CurrentMap.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Humanlike && !p.IsColonist && IsImportantPawn(p) && NeedsBackstory(p))
                .RandomElementWithFallback();

            if (targetPawn == null) return false;

            var coreComp = targetPawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (coreComp == null) return false;

            bool isLeader = targetPawn.Faction != null && targetPawn.Faction.leader == targetPawn;
            string factionName = targetPawn.Faction?.Name ?? "Outlander";
            string factionType = targetPawn.Faction?.def?.LabelCap ?? "Faction";
            string title = targetPawn.royalty?.MostSeniorTitle?.def?.label ?? (isLeader ? "Leader" : "Wanderer");

            string systemPrompt;
            if (isLeader)
            {
                systemPrompt = $@"You are {targetPawn.Name.ToStringShort}, the {title} of {factionName} (a {factionType}) in the RimWorld universe.
You must generate a detailed life history consisting of exactly 3 or 4 significant life events.
These events MUST include:
1. 'Faction Join' (or birth into the faction).
2. 'Faction Rise' (how you gained influence).
3. 'Faction Control' (how you took leadership).
(Optional) 4. A flavor memory that adds personality.

You MUST respond strictly in valid JSON format:
{{
  ""Memories"": [
    {{
      ""Summary"": ""I was born into the harsh wastes and immediately inducted into the tribe..."",
      ""Tags"": [""Faction Join"", ""Origin""],
      ""Weight"": 2.0
    }}
  ]
}}";
            }
            else
            {
                systemPrompt = $@"You are {targetPawn.Name.ToStringShort}, a {title} from {factionName} in the RimWorld universe.
Write a very short (2-3 sentences) autobiographical backstory about your past. Write in the first person ('I', 'me').

You MUST respond strictly in valid JSON format:
{{
  ""Memories"": [
    {{
      ""Summary"": ""I grew up in a glitterworld before crashing here..."",
      ""Tags"": [""Origin""],
      ""Weight"": 1.0
    }}
  ]
}}";
            }

            string userMessage = $"Generate my backstory. I am currently at {Find.CurrentMap.Parent.Label}.";

            // If it's a leader, bump priority slightly so the Storyteller can evaluate them sooner
            var options = new ChatOptions { priority = isLeader ? 5 : -1 };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => 
                {
                    HandleVisitorBackstoryResult(targetPawn, isLeader, result);
                },
                options
            );
            return true;
        }

        private static void HandleVisitorBackstoryResult(Pawn targetPawn, bool isLeader, ChatResult result)
        {
            if (result.success)
            {
                try
                {
                    string json = JsonHelper.ExtractJson(result.content);
                    if (json == null) { Log.Warning("[RimSynapse-Psychology] No JSON found in backstory response."); return; }

                    var parsed = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, object>>>>(json);
                    if (parsed != null && parsed.ContainsKey("Memories"))
                    {
                        foreach (var memDict in parsed["Memories"])
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
                                decayRate = 0f, // Backstories don't decay
                                tags = tagsList,
                                memoryType = "Backstory",
                                gameTick = Find.TickManager.TicksGame
                            });
                        }
                        MarkBackstoryCreated(targetPawn);
                        Log.Message($"[RimSynapse-Psychology] Opportunistic visitor backstory generated for {targetPawn.Name.ToStringShort} (Leader: {isLeader}).");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimSynapse-Psychology] Failed to parse visitor backstory JSON: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Triggered by the Core framework when the LLM queue is idle.
        /// Scans for a colonist who is flagged for a daily journal update and evaluates their profile.
        /// </summary>
        public static bool TriggerOpportunisticProfileEvaluation()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return false;

            // Find a colonist awaiting a journal update
            var targetPawn = Find.CurrentMap.mapPawns.FreeColonists
                .Where(p => {
                    var comp = p.TryGetComp<SynapsePawnComp>();
                    return comp != null && comp.isAwaitingJournalUpdate;
                })
                .RandomElementWithFallback();

            if (targetPawn == null) return false;

            var pawnComp = targetPawn.TryGetComp<SynapsePawnComp>();
            var coreComp = targetPawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            
            if (pawnComp == null || coreComp == null) return false;

            var memories = coreComp.memories;
            float averageMood = pawnComp.savedAverageMood;
            int currentDay = GenDate.DaysPassed;

            // Unset the flag immediately so we don't spam them if the queue fails
            pawnComp.isAwaitingJournalUpdate = false;

            // Route to existing API evaluation method, but override priority to -1
            // (QueueDailyPsychologyReview uses PromptAsync which accepts an options object, we need to modify QueueDailyPsychologyReview slightly to support options or priority -1)
            // Wait, I will just call it and it will enqueue. The opportunistic manager triggers this when the queue is ALREADY empty, so priority doesn't matter as much, it will execute instantly.
            // Actually, we should pass options. Let's look at QueueDailyPsychologyReview in SynapsePsychologyEvaluation.cs to ensure it uses options.
            // Oh, I haven't modified QueueDailyPsychologyReview yet. I will do that next.
            
            QueueDailyPsychologyReview(targetPawn, averageMood, memories, (success) => {
                if (success)
                {
                    pawnComp.lastJournalUpdateDay = currentDay;
                }
                else 
                {
                    // If it failed, flag them again so it retries later
                    pawnComp.isAwaitingJournalUpdate = true;
                }
            }, true); // true = isOpportunistic
            
            return true;
        }

        /// <summary>
        /// Triggered by the Core framework when the LLM queue is idle.
        /// Scans all world faction leaders for backstory generation.
        /// These are "World VIPs" — they get backstories regardless of whether they're on a map.
        /// If a leader dies or loses leadership, the new leader will get queued next cycle.
        /// If StoryTeller has already generated a faction history, it's included as context.
        /// </summary>
        public static bool TriggerLeaderBackstoryGeneration()
        {
            if (Current.ProgramState != ProgramState.Playing) return false;
            if (Find.FactionManager == null) return false;

            // Find a faction leader who needs a backstory
            Pawn targetLeader = null;
            foreach (var faction in Find.FactionManager.AllFactions)
            {
                if (faction == null || faction.IsPlayer || faction.Hidden) continue;
                if (faction.leader == null || !faction.leader.RaceProps.Humanlike) continue;
                if (faction.leader.Dead) continue;

                var coreComp = faction.leader.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
                if (coreComp == null) continue;

                if (NeedsBackstory(faction.leader))
                {
                    targetLeader = faction.leader;
                    break; // Take the first one found — we do one per cycle
                }
            }

            if (targetLeader == null) return false;

            var leaderCoreComp = targetLeader.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (leaderCoreComp == null) return false;

            string factionName = targetLeader.Faction?.Name ?? "Unknown Faction";
            string factionType = targetLeader.Faction?.def?.LabelCap ?? "Faction";
            string title = targetLeader.royalty?.MostSeniorTitle?.def?.label ?? "Leader";

            // Get traits for richer backstory
            string traits = targetLeader.story?.traits?.allTraits != null
                ? string.Join(", ", targetLeader.story.traits.allTraits.Select(t => t.LabelCap))
                : "None";

            // Check if StoryTeller has generated a faction history to use as context
            string factionHistoryContext = "";
            if (SynapseCore.IsModLoaded("RimSynapseStoryTeller") && targetLeader.Faction != null)
            {
                // Access faction history via the core comp's world data without direct StoryTeller type dependency
                // We use reflection-free approach: check SynapseCoreWorldComponent's faction tracker for any history data
                // Actually, StoryTeller stores this in its own WorldComponent. We can safely check via Find.World.
                try
                {
                    foreach (var comp in Find.World.components)
                    {
                        // Check by type name to avoid hard dependency on StoryTeller assembly
                        if (comp.GetType().Name == "SynapseStoryTellerWorldComponent")
                        {
                            var method = comp.GetType().GetMethod("GetOrCreateStoryTracker");
                            if (method != null)
                            {
                                var tracker = method.Invoke(comp, new object[] { targetLeader.Faction.GetUniqueLoadID() });
                                if (tracker != null)
                                {
                                    var historyField = tracker.GetType().GetField("factionHistory");
                                    if (historyField != null)
                                    {
                                        string history = historyField.GetValue(tracker) as string;
                                        if (!string.IsNullOrEmpty(history))
                                        {
                                            factionHistoryContext = $"\n\nFaction History (already established):\n\"{history}\"\nYour life events should be consistent with this faction history.";
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimSynapse-Psychology] Could not read faction history from StoryTeller: {ex.Message}");
                }
            }

            string systemPrompt = $@"You are {targetLeader.Name.ToStringShort}, the {title} of {factionName} (a {factionType}) in the RimWorld universe.
Your traits are: {traits}.
You must generate a detailed life history consisting of exactly 3 or 4 significant life events.
These events MUST include:
1. 'Faction Join' (or birth into the faction).
2. 'Faction Rise' (how you gained influence).
3. 'Faction Control' (how you took leadership).
(Optional) 4. A flavor memory that adds personality.{factionHistoryContext}

You MUST respond strictly in valid JSON format:
{{
  ""Memories"": [
    {{
      ""Summary"": ""I was born into the harsh wastes and immediately inducted into the tribe..."",
      ""Tags"": [""Faction Join"", ""Origin""],
      ""Weight"": 2.0
    }}
  ]
}}";

            string userMessage = $"Generate my backstory as faction leader of {factionName}.";
            var options = new ChatOptions { priority = 4 }; // Below colonist tasks, below faction history

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result =>
                {
                    HandleVisitorBackstoryResult(targetLeader, true, result);
                },
                options
            );
            return true;
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
