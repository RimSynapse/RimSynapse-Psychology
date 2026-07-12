using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.Psychology.Comps;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(Pawn_RelationsTracker), "OpinionOf")]
    public static class Patch_Pawn_RelationsTracker_OpinionOf
    {
        public static void Postfix(Pawn_RelationsTracker __instance, Pawn other, ref int __result, Pawn ___pawn)
        {
            if (___pawn == null || other == null) return;
            
            var comp = ___pawn.GetComp<SynapsePawnComp>();
            if (comp != null && comp.socialNetwork != null)
            {
                string otherId = other.GetUniqueLoadID();
                if (comp.socialNetwork.TryGetValue(otherId, out var record) && record != null)
                {
                    // Scale vanilla down by 50%
                    float vanilla = __result * 0.5f;
                    // Add 50% of our trust
                    float trustFactor = record.trust * 0.5f;
                    
                    __result = UnityEngine.Mathf.RoundToInt(vanilla + trustFactor);
                    __result = UnityEngine.Mathf.Clamp(__result, -100, 100);
                }
            }
        }
    }
}
