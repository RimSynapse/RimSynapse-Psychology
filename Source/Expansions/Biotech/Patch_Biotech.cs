using System;
using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.Models;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(Verse.Hediff_Pregnant), "DoBirthSpawn")]
    public static class Patch_Hediff_Pregnant_DoBirth
    {
        public static void Postfix(Pawn mother, Pawn father)
        {
            if (!ModsConfig.BiotechActive) return;
            if (mother == null || !mother.IsColonist) return;

            var coreComp = Find.World?.GetComponent<RimSynapse.SynapseCoreWorldComponent>();
            if (coreComp != null)
            {
                coreComp.EnqueuePastEvent(new PastEvent
                {
                    gameTick = Find.TickManager.TicksGame,
                    eventDescription = $"{mother.Name.ToStringShort} gave birth to a child.",
                    pawnSnapshots = new System.Collections.Generic.Dictionary<string, string>(),
                    category = "Biotech"
                });
            }
        }
    }

    [HarmonyPatch(typeof(RimWorld.GeneUtility), "ImplantXenogermItem")]
    public static class Patch_GeneUtility_ImplantXenogerm
    {
        public static void Postfix(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive) return;
            if (pawn == null || !pawn.IsColonist) return;

            var coreComp = Find.World?.GetComponent<RimSynapse.SynapseCoreWorldComponent>();
            if (coreComp != null)
            {
                coreComp.EnqueuePastEvent(new PastEvent
                {
                    gameTick = Find.TickManager.TicksGame,
                    eventDescription = $"{pawn.Name.ToStringShort} underwent gene modification and had a new xenogerm implanted.",
                    pawnSnapshots = new System.Collections.Generic.Dictionary<string, string>(),
                    category = "Biotech"
                });
            }
        }
    }
}
