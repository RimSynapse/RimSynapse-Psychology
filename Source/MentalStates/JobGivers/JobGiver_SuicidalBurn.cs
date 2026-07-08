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
            Room room = pawn.ownership?.OwnedRoom;
            
            if (room == null || room.TouchesMapEdge)
            {
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

            if (room != null)
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
                    
                    return JobMaker.MakeJob(JobDefOf.Wait_Wander);
                }
            }

            return JobMaker.MakeJob(JobDefOf.Wait_Wander);
        }
    }
}
