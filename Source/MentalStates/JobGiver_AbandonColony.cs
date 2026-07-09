using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using RimSynapse.Psychology.API;

namespace RimSynapse.Psychology.MentalStates
{
    public class JobGiver_AbandonColony : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Map == null) return null;

            // Determine if they hate everyone
            bool hatesEveryone = DoesHateEveryone(pawn);

            if (hatesEveryone)
            {
                // Try to find high value items (Silver, Plasteel, Gold, etc) to steal
                Thing stealTarget = FindValuableItem(pawn);
                if (stealTarget != null && pawn.inventory.innerContainer.TotalStackCount < 5)
                {
                    Job stealJob = JobMaker.MakeJob(JobDefOf.Steal, stealTarget);
                    stealJob.count = stealTarget.stackCount;
                    return stealJob;
                }

                // Try to rope bonded animals
                Pawn animal = FindBondedAnimal(pawn);
                if (animal != null)
                {
                    // RopeToDestination is a 1.3+ feature. We can just use standard rope if available,
                    // but for simplicity, we can have them "rescue" or just claim the animal.
                    // Actually, let's just make the animal follow them by changing its faction to null or giving it a mental state
                    animal.SetFaction(null);
                    animal.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter); // Or just let it go wild
                }
            }
            else
            {
                // Just take 4 meals
                if (pawn.inventory.innerContainer.TotalStackCount < 4)
                {
                    Thing meal = FindMeal(pawn);
                    if (meal != null)
                    {
                        Job takeJob = JobMaker.MakeJob(JobDefOf.TakeInventory, meal);
                        takeJob.count = Math.Min(4, meal.stackCount);
                        return takeJob;
                    }
                }
            }

            // Time to leave. Find map edge.
            IntVec3 exitCell;
            if (!RCellFinder.TryFindBestExitSpot(pawn, out exitCell, TraverseMode.PassDoors))
            {
                return null;
            }

            Job exitJob = JobMaker.MakeJob(JobDefOf.Goto, exitCell);
            exitJob.exitMapOnArrival = true;
            exitJob.locomotionUrgency = LocomotionUrgency.Walk; // Walk slowly to conserve strength
            return exitJob;
        }

        private bool DoesHateEveryone(Pawn pawn)
        {
            if (pawn.relations == null) return false;
            
            var colonists = pawn.Map.mapPawns.FreeColonists.Where(c => c != pawn).ToList();
            if (colonists.Count == 0) return false;

            int totalOpinion = 0;
            foreach (var colonist in colonists)
            {
                totalOpinion += pawn.relations.OpinionOf(colonist);
            }

            float avgOpinion = (float)totalOpinion / colonists.Count;
            return avgOpinion < -20f;
        }

        private Thing FindValuableItem(Pawn pawn)
        {
            return pawn.Map.listerThings.AllThings
                .Where(t => t.def.BaseMarketValue > 5f && !t.IsForbidden(pawn) && pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderByDescending(t => t.def.BaseMarketValue * t.stackCount)
                .FirstOrDefault();
        }

        private Pawn FindBondedAnimal(Pawn pawn)
        {
            if (pawn.relations == null) return null;

            return pawn.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)
                .Where(p => p.RaceProps.Animal && pawn.relations.DirectRelationExists(PawnRelationDefOf.Bond, p) && pawn.CanReach(p, PathEndMode.Touch, Danger.Some))
                .FirstOrDefault();
        }

        private Thing FindMeal(Pawn pawn)
        {
            return pawn.Map.listerThings.AllThings
                .Where(t => t.def.IsNutritionGivingIngestible && t.def.ingestible.preferability >= FoodPreferability.MealSimple && !t.IsForbidden(pawn) && pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .FirstOrDefault();
        }
    }
}
