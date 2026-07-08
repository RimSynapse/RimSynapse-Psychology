using Verse;
using RimSynapse.Psychology.Comps;

namespace RimSynapse.Psychology.MentalStates
{
    public class MentalStateWorker_Suicidal : Verse.AI.MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            if (!base.StateCanOccur(pawn)) return false;

            var comp = pawn.GetComp<SynapsePawnComp>();
            if (comp == null) return false;

            // Only allow this break if the AI specifically categorized them as Suicidal
            return comp.breakCategory == BreakCategory.Suicidal;
        }
    }
}
