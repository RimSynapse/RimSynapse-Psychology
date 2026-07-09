using Verse;
using RimWorld;
using RimSynapse.Psychology.Comps;

namespace RimSynapse.Psychology.MentalStates
{
    public class MentalStateWorker_Suicidal : Verse.AI.MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            // Disabled from naturally occurring gameplay as requested by user.
            // These traits and breaks are currently limited to Dev Mode triggers.
            return false;
        }
    }
}
