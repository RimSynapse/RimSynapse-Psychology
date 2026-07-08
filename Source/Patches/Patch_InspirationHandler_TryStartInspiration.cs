using HarmonyLib;
using RimWorld;
using Verse;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(InspirationHandler), "TryStartInspiration")]
    public static class Patch_InspirationHandler_TryStartInspiration
    {
        public static bool Prefix(InspirationHandler __instance, ref bool __result)
        {
            // If our system is enabled, completely block vanilla random inspirations
            if (RimSynapse.Psychology.Managers.SynapseBreakManager.Enabled)
            {
                __result = false;
                return false; // Skip original method
            }

            return true; // Run original method
        }
    }
}
