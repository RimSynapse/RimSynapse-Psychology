using Verse;
using Verse.AI;
using RimWorld;

namespace RimSynapse.Psychology.MentalStates
{
    public class JobGiver_TraumaCower : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn.MentalState is MentalState_TraumaTrigger traumaState))
                return null;

            if (!traumaState.hasFiredShots)
                return null; // Wait for shooting phase to finish

            // Return a wait job for 1 hour (2500 ticks)
            Job job = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            job.expiryInterval = 2500;
            job.overrideDisplayString = "cowering in fear";
            return job;
        }
    }
}
