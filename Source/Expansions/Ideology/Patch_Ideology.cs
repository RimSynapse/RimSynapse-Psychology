using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.Models;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(RitualOutcomeEffectWorker), "Apply")]
    public static class Patch_RitualOutcomeEffectWorker_Apply
    {
        public static void Postfix(RitualOutcomeEffectWorker __instance, float progress, Dictionary<Pawn, int> totalOutcomeProgress, LordJob_Ritual jobRitual)
        {
            if (!ModsConfig.IdeologyActive) return;
            if (jobRitual == null) return;

            string ritualLabel = jobRitual.RitualLabel;
            string outcomeLabel = "completed";

            try
            {
                // Try to determine the outcome def name
                var outcome = __instance.def.outcomeChances
                    .OrderBy(o => Math.Abs(o.positivityIndex - progress))
                    .FirstOrDefault();
                if (outcome != null)
                {
                    outcomeLabel = outcome.label;
                }
            }
            catch {}

            var coreComp = Find.World?.GetComponent<RimSynapse.SynapseCoreWorldComponent>();
            if (coreComp != null)
            {
                coreComp.EnqueuePastEvent(new PastEvent
                {
                    gameTick = Find.TickManager.TicksGame,
                    eventDescription = $"The ritual '{ritualLabel}' was concluded with the outcome: '{outcomeLabel}'.",
                    pawnSnapshots = new System.Collections.Generic.Dictionary<string, string>(),
                    category = "Ideology"
                });
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_IdeoTracker), "SetIdeo")]
    public static class Patch_Pawn_IdeoTracker_SetIdeo
    {
        public static void Postfix(Pawn_IdeoTracker __instance, Ideo newIdeo)
        {
            if (!ModsConfig.IdeologyActive) return;
            Pawn pawn = HarmonyLib.Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null || !pawn.IsColonist) return;

            var coreComp = Find.World?.GetComponent<RimSynapse.SynapseCoreWorldComponent>();
            if (coreComp != null)
            {
                coreComp.EnqueuePastEvent(new PastEvent
                {
                    gameTick = Find.TickManager.TicksGame,
                    eventDescription = $"{pawn.Name.ToStringShort} experienced a crisis of faith and converted to the ideology '{newIdeo?.name ?? "Unknown"}'.",
                    pawnSnapshots = new System.Collections.Generic.Dictionary<string, string>(),
                    category = "Ideology"
                });
            }
        }
    }
}
