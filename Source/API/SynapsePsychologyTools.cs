using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Newtonsoft.Json;
using RimSynapse.Psychology.Comps;
using RimSynapse.Comps;
using RimSynapse.Psychology.Models;
using RimSynapse.Models;

namespace RimSynapse.Psychology.API
{
    public static class SynapsePsychologyTools
    {
        public static void RegisterTools()
        {
            // Tool 1: get_colonist_psychology_profile
            SynapseToolRegistry.RegisterTool(
                "get_colonist_psychology_profile",
                "Retrieves traits, sanity breaks predictions, weighted memories, burdens, social network relationship stats (trust/familiarity), and therapy sessions history for a colonist.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        pawnName = new
                        {
                            type = "string",
                            description = "Name of the colonist (e.g. John)"
                        }
                    },
                    required = new[] { "pawnName" }
                },
                GetColonistPsychologyProfileHandler
            );

            // Tool 2: get_recent_social_interactions
            SynapseToolRegistry.RegisterTool(
                "get_recent_social_interactions",
                "Retrieves a chronological list of recent social chatter logs (insults, chit-chat, deep talks) on the map.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        pawnName = new
                        {
                            type = "string",
                            description = "Optional: Filter social interactions involving a specific colonist name"
                        }
                    }
                },
                GetRecentSocialInteractionsHandler
            );

            SynapseLogger.Message("[RimSynapse Psychology] Dynamic MCP tools registered with Core.");
        }

        private static string GetColonistPsychologyProfileHandler(string argumentsJson)
        {
            try
            {
                var args = JsonConvert.DeserializeAnonymousType(argumentsJson, new { pawnName = "" });
                string pawnName = args?.pawnName;
                if (string.IsNullOrEmpty(pawnName))
                {
                    return "{\"error\": \"Missing required argument 'pawnName'.\"}";
                }

                Pawn pawn = FindPawnByName(pawnName);
                if (pawn == null)
                {
                    return $"{{\"error\": \"Pawn with name '{pawnName}' not found on any map or world pawns.\"}}";
                }

                var coreComp = pawn.TryGetComp<SynapseCorePawnComp>();
                var psychComp = pawn.TryGetComp<SynapsePawnComp>();

                var response = new Dictionary<string, object>();
                response["pawnId"] = pawn.ThingID;
                response["name"] = pawn.LabelShort;
                response["gender"] = pawn.gender.ToString();
                response["age"] = pawn.ageTracker?.AgeBiologicalYears ?? 0;
                response["faction"] = pawn.Faction?.Name ?? "None";
                response["backstoryChildhood"] = pawn.story?.Childhood?.title ?? "Unknown";
                response["backstoryAdulthood"] = pawn.story?.Adulthood?.title ?? "None";
                if (ModsConfig.IdeologyActive && pawn.Ideo != null)
                {
                    response["ideology"] = pawn.Ideo.name;
                }

                // Core shared data
                if (coreComp != null)
                {
                    response["personalitySummary"] = coreComp.personalitySummary ?? "No summary generated yet.";
                    response["dynamicBackstory"] = coreComp.dynamicBackstory ?? "";
                    response["clinicalAssessment"] = coreComp.clinicalAssessment ?? "";
                    response["hometown"] = coreComp.hometown ?? "";
                    response["llmTraits"] = coreComp.llmTraits ?? new List<string>();
                    response["moodBurdens"] = coreComp.GetTopMemoryBurdens(5);
                    
                    var recentMemories = new List<object>();
                    foreach (var mem in coreComp.memories.OrderByDescending(m => m.absTick).Take(10))
                    {
                        long memGameTick = mem.absTick - RimSynapse.Utils.SynapseDateHelper.GetAdjustmentTick();
                        recentMemories.Add(new {
                            summary = mem.summary,
                            type = mem.memoryType,
                            weight = mem.weight,
                            tags = mem.tags,
                            ageHours = (Find.TickManager.TicksGame - memGameTick) / 2500f // estimated hours ago
                        });
                    }
                    response["recentMemories"] = recentMemories;
                }

                // Psychology specific data
                if (psychComp != null)
                {
                    response["breakCategory"] = psychComp.breakCategory.ToString();
                    response["predictedBreakState"] = psychComp.predictedBreakState?.defName ?? "None";
                    response["currentBreakWarning"] = psychComp.currentBreakWarning ?? "None";
                    response["isEuphoric"] = psychComp.isEuphoric;
                    response["ideologyZealot"] = psychComp.ideologyZealot;
                    
                    if (psychComp.dynamicTraits != null)
                    {
                        response["dynamicTraits"] = psychComp.dynamicTraits.Select(t => new {
                            defName = t.traitDef?.defName,
                            label = t.traitDef?.LabelCap.ToString() ?? t.traitDef?.label,
                            reason = t.reason,
                            tickAdded = t.tickAdded
                        }).ToList();
                    }

                    // Social network details
                    var socialNet = new List<object>();
                    if (psychComp.socialNetwork != null)
                    {
                        foreach (var kvp in psychComp.socialNetwork)
                        {
                            string targetPawnId = kvp.Key;
                            SocialRecord record = kvp.Value;
                            Pawn targetPawn = FindPawnById(targetPawnId);
                            if (targetPawn != null)
                            {
                                int opinion = pawn.relations?.OpinionOf(targetPawn) ?? 0;
                                string relationLabel = GetRelationshipLabel(pawn, targetPawn);
                                float? opinionIntegral = SynapsePsychology.GetOpinionIntegral(pawn, targetPawn);
                                float? trajectory = SynapsePsychology.GetRelationshipTrajectory(pawn, targetPawn);

                                socialNet.Add(new {
                                    targetName = targetPawn.LabelShort,
                                    relationship = relationLabel,
                                    opinion = opinion,
                                    trust = record.trust,
                                    familiarity = record.familiarity,
                                    opinionIntegral = opinionIntegral,
                                    trajectory = trajectory,
                                    memories = record.relationshipMemories
                                });
                            }
                        }
                    }
                    response["socialNetwork"] = socialNet;

                    // Therapy Transcripts
                    if (psychComp.therapyTranscripts != null)
                    {
                        response["therapyTranscripts"] = psychComp.therapyTranscripts.Select(t => new {
                            otherPawn = t.otherPawnName,
                            sessionTick = t.sessionTick,
                            lines = t.lines
                        }).ToList();
                    }
                }

                return JsonConvert.SerializeObject(response, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return $"{{\"error\": \"Failed to build psychology profile: {ex.Message}\"}}";
            }
        }

        private static string GetRecentSocialInteractionsHandler(string argumentsJson)
        {
            try
            {
                var args = JsonConvert.DeserializeAnonymousType(argumentsJson, new { pawnName = "" });
                string pawnName = args?.pawnName;

                var playLog = Find.PlayLog;
                var results = new List<object>();

                if (playLog != null && playLog.AllEntries != null)
                {
                    int currentTick = Find.TickManager.TicksGame;
                    
                    var initiatorField = typeof(PlayLogEntry_Interaction).GetField("initiator", BindingFlags.NonPublic | BindingFlags.Instance);
                    var recipientField = typeof(PlayLogEntry_Interaction).GetField("recipient", BindingFlags.NonPublic | BindingFlags.Instance);
                    var interactionField = typeof(PlayLogEntry_Interaction).GetField("intDef", BindingFlags.NonPublic | BindingFlags.Instance);

                    foreach (var entry in playLog.AllEntries.AsEnumerable().Reverse())
                    {
                        if (entry is PlayLogEntry_Interaction interactionEntry)
                        {
                            var initiator = initiatorField?.GetValue(interactionEntry) as Pawn;
                            var recipient = recipientField?.GetValue(interactionEntry) as Pawn;
                            var interactionDef = interactionField?.GetValue(interactionEntry) as InteractionDef;

                            string gameString = interactionEntry.ToGameStringFromPOV(null, false);

                            // Filter by pawn name if supplied
                            if (!string.IsNullOrEmpty(pawnName))
                            {
                                bool matchesInitiator = initiator != null && initiator.LabelShort.Equals(pawnName, StringComparison.OrdinalIgnoreCase);
                                bool matchesRecipient = recipient != null && recipient.LabelShort.Equals(pawnName, StringComparison.OrdinalIgnoreCase);
                                bool matchesText = gameString != null && gameString.IndexOf(pawnName, StringComparison.OrdinalIgnoreCase) >= 0;

                                if (!matchesInitiator && !matchesRecipient && !matchesText)
                                {
                                    continue;
                                }
                            }

                            results.Add(new {
                                initiator = initiator?.LabelShort ?? "Unknown",
                                recipient = recipient?.LabelShort ?? "Unknown",
                                interaction = interactionDef?.defName ?? "Interaction",
                                text = gameString,
                                ageHours = (currentTick - interactionEntry.Tick) / 2500f // estimated hours ago
                            });

                            if (results.Count >= 20) break;
                        }
                    }
                }

                return JsonConvert.SerializeObject(results, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return $"{{\"error\": \"Failed to retrieve recent social interactions: {ex.Message}\"}}";
            }
        }

        private static Pawn FindPawnByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            foreach (var map in Find.Maps)
            {
                if (map.mapPawns == null) continue;
                var p = map.mapPawns.AllPawns.FirstOrDefault(x => x.LabelShort.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (p != null) return p;
            }

            var worldPawn = Find.WorldPawns?.AllPawnsAlive?.FirstOrDefault(x => x.LabelShort.Equals(name, StringComparison.OrdinalIgnoreCase));
            return worldPawn;
        }

        private static Pawn FindPawnById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            foreach (var map in Find.Maps)
            {
                if (map.mapPawns == null) continue;
                var p = map.mapPawns.AllPawns.FirstOrDefault(x => x.ThingID == id);
                if (p != null) return p;
            }

            var worldPawn = Find.WorldPawns?.AllPawnsAlive?.FirstOrDefault(x => x.ThingID == id);
            return worldPawn;
        }

        private static string GetRelationshipLabel(Pawn pawn, Pawn target)
        {
            if (pawn.relations == null || target == null) return "Acquaintance";
            var rel = pawn.relations.DirectRelations.FirstOrDefault(r => r.otherPawn == target);
            if (rel != null)
            {
                return rel.def.LabelCap.ToString();
            }
            return "Acquaintance";
        }

        public static bool HandleCustomBreak(Pawn pawn, string action, string targetPawnName, int? targetX, int? targetZ)
        {
            if (pawn == null) return false;

            if (action.Equals("TrashClean", StringComparison.OrdinalIgnoreCase))
            {
                string stateDefName = "Synapse_SuicidalAntagonize";
                if (targetX.HasValue && targetZ.HasValue)
                {
                    string[] options = { "Synapse_SuicidalAntagonize", "Synapse_SuicidalBurn", "Synapse_SuicidalStarve" };
                    stateDefName = options[Rand.Range(0, options.Length)];
                }

                var stateDef = DefDatabase<MentalStateDef>.GetNamedSilentFail(stateDefName);
                if (stateDef != null)
                {
                    pawn.mindState?.mentalStateHandler?.TryStartMentalState(stateDef, "AI-Driven Suicidal Break");
                    return true;
                }
            }
            else if (action.Equals("OverWrite", StringComparison.OrdinalIgnoreCase))
            {
                var stateDef = DefDatabase<MentalStateDef>.GetNamedSilentFail("Synapse_MentalState_Homicidal");
                if (stateDef != null)
                {
                    pawn.mindState?.mentalStateHandler?.TryStartMentalState(stateDef, "AI-Driven Homicidal Break");
                    return true;
                }
            }
            else if (action.Equals("Depart", StringComparison.OrdinalIgnoreCase))
            {
                var stateDef = DefDatabase<MentalStateDef>.GetNamedSilentFail("Synapse_MentalState_AbandonColony");
                if (stateDef != null)
                {
                    pawn.mindState?.mentalStateHandler?.TryStartMentalState(stateDef, "AI-Driven Departure Break");
                    return true;
                }
            }
            else if (action.Equals("TraumaSnap", StringComparison.OrdinalIgnoreCase))
            {
                var stateDef = DefDatabase<MentalStateDef>.GetNamedSilentFail("Synapse_TraumaTrigger");
                if (stateDef != null)
                {
                    pawn.mindState?.mentalStateHandler?.TryStartMentalState(stateDef, "AI-Driven PTSD Trauma Break");
                    return true;
                }
            }

            return false;
        }
    }
}
