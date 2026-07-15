using System;
using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.Models;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(Pawn), "PostApplyDamage")]
    public static class Patch_Pawn_PostApplyDamage_Entity
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo)
        {
            if (!ModsConfig.AnomalyActive) return;
            if (__instance == null || !__instance.IsColonist) return;

            Thing instigator = dinfo.Instigator;
            if (instigator != null)
            {
                bool isEntity = false;
                if (instigator.Faction != null && (instigator.Faction.def.defName == "Entities" || instigator.Faction.def.defName == "EntitiesHostile"))
                {
                    isEntity = true;
                }
                else if (instigator.def != null && instigator.def.defName.StartsWith("Anomaly", StringComparison.OrdinalIgnoreCase))
                {
                    isEntity = true;
                }

                if (isEntity)
                {
                    var coreComp = Find.World?.GetComponent<RimSynapse.SynapseCoreWorldComponent>();
                    if (coreComp != null)
                    {
                        coreComp.EnqueuePastEvent(new PastEvent
                        {
                            gameTick = Find.TickManager.TicksGame,
                            eventDescription = $"{__instance.Name.ToStringShort} was attacked by a horrific entity: {instigator.LabelShort}.",
                            pawnSnapshots = new System.Collections.Generic.Dictionary<string, string>(),
                            category = "Anomaly"
                        });
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(RimWorld.CompStudiableMonolith), "Study", new Type[] { typeof(Pawn), typeof(float), typeof(float) })]
    public static class Patch_CompVoidMonolith_Notify_Studied
    {
        public static void Postfix(RimWorld.CompStudiableMonolith __instance, Pawn studyer)
        {
            if (!ModsConfig.AnomalyActive) return;
            if (studyer == null || !studyer.IsColonist) return;

            var coreComp = Find.World?.GetComponent<RimSynapse.SynapseCoreWorldComponent>();
            if (coreComp != null)
            {
                coreComp.EnqueuePastEvent(new PastEvent
                {
                    gameTick = Find.TickManager.TicksGame,
                    eventDescription = $"{studyer.Name.ToStringShort} studied the Void Monolith.",
                    pawnSnapshots = new System.Collections.Generic.Dictionary<string, string>(),
                    category = "Anomaly"
                });
            }
        }
    }
}
