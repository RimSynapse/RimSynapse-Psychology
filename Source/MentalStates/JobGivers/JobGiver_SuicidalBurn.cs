using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;

namespace RimSynapse.Psychology.MentalStates
{
    public class JobGiver_SuicidalBurn : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            // 1. Try to find the closest reachable fire
            var fires = pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Fire)
                .Where(f => pawn.CanReach(f, PathEndMode.Touch, Danger.Deadly))
                .ToList();

            if (fires.Count > 0)
            {
                Thing closestFire = fires.OrderBy(f => pawn.Position.DistanceToSquared(f.Position)).FirstOrDefault();
                if (closestFire != null)
                {
                    if (pawn.Position != closestFire.Position)
                    {
                        // Run to the fire
                        Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, closestFire.Position);
                        gotoJob.locomotionUrgency = LocomotionUrgency.Sprint;
                        return gotoJob;
                    }
                    else
                    {
                        // Stand in the fire!
                        return JobMaker.MakeJob(JobDefOf.Wait_Wander);
                    }
                }
            }

            // 2. No fire? Find something combustible to ignite
            Thing combustible = pawn.Map.listerThings.AllThings
                .Where(t => t.FlammableNow && pawn.CanReach(t, PathEndMode.Touch, Danger.Deadly))
                .OrderBy(t => pawn.Position.DistanceToSquared(t.Position))
                .FirstOrDefault();

            if (combustible != null)
            {
                Job igniteJob = JobMaker.MakeJob(JobDefOf.Ignite, combustible);
                return igniteJob;
            }

            // 3. Fallback
            return JobMaker.MakeJob(JobDefOf.Wait_Wander);
        }
    }
}
