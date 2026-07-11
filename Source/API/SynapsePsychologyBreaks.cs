using System;
using System.Linq;
using System.Collections.Generic;
using Verse;
using RimWorld;
using RimSynapse.Psychology.Comps;
using RimSynapse.Models;

namespace RimSynapse.Psychology.API
{
    /// <summary>
    /// Mental break management: Prediction, break profiles, and AI-driven trait directives.
    /// </summary>
    public static partial class SynapsePsychology
    {
        /// <summary>
        /// Updates the pawn's mental break category and zealotry flags.
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
                RimSynapse.SynapseLogger.Warn("psychology", $"[RimSynapse-Psychology] Trait {traitDefName} not found.");
                return;
            }

            bool changed = false;
            var comp = pawn.TryGetComp<SynapsePawnComp>();

            if (add && !pawn.story.traits.HasTrait(traitDef))
            {
                pawn.story.traits.GainTrait(new Trait(traitDef));
                if (comp != null)
                {
                    comp.dynamicTraits.Add(new RimSynapse.Psychology.Models.DynamicTraitRecord(traitDef, Find.TickManager.TicksGame, aiReasoning));
                }
                changed = true;
            }
            else if (!add && pawn.story.traits.HasTrait(traitDef))
            {
                var trait = pawn.story.traits.GetTrait(traitDef);
                pawn.story.traits.allTraits.Remove(trait);
                if (comp != null)
                {
                    comp.dynamicTraits.RemoveAll(x => x.traitDef == traitDef);
                }
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
        /// a pawn will suffer if they snap. Displays a warning threat to the player.
        /// </summary>
        public static void PredictMentalBreak(Pawn pawn, string breakDefName, string warningText)
        {
            var comp = pawn.TryGetComp<SynapsePawnComp>();
            if (comp == null) return;

            MentalStateDef breakDef = DefDatabase<MentalStateDef>.GetNamedSilentFail(breakDefName);
            if (breakDef == null)
            {
                RimSynapse.SynapseLogger.Warn("psychology", $"[RimSynapse-Psychology] Predicted break {breakDefName} not found.");
                return;
            }

            comp.predictedBreakState = breakDef;
            comp.currentBreakWarning = warningText;

            string title = $"Break Risk: {pawn.Name.ToStringShort}";
            Find.LetterStack.ReceiveLetter(title, warningText, LetterDefOf.ThreatSmall, pawn);
        }
    }
}


