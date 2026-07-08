using HarmonyLib;
using Verse;
using Verse.AI;
using RimSynapse.Psychology.Comps;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(MentalState), "PostStart")]
    public static class Patch_MentalState_PostStart
    {
        public static void Postfix(MentalState __instance)
        {
            if (__instance.pawn == null) return;

            var comp = __instance.pawn.GetComp<SynapsePawnComp>();
            if (comp != null)
            {
                // Convert hours to ticks (1 hour = 2500 ticks)
                int baseTicks = (int)(comp.breakDurationHours * 2500f);
                
                // Add random variance: 1 to 6 hours
                int varianceTicks = Rand.RangeInclusive(1, 6) * 2500;
                
                int totalTicks = baseTicks + varianceTicks;

                // Override the vanilla recovery timer with the AI-driven one
                __instance.forceRecoverAfterTicks = totalTicks;
            }
        }
    }
}
