using System;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;

namespace RimSynapse.Psychology.MentalStates
{
    public class JobGiver_TraumaShoot : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn.MentalState is MentalState_TraumaTrigger traumaState))
                return null;

            if (traumaState.hasFiredShots)
                return null; // Move to the cowering phase

            // Check if they have a ranged weapon
            if (pawn.equipment == null || pawn.equipment.Primary == null || !pawn.equipment.Primary.def.IsRangedWeapon)
            {
                traumaState.hasFiredShots = true;
                return null;
            }

            // Target selection rules:
            // 1. Room door or wall within 8 squares
            // 2. Noisy thing (animal/building/pawn) within 12 squares
            
            Thing target = null;
            
            // 1. Try find wall or door
            var potentialWalls = GenRadial.RadialCellsAround(pawn.Position, 8, true)
                .Where(c => c.InBounds(pawn.Map) && GenSight.LineOfSight(pawn.Position, c, pawn.Map))
                .SelectMany(c => c.GetThingList(pawn.Map))
                .Where(t => t.def.defName == "Wall" || t.def.IsDoor)
                .ToList();

            if (potentialWalls.Count > 0)
            {
                target = potentialWalls.RandomElement();
            }
            else
            {
                // 2. Noisy targets within 12 squares
                var noisyTargets = GenRadial.RadialCellsAround(pawn.Position, 12, true)
                    .Where(c => c.InBounds(pawn.Map) && GenSight.LineOfSight(pawn.Position, c, pawn.Map))
                    .SelectMany(c => c.GetThingList(pawn.Map))
                    .Where(t => (t is Pawn) || (t is Building && t.TryGetComp<CompPowerTrader>() != null))
                    .ToList();

                if (noisyTargets.Count > 0)
                {
                    target = noisyTargets.RandomElement();
                }
            }

            if (target != null)
            {
                LocalTargetInfo shootTarget = target;
                
                // 50% chance to fire wildly at a random cell within 3 squares of the pawn
                if (Rand.Chance(0.5f))
                {
                    var wildCells = GenRadial.RadialCellsAround(pawn.Position, 3, true)
                        .Where(c => c.InBounds(pawn.Map) && GenSight.LineOfSight(pawn.Position, c, pawn.Map))
                        .ToList();
                        
                    if (wildCells.Count > 0)
                    {
                        shootTarget = wildCells.RandomElement();
                    }
                }

                Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, shootTarget);
                job.maxNumStaticAttacks = 1; // Fire one burst/shot
                job.expiryInterval = 600;
                
                traumaState.hasFiredShots = true; 
                return job;
            }

            // If nothing to shoot, just cower.
            traumaState.hasFiredShots = true;
            return null;
        }
    }
}
