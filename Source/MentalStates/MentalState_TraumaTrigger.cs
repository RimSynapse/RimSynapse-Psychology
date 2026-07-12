using Verse;
using Verse.AI;
using RimWorld;

namespace RimSynapse.Psychology.MentalStates
{
    public class MentalState_TraumaTrigger : Verse.AI.MentalState
    {
        public bool hasFiredShots = false;
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hasFiredShots, "hasFiredShots", false);
        }
        
        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);
            
            // Check if they dropped their weapon or it got destroyed
            if (!hasFiredShots && (pawn.equipment == null || pawn.equipment.Primary == null || !pawn.equipment.Primary.def.IsRangedWeapon))
            {
                // If they have no gun, they just cower immediately
                hasFiredShots = true;
            }
        }
    }
}
