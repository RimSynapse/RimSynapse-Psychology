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
        public static void QueueDailyPsychologyReview(Pawn pawn, float averageMood, System.Collections.Generic.List<RimSynapse.Models.WeightedMemory> dailyEvents)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Connects to RimSynapse-Core LLM Queue
            // For now, this is a placeholder. 

            sw.Stop();
            RimSynapse.Utils.SynapseFileLogger.LogMetric("Psychology", pawn, "QueueDailyPsychologyReview_Stub", sw.ElapsedMilliseconds);
        }
    }
}
