using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(MentalBreaker), "TryDoRandomMoodCausedMentalBreak")]
    public static class Patch_MentalBreaker_TryDoRandomMoodCausedMentalBreak
    {
        public static bool Prefix(MentalBreaker __instance, ref bool __result)
        {
            // If our system is enabled, completely block vanilla random breaks
            if (RimSynapse.Psychology.Managers.SynapseBreakManager.Enabled)
            {
                __result = false;
                return false; // Skip original method
            }

            return true; // Run original method
        }
    }
}
