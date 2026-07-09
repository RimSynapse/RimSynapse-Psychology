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
    /// Query utilities: Opinion integrals, relationship trajectories, backstory status, sensitivities.
    /// </summary>
    public static partial class SynapsePsychology
    {
        /// <summary>
        /// Gets the opinion integral (moving average) for a pawn→target relationship.
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
        /// Gets the relationship trajectory (current opinion minus integral).
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
        /// Updates the thought sensitivities based on AI evaluation.
        /// </summary>
        public static void UpdateSensitivities(Pawn pawn, Dictionary<string, float> newSensitivities)
        {
            var comp = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (comp != null)
            {
                comp.thoughtSensitivities = newSensitivities;
            }
        }
    }
}
