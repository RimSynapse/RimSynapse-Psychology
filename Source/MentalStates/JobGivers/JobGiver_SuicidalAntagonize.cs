using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;

namespace RimSynapse.Psychology.MentalStates
{
    public class JobGiver_SuicidalAntagonize : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var animal = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction == null && p.RaceProps.Animal && p.RaceProps.manhunterOnDamageChance > 0)
                .OrderBy(p => pawn.Position.DistanceToSquared(p.Position))
                .FirstOrDefault();

            if (animal != null)
            {
                Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, animal);
                job.maxNumMeleeAttacks = 999;
                return job;
            }

            return JobMaker.MakeJob(JobDefOf.Wait_Wander);
        }
    }
}
