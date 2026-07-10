using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.Psychology.Comps;

namespace RimSynapse.Psychology.Patches
{
    [HarmonyPatch(typeof(MarriageCeremonyUtility), "Married")]
    public static class Patch_MarriageCeremonyUtility_Married
    {
        public static void Postfix(Pawn firstPawn, Pawn secondPawn)
        {
            if (firstPawn == null || secondPawn == null) return;

            var comp1 = firstPawn.GetComp<SynapsePawnComp>();
            if (comp1 != null && comp1.socialNetwork.TryGetValue(secondPawn.GetUniqueLoadID(), out var record1))
            {
                if (record1.relationshipMemories.Count > 0)
                {
                    string mem1 = record1.relationshipMemories.RandomElement();
                    Find.LetterStack.ReceiveLetter($"{firstPawn.Name.ToStringShort}'s Vows", $"During the ceremony, {firstPawn.Name.ToStringShort} spoke from the heart:\n\n\"{mem1}\"", LetterDefOf.PositiveEvent, firstPawn);
                }
            }
            
            var comp2 = secondPawn.GetComp<SynapsePawnComp>();
            if (comp2 != null && comp2.socialNetwork.TryGetValue(firstPawn.GetUniqueLoadID(), out var record2))
            {
                if (record2.relationshipMemories.Count > 0)
                {
                    string mem2 = record2.relationshipMemories.RandomElement();
                    Find.LetterStack.ReceiveLetter($"{secondPawn.Name.ToStringShort}'s Vows", $"During the ceremony, {secondPawn.Name.ToStringShort} spoke from the heart:\n\n\"{mem2}\"", LetterDefOf.PositiveEvent, secondPawn);
                }
            }
        }
    }

    public static class Patch_Funeral_Apply
    {
        public static void Postfix(LordJob_Ritual jobRitual)
        {
            if (jobRitual == null || jobRitual.assignments == null) return;
            var speaker = jobRitual.assignments.FirstAssignedPawn("speaker");
            
            Pawn deceased = null;
            
            try 
            {
                var obligation = Traverse.Create(jobRitual).Field("obligation").GetValue();
                if (obligation != null) 
                {
                    var targetInfo = Traverse.Create(obligation).Field("targetA").GetValue();
                    if (targetInfo != null)
                    {
                        var targetThing = Traverse.Create(targetInfo).Property("Thing").GetValue<Thing>();
                        deceased = targetThing as Pawn;
                        if (deceased == null && targetThing is Corpse corpse) deceased = corpse.InnerPawn;
                    }
                }
            } 
            catch {}

            if (deceased == null && speaker != null && speaker.Map != null)
            {
                foreach (var p in speaker.Map.mapPawns.AllPawns)
                {
                    if (p.Dead && p.Corpse != null && p.Corpse.Position.DistanceTo(speaker.Position) < 20f)
                    {
                        deceased = p;
                        break;
                    }
                }
            }

            if (speaker != null && deceased != null)
            {
                var comp = speaker.GetComp<SynapsePawnComp>();
                if (comp != null && comp.socialNetwork.TryGetValue(deceased.GetUniqueLoadID(), out var record))
                {
                    if (record.relationshipMemories.Count > 0)
                    {
                        string memory = record.relationshipMemories.RandomElement();
                        Find.LetterStack.ReceiveLetter($"{speaker.Name.ToStringShort}'s Eulogy", $"During the funeral, {speaker.Name.ToStringShort} stepped forward and shared a personal memory of {deceased.Name.ToStringShort}:\n\n\"{memory}\"", LetterDefOf.PositiveEvent, speaker);
                    }
                }
            }
        }
    }
}
