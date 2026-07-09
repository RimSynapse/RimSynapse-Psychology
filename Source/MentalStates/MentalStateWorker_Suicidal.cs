using Verse;
using RimWorld;
using RimSynapse.Psychology.Comps;

namespace RimSynapse.Psychology.MentalStates
{
    public class MentalStateWorker_Suicidal : Verse.AI.MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            if (!RimSynapsePsychologyMod.Settings.enableSuicidalBehaviors) return false;
            
            if (!base.StateCanOccur(pawn)) return false;

            var comp = pawn.GetComp<SynapsePawnComp>();
            if (comp == null) return false;

            // Only allow this break if the AI specifically categorized them as Suicidal
            if (comp.breakCategory != BreakCategory.Suicidal) return false;
            
            // To ensure extreme rarity, we enforce a strict threshold here.
            // Even if the LLM thinks they are suicidal, they must be truly broken.
            bool hasAmplifier = pawn.story?.traits?.HasTrait(DefDatabase<TraitDef>.GetNamedSilentFail("Depressive")) == true ||
                                pawn.story?.traits?.HasTrait(DefDatabase<TraitDef>.GetNamedSilentFail("Volatile")) == true ||
                                pawn.story?.traits?.HasTrait(DefDatabase<TraitDef>.GetNamedSilentFail("Pessimist")) == true ||
                                pawn.story?.traits?.HasTrait(DefDatabase<TraitDef>.GetNamedSilentFail("Bloodlust")) == true ||
                                pawn.story?.traits?.HasTrait(DefDatabase<TraitDef>.GetNamedSilentFail("Psychopath")) == true;

            // Their mood must be absolutely tanked (less than 5%) unless they have an amplifying trait (less than 15%)
            float mood = pawn.needs?.mood?.CurLevelPercentage ?? 1f;
            
            if (hasAmplifier)
            {
                return mood <= 0.15f;
            }
            else
            {
                return mood <= 0.05f;
            }
        }
    }
}
