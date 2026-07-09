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
            // 1. If there is already a fire nearby, run into it!
            var fires = pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Fire)
                .Where(f => f.Position.DistanceTo(pawn.Position) < 30f && pawn.CanReach(f, PathEndMode.Touch, Danger.Deadly))
                .ToList();
                
            if (fires.Count > 0)
            {
                Thing biggestFire = fires.OrderByDescending(f => ((Fire)f).fireSize).FirstOrDefault();
                if (biggestFire != null)
                {
                    if (pawn.Position != biggestFire.Position)
                    {
                        Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, biggestFire.Position);
                        gotoJob.locomotionUrgency = LocomotionUrgency.Sprint;
                        return gotoJob;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            Room room = pawn.ownership?.OwnedRoom;
            
            if (room == null || room.TouchesMapEdge)
            {
                // Find a cluster of flammable outdoor things (like trees/grass)
                for (int i = 0; i < 50; i++)
                {
                    IntVec3 c = CellFinder.RandomCell(pawn.Map);
                    Room r = c.GetRoom(pawn.Map);
                    if (r != null && !r.TouchesMapEdge && !r.IsHuge && r.CellCount < 50)
                    {
                        room = r;
                        break;
                    }
                }
            }

            if (room != null && !room.TouchesMapEdge)
            {
                if (!room.ContainsCell(pawn.Position))
                {
                    return JobMaker.MakeJob(JobDefOf.Goto, room.Cells.RandomElement());
                }
                else
                {
                    var flammable = room.ContainedThings(ThingDefOf.WoodLog).FirstOrDefault() 
                                    ?? room.ContainedAndAdjacentThings.FirstOrDefault(t => t.FlammableNow);
                    
                    if (flammable != null)
                    {
                        return JobMaker.MakeJob(JobDefOf.Ignite, flammable);
                    }
                }
            }
            else
            {
                // No room found, just find the nearest flammable thing outside and ignite it
                Thing flammableThing = GenClosest.ClosestThingReachable(
                    pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.HasGUIOverlay),
                    PathEndMode.Touch, TraverseParms.For(pawn), 9999f,
                    t => t.FlammableNow && !t.IsBurning());

                // Or find a plant
                if (flammableThing == null)
                {
                    flammableThing = GenClosest.ClosestThingReachable(
                        pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Plant),
                        PathEndMode.Touch, TraverseParms.For(pawn), 9999f,
                        t => t.FlammableNow && !t.IsBurning());
                }

                if (flammableThing != null)
                {
                    return JobMaker.MakeJob(JobDefOf.Ignite, flammableThing);
                }
            }

            return null;
        }
    }
}
