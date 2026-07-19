using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.Psychology.Comps;
using RimSynapse.Models;
using RimSynapse.Psychology;
using Newtonsoft.Json;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(MarriageCeremonyUtility), "Married")]
    public static class Patch_MarriageCeremonyUtility_Married
    {
        public static void Postfix(Pawn firstPawn, Pawn secondPawn)
        {
            if (firstPawn == null || secondPawn == null) return;

            var comp1 = firstPawn.GetComp<SynapsePawnComp>();
            if (comp1 != null && comp1.socialNetwork.TryGetValue(secondPawn.GetUniqueLoadID(), out var record1))
            {
                if (record1.relationshipMemories.Count > 0)
                {
                    string mem1 = record1.relationshipMemories.RandomElement();
                    Find.LetterStack.ReceiveLetter($"{firstPawn.Name.ToStringShort}'s Vows", $"During the ceremony, {firstPawn.Name.ToStringShort} spoke from the heart:\n\n\"{mem1}\"", LetterDefOf.PositiveEvent, firstPawn);
                }
            }
            
            var comp2 = secondPawn.GetComp<SynapsePawnComp>();
            if (comp2 != null && comp2.socialNetwork.TryGetValue(firstPawn.GetUniqueLoadID(), out var record2))
            {
                if (record2.relationshipMemories.Count > 0)
                {
                    string mem2 = record2.relationshipMemories.RandomElement();
                    Find.LetterStack.ReceiveLetter($"{secondPawn.Name.ToStringShort}'s Vows", $"During the ceremony, {secondPawn.Name.ToStringShort} spoke from the heart:\n\n\"{mem2}\"", LetterDefOf.PositiveEvent, secondPawn);
                }
            }
        }
    }

    [HarmonyPatch(typeof(LordJob_Ritual), "Tick")]
    public static class Patch_LordJob_Ritual_Tick
    {
        public static void Postfix(LordJob_Ritual __instance)
        {
            int ticksPassed = Traverse.Create(__instance).Field("ticksPassed").GetValue<int>();
            if (ticksPassed == 1)
            {
                if (__instance.Ritual != null && __instance.Ritual.def.defName.Contains("Funeral"))
                {
                    Patch_Funeral_Apply.CaptureStartState(__instance);
                }
            }
        }
    }

    public static class Patch_Funeral_Apply
    {
        public class FuneralStartState
        {
            public string weather;
            public string timeOfDay;
            public float averageMood;
            public string moodReason;
            public float averageOpinion;
            public List<Pawn> attendees = new List<Pawn>();
            public Dictionary<string, string> preGeneratedEulogies = new Dictionary<string, string>();
        }

        private static Dictionary<LordJob_Ritual, FuneralStartState> s_StartStates = new Dictionary<LordJob_Ritual, FuneralStartState>();

        private static bool IsConversationsActive()
        {
            return AccessTools.TypeByName("RimSynapse.Conversations.Patches.Patch_Pawn_InteractionsTracker_TryInteractWith") != null;
        }

        public static void CaptureStartState(LordJob_Ritual jobRitual)
        {
            if (jobRitual == null || jobRitual.Map == null || jobRitual.lord == null) return;
            var attendees = jobRitual.lord.ownedPawns.Where(p => p.RaceProps.Humanlike && !p.Dead && p.Spawned).ToList();
            if (attendees.Count == 0) return;

            Pawn deceased = null;
            try 
            {
                var obligation = Traverse.Create(jobRitual).Field("obligation").GetValue();
                if (obligation != null) 
                {
                    var targetInfo = Traverse.Create(obligation).Field("targetA").GetValue();
                    if (targetInfo != null)
                    {
                        var targetThing = Traverse.Create(targetInfo).Property("Thing").GetValue<Thing>();
                        deceased = targetThing as Pawn;
                        if (deceased == null && targetThing is Corpse corpse) deceased = corpse.InnerPawn;
                    }
                }
            } 
            catch {}

            if (deceased == null) return;

            var state = new FuneralStartState
            {
                weather = jobRitual.Map.weatherManager.curWeather.label,
                timeOfDay = GenLocalDate.HourOfDay(jobRitual.Map) < 12 ? "morning" : (GenLocalDate.HourOfDay(jobRitual.Map) < 17 ? "afternoon" : "evening"),
                averageMood = (float)attendees.Average(p => p.needs?.mood?.CurLevel ?? 0.5f),
                averageOpinion = (float)attendees.Average(p => p.relations?.OpinionOf(deceased) ?? 0),
                attendees = attendees
            };

            var reasons = new List<string>();
            if (attendees.Any(p => p.needs?.food?.Starving == true)) reasons.Add("starving");
            if (attendees.Any(p => p.needs?.rest?.CurCategory == RestCategory.Exhausted)) reasons.Add("exhausted");
            state.moodReason = reasons.Count > 0 ? string.Join(" and ", reasons) : "grief";

            s_StartStates[jobRitual] = state;

            if (IsConversationsActive())
            {
                PreGenerateEulogies(deceased, attendees, state);
            }
        }

        public class PreGenEulogyResponse
        {
            public Dictionary<string, string> eulogies;
        }

        private static void PreGenerateEulogies(Pawn deceased, List<Pawn> attendees, FuneralStartState state)
        {
            string systemPrompt = $"You are role-playing as the minds of a group of RimWorld pawns preparing to attend the funeral of {deceased.Name.ToStringShort}.\n" +
                                  $"For each pawn, write a short, personal, one-sentence eulogy statement they would say if they step forward to speak about the deceased.\n" +
                                  $"The statement must be in the first-person (\"I remember...\", \"{deceased.Name.ToStringShort} was...\").\n" +
                                  $"Make it fit their opinion and relationship. If they disliked the deceased, make it cold or formal. If they loved them, make it emotional.\n\n" +
                                  $"Response must be strictly in JSON format:\n" +
                                  $"{{\n" +
                                  $"  \"eulogies\": {{\n" +
                                  $"    \"PawnID_1\": \"Eulogy statement...\",\n" +
                                  $"    \"PawnID_2\": \"Eulogy statement...\"\n" +
                                  $"  }}\n" +
                                  $"}}";

            string attendeesList = "";
            foreach (var p in attendees)
            {
                string rel = p.relations?.OpinionOf(deceased).ToString() ?? "0";
                string traits = string.Join(", ", p.story?.traits?.allTraits?.Select(t => t.Label) ?? Enumerable.Empty<string>());
                string relationLabel = "Guest";
                if (p.relations != null && p.relations.DirectRelations != null)
                {
                    var relInfo = p.relations.DirectRelations.FirstOrDefault(r => r.otherPawn == deceased);
                    if (relInfo != null && relInfo.def != null)
                    {
                        relationLabel = relInfo.def.label;
                    }
                }
                attendeesList += $"- {p.Name.ToStringShort} (ID: {p.ThingID}): Traits: {traits}, Opinion: {rel}, Relation: {relationLabel}\n";
            }

            string userMessage = $"Deceased: {deceased.Name.ToStringShort} (ID: {deceased.ThingID})\n" +
                                 $"Attendees:\n{attendeesList}";

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result =>
                {
                    if (result.success && !string.IsNullOrEmpty(result.content))
                    {
                        try
                        {
                            string json = RimSynapse.Utils.JsonHelper.ExtractJson(result.content);
                            if (json != null)
                            {
                                var response = JsonConvert.DeserializeObject<PreGenEulogyResponse>(json);
                                if (response != null && response.eulogies != null)
                                {
                                    SynapseGameComponent.Enqueue(() =>
                                    {
                                        foreach (var kvp in response.eulogies)
                                        {
                                            state.preGeneratedEulogies[kvp.Key] = kvp.Value;
                                        }
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            RimSynapse.SynapseLogger.Warn("psychology", $"Failed to parse pre-generated eulogies: {ex.Message}");
                        }
                    }
                },
                new ChatOptions { priority = 4, requestName = "Pre-Generate Eulogies" }
            );
        }

        public static void Postfix(LordJob_Ritual jobRitual, Dictionary<Pawn, int> totalPresence)
        {
            if (jobRitual == null || jobRitual.assignments == null) return;
            var speaker = jobRitual.assignments.FirstAssignedPawn("speaker");
            
            Pawn deceased = null;
            try 
            {
                var obligation = Traverse.Create(jobRitual).Field("obligation").GetValue();
                if (obligation != null) 
                {
                    var targetInfo = Traverse.Create(obligation).Field("targetA").GetValue();
                    if (targetInfo != null)
                    {
                        var targetThing = Traverse.Create(targetInfo).Property("Thing").GetValue<Thing>();
                        deceased = targetThing as Pawn;
                        if (deceased == null && targetThing is Corpse corpse) deceased = corpse.InnerPawn;
                    }
                }
            } 
            catch {}

            if (deceased == null && speaker != null && speaker.Map != null)
            {
                foreach (var p in speaker.Map.mapPawns.AllPawns)
                {
                    if (p.Dead && p.Corpse != null && p.Corpse.Position.DistanceTo(speaker.Position) < 20f)
                    {
                        deceased = p;
                        break;
                    }
                }
            }

            if (deceased == null) return;

            FuneralStartState startState = null;
            if (s_StartStates.TryGetValue(jobRitual, out var captured))
            {
                startState = captured;
                s_StartStates.Remove(jobRitual);
            }

            var attendees = new HashSet<Pawn>();
            if (totalPresence != null)
            {
                foreach (var p in totalPresence.Keys)
                {
                    if (p != null && p.RaceProps.Humanlike && !p.Dead && p.Spawned) attendees.Add(p);
                }
            }
            if (startState != null)
            {
                foreach (var p in startState.attendees)
                {
                    if (p != null && p.RaceProps.Humanlike && !p.Dead && p.Spawned) attendees.Add(p);
                }
            }
            if (jobRitual.lord?.ownedPawns != null)
            {
                foreach (var p in jobRitual.lord.ownedPawns)
                {
                    if (p != null && p.RaceProps.Humanlike && !p.Dead && p.Spawned) attendees.Add(p);
                }
            }

            if (attendees.Count == 0) return;

            string outcomeLabel = "boring";
            float memoryWeight = 0.4f;
            float memoryDecay = 0.02f;
            var outcomeEffect = jobRitual.Ritual?.outcomeEffect;
            if (outcomeEffect != null && outcomeEffect.def != null && outcomeEffect.def.outcomeChances != null)
            {
                foreach (var poss in outcomeEffect.def.outcomeChances)
                {
                    if (poss.memory != null)
                    {
                        foreach (var attendee in attendees)
                        {
                            if (attendee.needs?.mood?.thoughts?.memories?.NumMemoriesOfDef(poss.memory) > 0)
                            {
                                outcomeLabel = poss.label;
                                if (outcomeLabel == "terrible") memoryWeight = 0.6f;
                                else if (outcomeLabel == "boring") memoryWeight = 0.2f;
                                else if (outcomeLabel == "good") memoryWeight = 0.4f;
                                else if (outcomeLabel == "heartwarming") memoryWeight = 0.7f;
                                break;
                            }
                        }
                    }
                }
            }

            string memory = null;
            if (speaker != null)
            {
                if (startState != null && startState.preGeneratedEulogies.TryGetValue(speaker.ThingID, out string preGen))
                {
                    memory = preGen;
                }
                else
                {
                    var comp = speaker.GetComp<SynapsePawnComp>();
                    if (comp != null && comp.socialNetwork.TryGetValue(deceased.GetUniqueLoadID(), out var record))
                    {
                        if (record.relationshipMemories.Count > 0)
                        {
                            memory = record.relationshipMemories.RandomElement();
                        }
                    }
                }
            }

            if (speaker != null && !string.IsNullOrEmpty(memory))
            {
                if (outcomeLabel == "terrible" || outcomeLabel == "boring")
                {
                    Find.LetterStack.ReceiveLetter(
                        $"{speaker.Name.ToStringShort}'s Awkward Eulogy",
                        $"During the funeral, {speaker.Name.ToStringShort} stepped forward to share a memory of {deceased.Name.ToStringShort}, but the service was {outcomeLabel} and the eulogy was received poorly:\n\n\"{memory}\"",
                        LetterDefOf.NegativeEvent,
                        speaker
                    );
                }
                else
                {
                    Find.LetterStack.ReceiveLetter(
                        $"{speaker.Name.ToStringShort}'s Eulogy",
                        $"During the funeral, {speaker.Name.ToStringShort} stepped forward and shared a personal memory of {deceased.Name.ToStringShort}:\n\n\"{memory}\"",
                        LetterDefOf.PositiveEvent,
                        speaker
                    );
                }
            }

            GenerateFuneralRecordAndMemories(deceased, outcomeLabel, startState, attendees, memoryWeight, memoryDecay);
        }

        public class FuneralResponse
        {
            public string overallRecord;
            public Dictionary<string, string> pawnMemories;
        }

        private static void GenerateFuneralRecordAndMemories(Pawn deceased, string outcomeLabel, FuneralStartState startState, HashSet<Pawn> attendees, float memoryWeight, float memoryDecay)
        {
            var placeholderTags = new Dictionary<string, string>();
            foreach (var attendee in attendees)
            {
                var coreComp = attendee.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
                if (coreComp != null)
                {
                    string placeholderId = Guid.NewGuid().ToString("N");
                    string placeholderTag = "funeral_placeholder_" + placeholderId;
                    placeholderTags[attendee.ThingID] = placeholderTag;

                    var placeholder = new WeightedMemory
                    {
                        summary = $"Attended the funeral of {deceased.Name.ToStringShort}. The service was {outcomeLabel}.",
                        memoryType = "social",
                        tags = new List<string> { "funeral", deceased.ThingID, placeholderTag },
                        absTick = RimSynapse.Utils.SynapseDateHelper.GameTickToAbsTick(Find.TickManager.TicksGame),
                        gameTick = Find.TickManager.TicksGame,
                        weight = memoryWeight,
                        baseWeight = memoryWeight,
                        decayRate = memoryDecay
                    };
                    coreComp.memories.Add(placeholder);
                }
            }

            string weather = startState?.weather ?? "clear";
            string timeOfDay = startState?.timeOfDay ?? "afternoon";
            string moodReason = startState?.moodReason ?? "grief";
            float averageMood = startState?.averageMood ?? 0.5f;
            float averageOpinion = startState?.averageOpinion ?? 0f;

            string systemPrompt = $"You are role-playing as the narrator and subconscious minds of a group of RimWorld pawns attending the funeral of {deceased.Name.ToStringShort}.\n" +
                                  $"The service occurred during a {weather} {timeOfDay}. The overall mood of attendees was {moodReason} (average: {averageMood:F2}), and their average opinion of the deceased was {averageOpinion:F1}.\n" +
                                  $"The overall outcome of the service was: {outcomeLabel}.\n\n" +
                                  $"You must generate two things:\n" +
                                  $"1. An overall detailed narrative of the funeral service (to be saved at the gravesite). Describe the atmosphere, how the weather and mood played in, and how the attendees reacted to the {outcomeLabel} outcome. Keep it beautiful and thematic.\n" +
                                  $"2. A short first-person journal entry/memory for EACH attendee.\n" +
                                  $"   - Standard memory: 1-2 sentences.\n" +
                                  $"   - Lover / Spouse / Close Friend / Bitter Rival: 3-4 sentences, expressing deeper feelings (relief, profound grief, hidden satisfaction).\n" +
                                  $"   - Write in the first person (\"I felt...\", \"Seeing them...\").\n\n" +
                                  $"Response must be strictly in JSON format:\n" +
                                  $"{{\n" +
                                  $"  \"overallRecord\": \"The detailed narrative of the funeral...\",\n" +
                                  $"  \"pawnMemories\": {{\n" +
                                  $"    \"PawnID_1\": \"Memory text...\",\n" +
                                  $"    \"PawnID_2\": \"Memory text...\"\n" +
                                  $"  }}\n" +
                                  $"}}";

            string attendeesList = "";
            foreach (var p in attendees)
            {
                string rel = p.relations?.OpinionOf(deceased).ToString() ?? "0";
                string traits = string.Join(", ", p.story?.traits?.allTraits?.Select(t => t.Label) ?? Enumerable.Empty<string>());
                string relationLabel = "Guest";
                
                if (p.relations != null && p.relations.DirectRelations != null)
                {
                    var relInfo = p.relations.DirectRelations.FirstOrDefault(r => r.otherPawn == deceased);
                    if (relInfo != null && relInfo.def != null)
                    {
                        relationLabel = relInfo.def.label;
                    }
                }
                
                attendeesList += $"- {p.Name.ToStringShort} (ID: {p.ThingID}): Traits: {traits}, Opinion: {rel}, Relation: {relationLabel}\n";
            }

            string userMessage = $"Deceased: {deceased.Name.ToStringShort} (ID: {deceased.ThingID})\n" +
                                 $"Attendees:\n{attendeesList}";

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result =>
                {
                    if (result.success && !string.IsNullOrEmpty(result.content))
                    {
                        try
                        {
                            string json = RimSynapse.Utils.JsonHelper.ExtractJson(result.content);
                            if (json != null)
                            {
                                var response = JsonConvert.DeserializeObject<FuneralResponse>(json);
                                if (response != null)
                                {
                                    SynapseGameComponent.Enqueue(() =>
                                    {
                                        var worldComp = Find.World?.GetComponent<SynapsePsychologyWorldComponent>();
                                        if (worldComp != null)
                                        {
                                            worldComp.funeralRecords[deceased.GetUniqueLoadID()] = response.overallRecord;
                                        }

                                        if (response.pawnMemories != null)
                                        {
                                            foreach (var kvp in response.pawnMemories)
                                            {
                                                string pId = kvp.Key;
                                                string explanation = kvp.Value;
                                                Pawn p = attendees.FirstOrDefault(a => a.ThingID == pId);
                                                if (p != null && placeholderTags.TryGetValue(pId, out string pTag))
                                                {
                                                    var targetComp = p.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
                                                    if (targetComp != null)
                                                    {
                                                        var targetMem = targetComp.memories.FirstOrDefault(m => m.tags.Contains(pTag));
                                                        if (targetMem != null)
                                                        {
                                                            targetMem.summary = explanation;
                                                            targetMem.tags.Remove(pTag);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            RimSynapse.SynapseLogger.Warn("psychology", $"Failed to process funeral record and memories: {ex.Message}");
                        }
                    }
                },
                new ChatOptions { priority = 3, requestName = "Personalized Funeral Memory" }
            );
        }
    }
}
