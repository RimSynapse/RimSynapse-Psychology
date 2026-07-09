using Verse;
using RimWorld;

namespace RimSynapse.Psychology.MentalStates
{
    public class MentalStateWorker_Homicidal : Verse.AI.MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            if (!base.StateCanOccur(pawn))
            {
                return false;
            }
            if (!pawn.Spawned || pawn.Downed)
            {
                return false;
            }

            // Homicidal should be rare and extreme.
            // Check if they have a violent trait.
            bool isViolent = pawn.story != null && pawn.story.traits != null && 
                             (pawn.story.traits.HasTrait(TraitDefOf.Bloodlust) ||
                              pawn.story.traits.HasTrait(TraitDefOf.Psychopath) ||
                              pawn.story.traits.HasTrait(TraitDefOf.Brawler));

            float moodThreshold = isViolent ? 0.15f : 0.05f; // Violent pawns snap easier
            
            if (pawn.needs != null && pawn.needs.mood != null)
            {
                if (pawn.needs.mood.CurLevel > moodThreshold)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
