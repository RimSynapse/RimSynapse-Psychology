using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimSynapse.Psychology.MentalStates
{
    public class JobGiver_Homicidal : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Downed || pawn.Dead) return null;

            // 1. Weapon Procurement
            if (pawn.equipment == null) return null;

            ThingWithComps primary = pawn.equipment.Primary;
            if (primary == null)
            {
                // Unarmed. Try to find a weapon.
                Thing weapon = GenClosest.ClosestThingReachable(
                    pawn.Position,
                    pawn.Map,
                    ThingRequest.ForGroup(ThingRequestGroup.Weapon),
                    PathEndMode.OnCell,
                    TraverseParms.For(pawn),
                    9999f,
                    validator: t => t.def.IsWeapon && !t.IsForbidden(pawn) && pawn.CanReserve(t)
                );

                if (weapon != null)
                {
                    return JobMaker.MakeJob(JobDefOf.Equip, weapon);
                }
            }

            // 2. Target Selection
            bool isViolent = pawn.story != null && pawn.story.traits != null && 
                             (pawn.story.traits.HasTrait(TraitDefOf.Bloodlust) ||
                              pawn.story.traits.HasTrait(TraitDefOf.Psychopath) ||
                              pawn.story.traits.HasTrait(TraitDefOf.Brawler));

            Pawn target = null;
            // Get valid targets: not us, spawned, not downed/dead, and reachable
            var potentialTargets = pawn.Map.mapPawns.FreeColonistsAndPrisonersSpawned
                .Where(p => p != pawn && !p.Downed && !p.Dead && pawn.CanReach(p, PathEndMode.Touch, Danger.Deadly))
                .ToList();

            if (potentialTargets.Count == 0)
            {
                return JobMaker.MakeJob(JobDefOf.Wait_Wander);
            }

            if (isViolent)
            {
                // Find nearest unarmored target (Armor Rating < 40%)
                target = potentialTargets
                    .Where(p => p.GetStatValue(StatDefOf.ArmorRating_Sharp) < 0.4f)
                    .OrderBy(p => pawn.Position.DistanceToSquared(p.Position))
                    .FirstOrDefault();

                // If everyone is armored, just pick the nearest one anyway
                if (target == null)
                {
                    target = potentialTargets.OrderBy(p => pawn.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                }
            }
            else
            {
                // Find most hated target
                target = potentialTargets
                    .OrderBy(p => pawn.relations.OpinionOf(p))
                    .FirstOrDefault();
            }

            if (target == null)
            {
                return JobMaker.MakeJob(JobDefOf.Wait_Wander);
            }

            // 3. Attack Action
            Verb attackVerb = pawn.TryGetAttackVerb(target, !pawn.IsColonist);
            if (attackVerb != null && attackVerb.verbProps.isPrimary && attackVerb.verbProps.range > 1.5f)
            {
                if (pawn.CanSee(target) && pawn.Position.DistanceTo(target.Position) <= attackVerb.verbProps.range)
                {
                    Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                    job.maxNumStaticAttacks = 1;
                    job.expiryInterval = 600;
                    job.canBashDoors = true;
                    return job;
                }
                else
                {
                    // Move towards target until in range
                    Job job = JobMaker.MakeJob(JobDefOf.Goto, target);
                    job.expiryInterval = 120;
                    job.locomotionUrgency = LocomotionUrgency.Sprint;
                    return job;
                }
            }
            else
            {
                // Melee attack
                Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                job.maxNumMeleeAttacks = 1;
                job.expiryInterval = 600;
                job.canBashDoors = true;
                return job;
            }
        }
    }
}
