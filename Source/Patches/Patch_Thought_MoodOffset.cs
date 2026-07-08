using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.Comps;
using RimSynapse.Psychology.Comps;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(Thought), nameof(Thought.MoodOffset))]
    public static class Patch_Thought_MoodOffset
    {
        public static void Postfix(Thought __instance, ref float __result)
        {
            // Ignore if no mood impact
            if (__result == 0f) return;

            if (__instance.pawn == null) return;

            var comp = __instance.pawn.GetComp<SynapseCorePawnComp>();
            if (comp != null && comp.thoughtSensitivities != null)
            {
                // Match by specific thought defName (e.g., "KnowColonistDied")
                if (comp.thoughtSensitivities.TryGetValue(__instance.def.defName, out float multiplier))
                {
                    __result *= multiplier;
                    return;
                }

                // Or match by a broader category if the AI provided it (e.g., "Death", "Social")
                // Here we can map some common categories if needed, but for now we expect the AI
                // or the bridge to map categories to specific tags, or just pass the exact defName.
                // If we want to support category tags, we would check if the thought def name 
                // contains the string, though that's less precise.
                
                // Example wildcard check for broad AI tags:
                foreach (var kvp in comp.thoughtSensitivities)
                {
                    if (__instance.def.defName.Contains(kvp.Key))
                    {
                        __result *= kvp.Value;
                        break;
                    }
                }
            }
        }
    }
}
