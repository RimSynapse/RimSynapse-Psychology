using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.Psychology.Comps;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "StartSocialFight")]
    public static class Patch_Pawn_InteractionsTracker_StartSocialFight
    {
        public static bool Prefix(Pawn_InteractionsTracker __instance, Pawn otherPawn, Pawn ___pawn)
        {
            if (___pawn == null || otherPawn == null) return true;

            var recComp = ___pawn.GetComp<SynapsePawnComp>();
            if (recComp != null && recComp.socialNetwork != null)
            {
                string initId = otherPawn.GetUniqueLoadID();
                if (recComp.socialNetwork.TryGetValue(initId, out var record))
                {
                    if (record.trust > 20f)
                    {
                        // 70% chance to reduce likelihood of retaliating (suppress social fight)
                        if (Rand.Chance(0.70f))
                        {
                            Log.Message($"[RimSynapse] Insult shield triggered: {___pawn.LabelShort} suppressed social fight retaliation against {otherPawn.LabelShort} due to high trust ({record.trust:F1}).");
                            return false; // Skip original StartSocialFight method
                        }
                    }
                }
            }

            return true; // Execute original method
        }
    }
}
