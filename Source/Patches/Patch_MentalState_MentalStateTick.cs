using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;
using RimSynapse.Psychology.Comps;
using RimSynapse.Psychology.MentalStates;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(MentalState), "MentalStateTick")]
    public static class Patch_MentalState_MentalStateTick
    {
        public static void Postfix(MentalState __instance)
        {
            if (__instance.pawn == null) return;
            
            // Check dynamically every 150 ticks to avoid performance hits
            if (!__instance.pawn.IsHashIntervalTick(150)) return;

            var comp = __instance.pawn.GetComp<SynapsePawnComp>();
            if (comp == null || comp.breakIntensity != BreakIntensity.Light) return;

            // Handle Light intensity abort conditions
            if (__instance.def.defName == "Synapse_SuicidalBurn")
            {
                if (__instance.pawn.HasAttachment(ThingDefOf.Fire) || __instance.pawn.AmbientTemperature > 50f)
                {
                    __instance.RecoverFromState();
                }
            }
            else if (__instance.def.defName == "Synapse_SuicidalAntagonize")
            {
                if (__instance.pawn.health.summaryHealth.SummaryHealthPercent < 0.5f)
                {
                    __instance.RecoverFromState();
                }
            }
        }
    }
}
