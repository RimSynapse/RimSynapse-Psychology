using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using RimSynapse.Psychology.Comps;
using System.Reflection;

namespace RimSynapse.Psychology.Patches
{
    // MentalBreaker.BreakThresholdExtreme getter
    [HarmonyPatch(typeof(MentalBreaker), "BreakThresholdExtreme", MethodType.Getter)]
    public static class Patch_BreakThresholdExtreme
    {
        public static void Postfix(MentalBreaker __instance, ref float __result)
        {
            ApplyThresholdModifier(__instance, ref __result);
        }
        
        private static void ApplyThresholdModifier(MentalBreaker breaker, ref float result)
        {
            // Traverse to get the private/internal 'pawn' field
            var pawn = Traverse.Create(breaker).Field("pawn").GetValue<Pawn>();
            if (pawn == null) return;

            var comp = pawn.GetComp<SynapsePawnComp>();
            if (comp != null)
            {
                // Ideology zealots snap earlier, effectively increasing their threshold
                if (comp.ideologyZealot)
                {
                    result += 0.05f; // Snap at 5% higher mood
                }

                // Can add more modifiers based on BreakCategory here
            }
        }
    }

    [HarmonyPatch(typeof(MentalBreaker), "BreakThresholdMajor", MethodType.Getter)]
    public static class Patch_BreakThresholdMajor
    {
        public static void Postfix(MentalBreaker __instance, ref float __result)
        {
            var pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null) return;

            var comp = pawn.GetComp<SynapsePawnComp>();
            if (comp != null && comp.ideologyZealot)
            {
                __result += 0.05f; 
            }
        }
    }

    [HarmonyPatch(typeof(MentalBreaker), "BreakThresholdMinor", MethodType.Getter)]
    public static class Patch_BreakThresholdMinor
    {
        public static void Postfix(MentalBreaker __instance, ref float __result)
        {
            var pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null) return;

            var comp = pawn.GetComp<SynapsePawnComp>();
            if (comp != null && comp.ideologyZealot)
            {
                __result += 0.05f; 
            }
        }
    }
}
