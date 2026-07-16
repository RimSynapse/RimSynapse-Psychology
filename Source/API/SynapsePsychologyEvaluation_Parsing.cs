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
    /// Response parsing and callback handlers for clinical evaluation results.
    /// Handles social adjustments, abandonment risk, trait changes, and therapy summaries.
    /// </summary>
    public static partial class SynapsePsychology
    {
        private static void ParseEvaluationResult(ChatResult result, Pawn pawn, SynapsePawnComp pawnComp, Action<bool> onComplete, System.Diagnostics.Stopwatch sw)
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

                    var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    if (parsed != null)
                    {
                        foreach (var kvp in parsed)
                        {
                            if (kvp.Key == "SocialAdjustments" && kvp.Value is Newtonsoft.Json.Linq.JObject socialObj)
                            {
                                ApplySocialAdjustments(pawn, pawnComp, socialObj);
                            }
                            else if (kvp.Key == "AbandonmentRiskScore")
                            {
                                ApplyAbandonmentRisk(pawn, Convert.ToInt32(kvp.Value));
                            }
                            else if (kvp.Key == "TraitChanges" && kvp.Value is Newtonsoft.Json.Linq.JObject traitObj)
                            {
                                ApplyTraitChanges(pawn, traitObj);
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
        }

        private static void ApplySocialAdjustments(Pawn pawn, SynapsePawnComp pawnComp, Newtonsoft.Json.Linq.JObject socialObj)
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

        private static void ApplyAbandonmentRisk(Pawn pawn, int riskScore)
        {
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

        private static void ApplyTraitChanges(Pawn pawn, Newtonsoft.Json.Linq.JObject traitObj)
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
