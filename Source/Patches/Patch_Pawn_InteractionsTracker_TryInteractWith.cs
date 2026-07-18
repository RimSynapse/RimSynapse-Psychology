using System;
using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.Psychology.Comps;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractWith")]
    public static class Patch_Pawn_InteractionsTracker_TryInteractWith
    {
        public static bool Prefix(Pawn_InteractionsTracker __instance, Pawn recipient, InteractionDef intDef, Pawn ___pawn)
        {
            if (intDef == InteractionDefOf.Insult || intDef.defName == "Slight")
            {
                var recComp = recipient.GetComp<SynapsePawnComp>();
                if (recComp != null)
                {
                    string initId = ___pawn.GetUniqueLoadID();
                    if (recComp.socialNetwork.ContainsKey(initId))
                    {
                        var record = recComp.socialNetwork[initId];
                        
                        // The recipient's perception of the initiator determines the shield
                        float familiarity = record.familiarity; // 0 to 100
                        float trust = record.trust; // -100 to 100
                        float opinion = 0f;
                        
                        if (recipient.relations != null)
                        {
                            opinion = recipient.relations.OpinionOf(___pawn);
                        }
                        
                        // Normalize trust and opinion to map -100 (zero shield) to 100 (max shield).
                        float trustNormalized = (trust + 100f) / 2f; 
                        float opinionNormalized = (opinion + 100f) / 2f; 
                        
                        // Combine metrics
                        float shieldPower = (familiarity * 0.5f) + (trustNormalized * 0.25f) + (opinionNormalized * 0.25f);
                        
                        if (shieldPower > 50f)
                        {
                            float chance = (shieldPower - 50f) / 50f;
                            
                            if (Rand.Chance(chance))
                            {
                                // Show text on the recipient, as they are the one brushing it off
                                if (recipient.Map != null)
                                {
                                    MoteMaker.ThrowText(recipient.DrawPos, recipient.Map, "Brushed off insult", 4f);
                                }
                                
                                return false; // Block the vanilla interaction completely
                            }
                        }
                    }
                }
            }
            return true; // Let the interaction proceed normally
        }
    }
}
