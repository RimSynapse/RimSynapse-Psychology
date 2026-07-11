using Verse;
using HarmonyLib;
using RimWorld;
using RimSynapse.Psychology.MentalStates;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(DamageWorker), "Apply")]
    public static class Patch_DamageWorker_Apply
    {
        public static void Postfix(DamageWorker __instance, DamageInfo dinfo, Thing victim, DamageWorker.DamageResult __result)
        {
            if (dinfo.Instigator is Pawn instigator && instigator.MentalState is MentalState_TraumaTrigger)
            {
                // If they hit an animal or pawn and caused damage
                if (__result.totalDamageDealt > 0 && (victim is Pawn))
                {
                    // Snap out of the trauma state to engage in normal combat behavior
                    instigator.MentalState.RecoverFromState();
                    
                    // Optionally notify player
                    Messages.Message($"{instigator.NameShortColored} snapped out of their trauma after hitting a living target!", instigator, MessageTypeDefOf.NeutralEvent, false);
                }
            }
        }
    }
}
