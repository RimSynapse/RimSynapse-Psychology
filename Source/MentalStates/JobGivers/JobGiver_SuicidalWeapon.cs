using System;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;

namespace RimSynapse.Psychology.MentalStates
{
    public class JobGiver_SuicidalWeapon : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Map == null) return null;

            // 1. If we don't have a weapon, find one and equip it!
            if (pawn.equipment == null || pawn.equipment.Primary == null)
            {
                // Find closest weapon on the map (ignoring player forbidden setting!)
                float closestDist = float.MaxValue;
                Thing closestWeapon = null;
                foreach (var t in pawn.Map.listerThings.AllThings)
                {
                    if (t.def.IsWeapon && t.ParentHolder is not Pawn && pawn.CanReach(t, PathEndMode.Touch, Danger.Deadly))
                    {
                        float dist = pawn.Position.DistanceToSquared(t.Position);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestWeapon = t;
                        }
                    }
                }

                if (closestWeapon != null)
                {
                    // Create an equip job. Since this is an AI-giver job, 
                    // it bypasses the player's direct interaction forbidden flag naturally!
                    return JobMaker.MakeJob(JobDefOf.Equip, closestWeapon);
                }

                // If no weapon is available, fall back to wandering aimlessly
                return JobMaker.MakeJob(JobDefOf.Wait_Wander);
            }

            // 2. If we do have a weapon, trigger the custom suicide self-harm job!
            JobDef selfHarmDef = DefDatabase<JobDef>.GetNamed("Synapse_SuicideSelfHarm", errorOnFail: false);
            if (selfHarmDef != null)
            {
                return JobMaker.MakeJob(selfHarmDef, pawn.equipment.Primary);
            }

            return JobMaker.MakeJob(JobDefOf.Wait_Wander);
        }
    }
}
