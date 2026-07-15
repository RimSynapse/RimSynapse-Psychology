using System;
using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.Models;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(Pawn_RoyaltyTracker), "OnPostTitleChanged")]
    public static class Patch_Pawn_RoyaltyTracker_OnPostTitleChanged
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_RoyaltyTracker __instance, Faction faction, RoyalTitleDef prevTitle, RoyalTitleDef newTitle)
        {
            if (!ModsConfig.RoyaltyActive) return;
            if (newTitle == null) return;
            Pawn pawn = __instance.pawn;
            if (pawn == null || !pawn.IsColonist) return;

            var coreComp = Find.World?.GetComponent<RimSynapse.SynapseCoreWorldComponent>();
            if (coreComp != null)
            {
                coreComp.EnqueuePastEvent(new PastEvent
                {
                    gameTick = Find.TickManager.TicksGame,
                    eventDescription = $"{pawn.Name.ToStringShort} was awarded the noble title of {newTitle.label} by Faction {faction?.Name ?? "Unknown"}.",
                    pawnSnapshots = new System.Collections.Generic.Dictionary<string, string>(),
                    category = "Royalty"
                });
            }
        }
    }

    [HarmonyPatch(typeof(RimWorld.Ability), "Activate", new Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) })]
    public static class Patch_Ability_Activate
    {
        public static void Postfix(RimWorld.Ability __instance)
        {
            if (!ModsConfig.RoyaltyActive) return;
            Pawn pawn = __instance.pawn;
            if (pawn == null || !pawn.IsColonist) return;

            bool isPsycast = __instance.GetType().Name.Equals("Psycast", StringComparison.OrdinalIgnoreCase) ||
                             __instance.def.abilityClass?.Name.Equals("Psycast", StringComparison.OrdinalIgnoreCase) == true;

            if (isPsycast)
            {
                var coreComp = Find.World?.GetComponent<RimSynapse.SynapseCoreWorldComponent>();
                if (coreComp != null)
                {
                    coreComp.EnqueuePastEvent(new PastEvent
                    {
                        gameTick = Find.TickManager.TicksGame,
                        eventDescription = $"{pawn.Name.ToStringShort} cast the psycast {__instance.def.label}.",
                        pawnSnapshots = new System.Collections.Generic.Dictionary<string, string>(),
                        category = "Royalty"
                    });
                }
            }
        }
    }
}
