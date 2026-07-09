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
                                            absTick = SynapseDateHelper.GameTickToAbsTick(pastEvent.gameTick),
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
        /// Generates memory-first backstories for important non-colonist visitors.
        /// Visitors get 1-2 memories (one per backstory they have) + hometown, no personality synthesis.
        /// </summary>
        public static bool TriggerOpportunisticVisitorBackstory()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return false;

            var targetPawn = Find.CurrentMap.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Humanlike && !p.IsColonist && !p.Faction?.IsPlayer == true && IsImportantPawn(p) && NeedsBackstory(p))
                .RandomElementWithFallback();

            if (targetPawn == null) return false;

            var coreComp = targetPawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (coreComp == null) return false;

            string factionName = targetPawn.Faction?.Name ?? "Outlander";
            string factionType = targetPawn.Faction?.def?.LabelCap ?? "Faction";

            // Start with childhood if available, otherwise go straight to adulthood
            if (targetPawn.story?.Childhood != null)
            {
                GenerateVisitorChildhoodMemory(targetPawn, coreComp, factionName, factionType);
            }
            else if (targetPawn.story?.Adulthood != null)
            {
                GenerateVisitorAdulthoodMemory(targetPawn, coreComp, factionName, factionType);
            }
            else
            {
                return false; // No backstory data at all
            }
            return true;
        }

        private static void GenerateVisitorChildhoodMemory(Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp, string factionName, string factionType)
        {
            var childhood = pawn.story.Childhood;
            string childhoodTitle = childhood?.title ?? "Unknown";
            string childhoodDesc = childhood?.description ?? "An unremarkable childhood.";
            string skillBonuses = FormatSkillGains(childhood);

            string systemPrompt = @"You are writing a vivid first-person memory for a visitor in the RimWorld universe.
This memory is from their CHILDHOOD. Keep it brief and grounded.

RULES:
- Write 80-120 words, first person (""I"", ""me"", ""my"")
- Ground the memory in their skill bonuses — their abilities come from real experience
- Generate a ""Hometown"" — their place of origin, matching their faction type:
  - Outlander → a named settlement (e.g., ""Port Valen"")
  - Tribal → a geographic feature or camp (e.g., ""the Ashen Ridge camp"")
  - Pirate → a ship or den (e.g., ""the Rust Fang"")
  - Imperial → a city or estate
  - Nomadic/orphan → something vague (e.g., ""the trade roads south of Helixon"")

You MUST respond in valid JSON:
{
  ""Memory"": ""I remember...(80-120 words)..."",
  ""Hometown"": ""Port Valen"",
  ""Tags"": [""Origin"", ""Childhood""]
}";

            string userMessage = $@"Visitor: {pawn.Name.ToStringShort} from {factionName} ({factionType})
Childhood: ""{childhoodTitle}""
Description: ""{childhoodDesc}""
Skills: {skillBonuses}";

            var options = new ChatOptions { priority = -1 };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => OnVisitorChildhoodGenerated(result, pawn, coreComp, factionName, factionType),
                options
            );
        }

        private static void OnVisitorChildhoodGenerated(ChatResult result, Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp, string factionName, string factionType)
        {
            if (result.success)
            {
                try
                {
                    string json = JsonHelper.ExtractJson(result.content);
                    if (json != null)
                    {
                        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (parsed != null && parsed.ContainsKey("Memory"))
                        {
                            string memoryText = parsed["Memory"].ToString();
                            var tags = new List<string> { "Childhood", "Origin" };
                            if (parsed.ContainsKey("Tags") && parsed["Tags"] is Newtonsoft.Json.Linq.JArray arr)
                                tags = arr.Select(t => t.ToString()).ToList();

                            if (parsed.ContainsKey("Hometown"))
                                coreComp.hometown = parsed["Hometown"].ToString();

                            long childTick = SynapseDateHelper.GetChildhoodMemoryTick(pawn);
                            coreComp.memories.Add(new WeightedMemory
                            {
                                summary = memoryText,
                                weight = 2.0f,
                                baseWeight = 2.0f,
                                decayRate = 0f,
                                tags = tags,
                                memoryType = "BackstoryChildhood",
                                absTick = childTick,
                                gameTick = (int)(childTick - SynapseDateHelper.GetAdjustmentTick())
                            });

                            Log.Message($"[RimSynapse-Psychology] Visitor childhood memory for {pawn.Name.ToStringShort}. Hometown: {coreComp.hometown ?? "none"}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimSynapse-Psychology] Failed to parse visitor childhood memory: {ex.Message}");
                }
            }

            // Chain to adulthood if available, otherwise finalize
            if (pawn.story?.Adulthood != null)
            {
                GenerateVisitorAdulthoodMemory(pawn, coreComp, factionName, factionType);
            }
            else
            {
                MarkBackstoryCreated(pawn);
                Log.Message($"[RimSynapse-Psychology] Visitor backstory complete for {pawn.Name.ToStringShort} (childhood only).");
            }
        }

        private static void GenerateVisitorAdulthoodMemory(Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp, string factionName, string factionType)
        {
            var adulthood = pawn.story.Adulthood;
            string adulthoodTitle = adulthood?.title ?? "Unknown";
            string adulthoodDesc = adulthood?.description ?? "An uneventful adult life.";
            string skillBonuses = FormatSkillGains(adulthood);

            // Include childhood for continuity if we have it
            var childhoodMem = coreComp.memories.LastOrDefault(m => m.memoryType == "BackstoryChildhood");
            string childhoodContext = childhoodMem != null
                ? $"\nChildhood Memory (maintain continuity): \"{childhoodMem.summary}\""
                : "";
            string hometownContext = !string.IsNullOrEmpty(coreComp.hometown)
                ? $"\nHometown: {coreComp.hometown}"
                : "";

            string systemPrompt = @"You are writing a vivid first-person memory for a visitor in the RimWorld universe.
This memory is from their ADULTHOOD. Keep it brief and grounded.

RULES:
- Write 80-120 words, first person (""I"", ""me"", ""my"")
- Ground the memory in their skill bonuses
- If a childhood memory is provided, maintain narrative continuity
- This should be a defining adult moment — what made them who they are

You MUST respond in valid JSON:
{
  ""Memory"": ""The day I...(80-120 words)..."",
  ""Tags"": [""Adulthood"", ""Defining""]
}";

            string userMessage = $@"Visitor: {pawn.Name.ToStringShort} from {factionName} ({factionType})
Adulthood: ""{adulthoodTitle}""
Description: ""{adulthoodDesc}""
Skills: {skillBonuses}{hometownContext}{childhoodContext}";

            var options = new ChatOptions { priority = -1 };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => OnVisitorAdulthoodGenerated(result, pawn, coreComp),
                options
            );
        }

        private static void OnVisitorAdulthoodGenerated(ChatResult result, Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            if (result.success)
            {
                try
                {
                    string json = JsonHelper.ExtractJson(result.content);
                    if (json != null)
                    {
                        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (parsed != null && parsed.ContainsKey("Memory"))
                        {
                            string memoryText = parsed["Memory"].ToString();
                            var tags = new List<string> { "Adulthood", "Defining" };
                            if (parsed.ContainsKey("Tags") && parsed["Tags"] is Newtonsoft.Json.Linq.JArray arr)
                                tags = arr.Select(t => t.ToString()).ToList();

                            long adultTick = SynapseDateHelper.GetAdulthoodMemoryTick(pawn);
                            coreComp.memories.Add(new WeightedMemory
                            {
                                summary = memoryText,
                                weight = 2.0f,
                                baseWeight = 2.0f,
                                decayRate = 0f,
                                tags = tags,
                                memoryType = "BackstoryAdulthood",
                                absTick = adultTick,
                                gameTick = (int)(adultTick - SynapseDateHelper.GetAdjustmentTick())
                            });

                            Log.Message($"[RimSynapse-Psychology] Visitor adulthood memory for {pawn.Name.ToStringShort} ({memoryText.Length} chars).");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimSynapse-Psychology] Failed to parse visitor adulthood memory: {ex.Message}");
                }
            }

            MarkBackstoryCreated(pawn);
            Log.Message($"[RimSynapse-Psychology] Visitor backstory complete for {pawn.Name.ToStringShort}.");
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
        ///
        /// Uses the memory-first pipeline:
        ///   Step 1: Childhood memory (grounded in vanilla backstory + skills)
        ///   Step 2: Adulthood/Rise memory (how they gained power, grounded in skills + faction context)
        /// </summary>
        public static bool TriggerLeaderBackstoryGeneration()
        {
            if (Current.ProgramState != ProgramState.Playing) return false;
            if (Find.FactionManager == null) return false;

            // Find a faction leader who needs a backstory
            Pawn targetLeader = null;
            Faction targetFaction = null;
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
                    targetFaction = faction;
                    break; // Take the first one found — we do one per cycle
                }
            }

            if (targetLeader == null || targetFaction == null) return false;

            var leaderCoreComp = targetLeader.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (leaderCoreComp == null) return false;

            // Get faction history from StoryTeller (if available) for context
            string factionHistoryContext = GetFactionHistoryContext(targetFaction);

            // Step 1: Generate childhood memory for the leader
            GenerateLeaderChildhoodMemory(targetLeader, targetFaction, leaderCoreComp, factionHistoryContext);
            return true;
        }

        private static string GetFactionHistoryContext(Faction faction)
        {
            if (!SynapseCore.IsModLoaded("RimSynapseStoryTeller") || faction == null) return "";

            try
            {
                foreach (var comp in Find.World.components)
                {
                    if (comp.GetType().Name == "SynapseStoryTellerWorldComponent")
                    {
                        var method = comp.GetType().GetMethod("GetOrCreateStoryTracker");
                        if (method != null)
                        {
                            var tracker = method.Invoke(comp, new object[] { faction.GetUniqueLoadID() });
                            if (tracker != null)
                            {
                                var historyField = tracker.GetType().GetField("factionHistory");
                                if (historyField != null)
                                {
                                    string history = historyField.GetValue(tracker) as string;
                                    if (!string.IsNullOrEmpty(history))
                                    {
                                        return $"\nFaction History (already established — your memories must be consistent with this):\n\"{history}\"";
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
            return "";
        }

        private static void GenerateLeaderChildhoodMemory(Pawn leader, Faction faction, RimSynapse.Comps.SynapseCorePawnComp coreComp, string factionHistoryContext)
        {
            var childhood = leader.story?.Childhood;
            string childhoodTitle = childhood?.title ?? "Unknown";
            string childhoodDesc = childhood?.description ?? "An unremarkable childhood.";
            string skillBonuses = FormatSkillGains(childhood);
            string disabledWork = FormatDisabledWork(childhood);

            string systemPrompt = @"You are writing a vivid first-person memory for a faction LEADER in the RimWorld universe.
This memory is from their CHILDHOOD — before they held power.

RULES:
- Write 100-200 words, first person (""I"", ""me"", ""my"")
- This is a SINGLE vivid memory, not a life summary
- Ground the memory in the skill bonuses: explain WHAT childhood experience gave them these skills
- If work types are disabled, hint at WHY (trauma, cultural taboo, physical limitation)
- If faction history is provided, the childhood should be consistent with that world
- The memory should hint at the seeds of leadership — even as a child, something set them apart
- You MUST generate a ""Hometown"" — their place of origin, matching the faction type:
  - Outlander → a named settlement or outpost (e.g., ""Kharstead"", ""Port Valen"")
  - Tribal → a geographic feature, camp, or caravan route (e.g., ""the Redstone caravan"", ""the marshlands east of Sleeping Ridge"")
  - Pirate → a ship, station, or raider den (e.g., ""the Rust Fang"", ""Scrapheap Station"")
  - Imperial → a named city or estate (e.g., ""the Stellarch's court at Novium"")
- RimWorld setting: frontier planets, tribal societies, pirate dens, outlander settlements

You MUST respond in valid JSON:
{
  ""Memory"": ""I remember the first time I...(100-200 words)..."",
  ""Hometown"": ""the Redstone caravan"",
  ""Tags"": [""Origin"", ""Childhood"", ""Leadership""],
  ""EmotionalTone"": ""formative""
}";

            string factionName = faction?.Name ?? "Unknown";
            string factionType = faction?.def?.LabelCap ?? "Faction";

            string userMessage = $@"Leader: {leader.Name.ToStringFull}
Faction: {factionName} ({factionType})
Childhood Backstory: ""{childhoodTitle}""
Vanilla Description: ""{childhoodDesc}""
Skill Bonuses from Childhood: {skillBonuses}
{(string.IsNullOrEmpty(disabledWork) ? "" : $"Disabled Work Types: {disabledWork}\n")}{factionHistoryContext}

Write a vivid childhood memory for this future leader.";

            var options = new ChatOptions { priority = 4 };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => OnLeaderChildhoodGenerated(result, leader, faction, coreComp, factionHistoryContext),
                options
            );
        }

        private static void OnLeaderChildhoodGenerated(ChatResult result, Pawn leader, Faction faction, RimSynapse.Comps.SynapseCorePawnComp coreComp, string factionHistoryContext)
        {
            if (result.success)
            {
                try
                {
                    string json = JsonHelper.ExtractJson(result.content);
                    if (json != null)
                    {
                        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (parsed != null && parsed.ContainsKey("Memory"))
                        {
                            string memoryText = parsed["Memory"].ToString();
                            var tags = new List<string> { "Childhood", "Origin" };
                            if (parsed.ContainsKey("Tags") && parsed["Tags"] is Newtonsoft.Json.Linq.JArray arr)
                            {
                                tags = arr.Select(t => t.ToString()).ToList();
                                if (!tags.Contains("Childhood")) tags.Insert(0, "Childhood");
                            }

                            // Store hometown
                            if (parsed.ContainsKey("Hometown"))
                            {
                                coreComp.hometown = parsed["Hometown"].ToString();
                            }

                            long childTick = SynapseDateHelper.GetChildhoodMemoryTick(leader);
                            coreComp.memories.Add(new WeightedMemory
                            {
                                summary = memoryText,
                                weight = 3.0f,
                                baseWeight = 3.0f,
                                decayRate = 0f,
                                tags = tags,
                                memoryType = "BackstoryChildhood",
                                absTick = childTick,
                                gameTick = (int)(childTick - SynapseDateHelper.GetAdjustmentTick())
                            });

                            Log.Message($"[RimSynapse-Psychology] Leader childhood memory generated for {leader.Name.ToStringShort} ({memoryText.Length} chars). Hometown: {coreComp.hometown ?? "none"}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimSynapse-Psychology] Failed to parse leader childhood memory: {ex.Message}");
                }
            }

            // Chain to Step 2: Adulthood/Rise memory
            GenerateLeaderRiseMemory(leader, faction, coreComp, factionHistoryContext);
        }

        private static void GenerateLeaderRiseMemory(Pawn leader, Faction faction, RimSynapse.Comps.SynapseCorePawnComp coreComp, string factionHistoryContext)
        {
            var adulthood = leader.story?.Adulthood;
            string adulthoodTitle = adulthood?.title ?? "Unknown";
            string adulthoodDesc = adulthood?.description ?? "An uneventful adult life.";
            string skillBonuses = FormatSkillGains(adulthood);
            string disabledWork = FormatDisabledWork(adulthood);

            string title = leader.royalty?.MostSeniorTitle?.def?.label ?? "Leader";
            string factionName = faction?.Name ?? "Unknown";
            string factionType = faction?.def?.LabelCap ?? "Faction";
            string traits = leader.story?.traits?.allTraits != null
                ? string.Join(", ", leader.story.traits.allTraits.Select(t => t.LabelCap))
                : "None";

            // Include the childhood memory for continuity
            var childhoodMem = coreComp.memories.LastOrDefault(m => m.memoryType == "BackstoryChildhood");
            string childhoodContext = childhoodMem != null
                ? $"\nChildhood Memory (already generated — maintain continuity):\n\"{childhoodMem.summary}\""
                : "";

            // Include hometown if generated
            string hometownContext = !string.IsNullOrEmpty(coreComp.hometown)
                ? $"\nHometown: {coreComp.hometown}"
                : "";

            string systemPrompt = @"You are writing a vivid first-person memory for a faction LEADER in the RimWorld universe.
This memory is from their ADULTHOOD — specifically about their RISE TO POWER.

RULES:
- Write 150-250 words, first person (""I"", ""me"", ""my"")
- This must cover TWO key moments woven into one memory:
  1. How you gained influence and skill in the faction (grounded in the adulthood backstory + skill bonuses)
  2. The specific moment you took control (a challenge, a crisis, a succession, a coup)
- If faction history is provided, your rise must be consistent with it
- Ground the memory in the skill bonuses — your adulthood skills are what let you seize power
- The memory should feel like the defining moment of your life
- RimWorld setting: political intrigue, tribal succession, pirate might-makes-right, outlander elections

You MUST respond in valid JSON:
{
  ""Memory"": ""The night the old chief died, I...(150-250 words)..."",
  ""Tags"": [""Adulthood"", ""FactionRise"", ""Leadership""],
  ""EmotionalTone"": ""triumphant""
}";

            string userMessage = $@"Leader: {leader.Name.ToStringFull}, {title} of {factionName} ({factionType})
Traits: {traits}
Adulthood Backstory: ""{adulthoodTitle}""
Vanilla Description: ""{adulthoodDesc}""
Skill Bonuses from Adulthood: {skillBonuses}
{(string.IsNullOrEmpty(disabledWork) ? "" : $"Disabled Work Types: {disabledWork}\n")}{hometownContext}{childhoodContext}{factionHistoryContext}

Write their rise-to-power memory.";

            var options = new ChatOptions { priority = 4 };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => OnLeaderRiseGenerated(result, leader, coreComp),
                options
            );
        }

        private static void OnLeaderRiseGenerated(ChatResult result, Pawn leader, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            if (result.success)
            {
                try
                {
                    string json = JsonHelper.ExtractJson(result.content);
                    if (json != null)
                    {
                        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (parsed != null && parsed.ContainsKey("Memory"))
                        {
                            string memoryText = parsed["Memory"].ToString();
                            var tags = new List<string> { "Adulthood", "FactionRise", "Leadership" };
                            if (parsed.ContainsKey("Tags") && parsed["Tags"] is Newtonsoft.Json.Linq.JArray arr)
                            {
                                tags = arr.Select(t => t.ToString()).ToList();
                                if (!tags.Contains("Leadership")) tags.Add("Leadership");
                            }

                            long riseTick = SynapseDateHelper.GetAdulthoodMemoryTick(leader);
                            coreComp.memories.Add(new WeightedMemory
                            {
                                summary = memoryText,
                                weight = 4.0f,
                                baseWeight = 4.0f,
                                decayRate = 0f,
                                tags = tags,
                                memoryType = "BackstoryAdulthood",
                                absTick = riseTick,
                                gameTick = (int)(riseTick - SynapseDateHelper.GetAdjustmentTick())
                            });

                            Log.Message($"[RimSynapse-Psychology] Leader rise memory generated for {leader.Name.ToStringShort} ({memoryText.Length} chars).");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimSynapse-Psychology] Failed to parse leader rise memory: {ex.Message}");
                }
            }

            // Chain to Step 3: Personality synthesis (permanent — no daily reviews for leaders)
            GenerateLeaderPersonalityProfile(leader, coreComp);
        }

        /// <summary>
        /// Step 3 for leaders: Synthesize permanent psychological profile from memories.
        /// Leaders get MBTI/archetype/temperament but NO daily journal reviews.
        /// </summary>
        private static void GenerateLeaderPersonalityProfile(Pawn leader, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            string traits = string.Join(", ", leader.story?.traits?.allTraits?.Select(t => t.Label) ?? Enumerable.Empty<string>());

            var childhoodMem = coreComp.memories.LastOrDefault(m => m.memoryType == "BackstoryChildhood");
            var adulthoodMem = coreComp.memories.LastOrDefault(m => m.memoryType == "BackstoryAdulthood");

            string memoriesContext = "";
            if (childhoodMem != null) memoriesContext += $"Childhood Memory:\n\"{childhoodMem.summary}\"\n\n";
            if (adulthoodMem != null) memoriesContext += $"Adulthood/Rise Memory:\n\"{adulthoodMem.summary}\"\n\n";

            string hometownContext = !string.IsNullOrEmpty(coreComp.hometown) ? $"\nHometown: {coreComp.hometown}" : "";
            string factionName = leader.Faction?.Name ?? "Unknown";
            string factionType = leader.Faction?.def?.LabelCap ?? "Faction";

            string systemPrompt = @"You are analyzing the psychology of a faction LEADER in the RimWorld universe.
Given their childhood memory, rise-to-power memory, and personality traits, synthesize a permanent psychological profile.
This leader does NOT get daily reviews — this profile is their permanent character assessment.

OUTPUT:
1. Personality — A 2-3 sentence personality summary (third person). How do they lead? What drives them? What is their weakness?
2. Archetypes — Three psychological classifications.
3. Leadership Style — One sentence describing how they run their faction.

You MUST respond in valid JSON:
{
  ""Personality"": ""She is a calculating strategist who..."",
  ""JungianType"": ""INTJ"",
  ""CoreArchetype"": ""Ruler"",
  ""Temperament"": ""Choleric"",
  ""LeadershipStyle"": ""Rules through fear and strict discipline, but rewards loyalty generously.""
}";

            string userMessage = $@"Leader: {leader.Name.ToStringShort}, of {factionName} ({factionType})
Age: {leader.ageTracker?.AgeBiologicalYears ?? 0}
Gender: {leader.gender}
Traits: {traits}{hometownContext}

{memoriesContext}Synthesize their permanent psychological profile.";

            var options = new ChatOptions { priority = 4 };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => OnLeaderPersonalityGenerated(result, leader, coreComp),
                options
            );
        }

        private static void OnLeaderPersonalityGenerated(ChatResult result, Pawn leader, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            if (result.success)
            {
                try
                {
                    string json = JsonHelper.ExtractJson(result.content);
                    if (json != null)
                    {
                        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (parsed != null)
                        {
                            if (parsed.TryGetValue("Personality", out object personalityObj))
                                coreComp.personalitySummary = personalityObj.ToString();

                            coreComp.llmTraits.Clear();
                            if (parsed.TryGetValue("JungianType", out object jungian))
                                coreComp.llmTraits.Add($"Jungian Type: {jungian}");
                            if (parsed.TryGetValue("CoreArchetype", out object archetype))
                                coreComp.llmTraits.Add($"Core Archetype: {archetype}");
                            if (parsed.TryGetValue("Temperament", out object temperament))
                                coreComp.llmTraits.Add($"Temperament: {temperament}");
                            if (parsed.TryGetValue("LeadershipStyle", out object style))
                                coreComp.llmTraits.Add($"Leadership: {style}");

                            Log.Message($"[RimSynapse-Psychology] Leader personality profile synthesized for {leader.Name.ToStringShort}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimSynapse-Psychology] Failed to parse leader personality profile: {ex.Message}");
                }
            }

            // Finalize — mark backstory complete
            MarkBackstoryCreated(leader);
            Log.Message($"[RimSynapse-Psychology] Leader backstory pipeline complete for {leader.Name.ToStringShort} (3-step).");
        }

        /// <summary>
        /// Formats skill gains from a BackstoryDef into a readable string.
        /// e.g., "+4 Mining, +2 Crafting, -3 Social"
        /// </summary>
        private static string FormatSkillGains(BackstoryDef backstory)
        {
            if (backstory?.skillGains == null || backstory.skillGains.Count == 0) return "None";
            var parts = new List<string>();
            foreach (var sg in backstory.skillGains)
            {
                string sign = sg.amount >= 0 ? "+" : "";
                parts.Add($"{sign}{sg.amount} {sg.skill.label}");
            }
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Formats disabled work types from a BackstoryDef.
        /// </summary>
        private static string FormatDisabledWork(BackstoryDef backstory)
        {
            if (backstory == null || backstory.workDisables == WorkTags.None) return "";
            var disabled = new List<string>();
            foreach (WorkTags tag in Enum.GetValues(typeof(WorkTags)))
            {
                if (tag == WorkTags.None) continue;
                if ((backstory.workDisables & tag) != 0) disabled.Add(tag.ToString());
            }
            return disabled.Count > 0 ? string.Join(", ", disabled) : "";
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
