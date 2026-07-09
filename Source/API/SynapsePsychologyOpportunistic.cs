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
    /// Opportunistic background tasks: Memory generation from PastEvents,
    /// visitor backstory generation, and importance filtering.
    /// </summary>
    public static partial class SynapsePsychology
    {
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
        /// Generates a low-priority background backstory for an important non-colonist.
        /// </summary>
        public static void TriggerOpportunisticVisitorBackstory()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;

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
