using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.Psychology.Comps;
using RimSynapse.Psychology.UI;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(Building_Grave), "GetGizmos")]
    public static class Patch_Building_Grave_GetGizmos
    {
        public static void Postfix(Building_Grave __instance, ref IEnumerable<Gizmo> __result)
        {
            var list = new List<Gizmo>(__result);
            if (__instance.Corpse != null && __instance.Corpse.InnerPawn != null)
            {
                AddViewFuneralGizmo(__instance.Corpse.InnerPawn, list);
            }
            __result = list;
        }

        public static void AddViewFuneralGizmo(Pawn deceased, List<Gizmo> gizmos)
        {
            if (deceased == null || Find.World == null) return;
            
            var worldComp = Find.World.GetComponent<SynapsePsychologyWorldComponent>();
            if (worldComp != null && worldComp.funeralRecords.TryGetValue(deceased.GetUniqueLoadID(), out string record))
            {
                gizmos.Add(new Command_Action
                {
                    defaultLabel = "View Funeral Record",
                    defaultDesc = "Read the detailed personal log of this colonist's funeral service.",
                    icon = TexCommand.GatherSpotActive,
                    action = () =>
                    {
                        Find.WindowStack.Add(new Dialog_ViewFuneralRecord(deceased.Name.ToStringFull, record));
                    }
                });
            }
        }
    }

    [HarmonyPatch(typeof(Corpse), "GetGizmos")]
    public static class Patch_Corpse_GetGizmos
    {
        public static void Postfix(Corpse __instance, ref IEnumerable<Gizmo> __result)
        {
            var list = new List<Gizmo>(__result);
            if (__instance.InnerPawn != null)
            {
                Patch_Building_Grave_GetGizmos.AddViewFuneralGizmo(__instance.InnerPawn, list);
            }
            __result = list;
        }
    }
}
