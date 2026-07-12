using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
    public static class Patch_FloatMenuMakerMap_Interactions
    {
        public static void Postfix(List<Pawn> selectedPawns, Vector3 clickPos, ref List<FloatMenuOption> __result)
        {
            if (selectedPawns == null || selectedPawns.Count != 1) return;
            Pawn pawn = selectedPawns[0];

            if (pawn.Drafted || !pawn.IsColonistPlayerControlled) return;

            IntVec3 clickCell = IntVec3.FromVector3(clickPos);
            foreach (Thing t in clickCell.GetThingList(pawn.Map))
            {
                if (t is Pawn targetPawn && targetPawn != pawn && targetPawn.RaceProps.Humanlike)
                {
                    // Target must be awake and capable
                    if (targetPawn.Downed || targetPawn.Dead || targetPawn.InMentalState || !targetPawn.Awake())
                        continue;

                    string label = (targetPawn.Faction == pawn.Faction || targetPawn.IsPrisoner || targetPawn.IsSlaveOfColony) 
                        ? "Initiate Therapy Session" 
                        : "Attempt Recruitment / Conversion";

                    // Build float menu option
                    __result.Add(new FloatMenuOption(label, () =>
                    {
                        // Acceptance logic
                        bool accepted = true;
                        string rejectReason = "";

                        // Slaves/Prisoners always accept. Colonists might reject. Visitors might reject.
                        if (!targetPawn.IsPrisoner && !targetPawn.IsSlaveOfColony)
                        {
                            // Opinion check
                            int opinion = targetPawn.relations.OpinionOf(pawn);
                            if (opinion < -20)
                            {
                                accepted = false;
                                rejectReason = "Hates you";
                                
                                // Insulted reaction if they are a visitor
                                if (targetPawn.Faction != pawn.Faction && targetPawn.Faction != null)
                                {
                                    // targetPawn.Faction.TryAffectGoodwillWith(pawn.Faction, -5, true, true, HistoryEventDefOf.MemberInsulted);
                                    Messages.Message($"{targetPawn.LabelShort} was insulted by {pawn.LabelShort}'s approach.", MessageTypeDefOf.NegativeEvent, false);
                                }
                            }
                            // else if (targetPawn.CurJob != null && targetPawn.CurJob.def.isCritical)
                            // {
                            //     accepted = false;
                            //     rejectReason = "Too busy";
                            // }
                        }

                        if (!accepted)
                        {
                            MoteMaker.ThrowText(targetPawn.DrawPos, targetPawn.Map, rejectReason, Color.red);
                            return;
                        }

                        // Success! Issue jobs to both pawns
                        Job jobInitiator = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("Synapse_InitiateTherapy"), targetPawn);
                        pawn.jobs.TryTakeOrderedJob(jobInitiator);
                        
                    }, MenuOptionPriority.Default));
                }
            }
        }
    }
}
