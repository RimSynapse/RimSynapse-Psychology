using Verse;
using Verse.AI;
using RimWorld;

namespace RimSynapse.Psychology.MentalStates
{
    public class JobGiver_SuicidalStarve : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            IntVec3 wanderDest = RCellFinder.RandomWanderDestFor(pawn, pawn.Position, 10f, null, Danger.Deadly);
            if (wanderDest.IsValid)
            {
                return JobMaker.MakeJob(JobDefOf.GotoWander, wanderDest);
            }
            
            return JobMaker.MakeJob(JobDefOf.Wait_Wander);
        }
    }
}
