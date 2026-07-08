using Verse;
using Verse.AI;

namespace RimSynapse.Psychology.MentalStates
{
    public class MentalState_SuicidalAntagonize : MentalState
    {
        public override void PostStart(string reason)
        {
            base.PostStart(reason);
            // Drop all weapons and apparel immediately upon snapping
            if (pawn.apparel != null)
            {
                pawn.apparel.DropAll(pawn.Position, forbid: false);
            }
            if (pawn.equipment != null)
            {
                pawn.equipment.DropAllEquipment(pawn.Position, forbid: false);
            }
        }
    }
}
