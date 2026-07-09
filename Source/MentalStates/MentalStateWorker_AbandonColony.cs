using Verse;
using Verse.AI;

namespace RimSynapse.Psychology.MentalStates
{
    public class MentalStateWorker_AbandonColony : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            if (!pawn.IsColonist || pawn.Downed)
            {
                return false;
            }
            
            // Only triggers organically via the LLM psychology review
            return false;
        }
    }
}
