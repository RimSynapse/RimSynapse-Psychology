using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    public static class Patch_JobGiver_GetFood
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn.MentalStateDef?.defName == "Synapse_SuicidalStarve")
            {
                // The pawn refuses to eat
                __result = null;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(JobGiver_PackFood), "TryGiveJob")]
    public static class Patch_JobGiver_PackFood
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn.MentalStateDef?.defName == "Synapse_SuicidalStarve")
            {
                // The pawn refuses to pack food
                __result = null;
                return false;
            }
            return true;
        }
    }
}
