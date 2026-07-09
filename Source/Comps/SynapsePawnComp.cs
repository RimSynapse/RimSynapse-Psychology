using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using RimSynapse.Models;
using RimSynapse.Internal; // Assuming this is for SynapseLog if available, but let's avoid tight coupling.

namespace RimSynapse.Psychology.Comps
{
    public enum BreakCategory
    {
        Default,
        Homicidal,
        Suicidal,
        IssueAverse
    }

    public enum BreakIntensity
    {
        Light,
        Medium,
        Severe
    }

    public class SynapsePawnComp : ThingComp
    {
        // Track whether this pawn has had a backstory memory generated yet.
        // This helps queue LLM calls safely instead of freezing the game on spawn.
        public bool hasBackstoryMemory = false;
        private int ticksToGenerateBackstory = 2500; // ~1 in-game hour delay to simulate LLM

        // Active AI-driven modifiers
        public BreakCategory breakCategory = BreakCategory.Default;
        public bool ideologyZealot = false;

        // Async LLM Break Data
        public MentalStateDef predictedBreakState = null;
        public string currentBreakWarning = null;
        public bool isEuphoric = false;
        
        // Long-term Context Modifiers
        public float breakDurationHours = 6f; // Default 6 hours
        public BreakIntensity breakIntensity = BreakIntensity.Medium;

        // Daily sleep tracking
        private bool wasAsleep = false;
        private float dailyMoodAccumulator = 0f;
        private int moodSamples = 0;
        
        public int lastExtremeNegativeTick = -1;

        private const int TickIntervalDay = 60000;
        private const int TickInterval6Hours = 15000;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref hasBackstoryMemory, "hasBackstoryMemory", false);
            
            Scribe_Values.Look(ref breakCategory, "breakCategory", BreakCategory.Default);
            Scribe_Values.Look(ref ideologyZealot, "ideologyZealot", false);

            Scribe_Defs.Look(ref predictedBreakState, "predictedBreakState");
            Scribe_Values.Look(ref currentBreakWarning, "currentBreakWarning");
            Scribe_Values.Look(ref isEuphoric, "isEuphoric", false);
            
            Scribe_Values.Look(ref breakDurationHours, "breakDurationHours", 6f);
            Scribe_Values.Look(ref breakIntensity, "breakIntensity", BreakIntensity.Medium);
            Scribe_Values.Look(ref wasAsleep, "wasAsleep", false);
            Scribe_Values.Look(ref lastExtremeNegativeTick, "lastExtremeNegativeTick", -1);
            Scribe_Values.Look(ref ticksToGenerateBackstory, "ticksToGenerateBackstory", 2500);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            
            if (parent is Pawn pawn && pawn.Spawned && !pawn.Dead)
            {
                // Async Backstory Stub
                if (!hasBackstoryMemory && pawn.Faction == Faction.OfPlayer)
                {
                    ticksToGenerateBackstory -= 250;
                    if (ticksToGenerateBackstory <= 0)
                    {
                        GenerateStubBackstory(pawn);
                        hasBackstoryMemory = true;
                    }
                }

                // Sleep Tracking & Daily Review (TickRare is 250 ticks)
                if (pawn.needs != null && pawn.needs.mood != null)
                {
                    dailyMoodAccumulator += pawn.needs.mood.CurLevelPercentage;
                    moodSamples++;
                    
                    bool isAsleep = pawn.jobs != null && pawn.jobs.curDriver != null && pawn.jobs.curDriver.asleep;
                    if (isAsleep && !wasAsleep)
                    {
                        // Pawn just fell asleep. Trigger daily review.
                        float averageMood = moodSamples > 0 ? (dailyMoodAccumulator / moodSamples) : pawn.needs.mood.CurLevelPercentage;
                        
                        RimSynapse.Utils.SynapseFileLogger.LogEvent("Psychology", pawn, "SleepReview", $"Average Mood: {averageMood:F2}");

                        // Route to API, passing memories from Core
                        var coreComp = pawn.GetComp<RimSynapse.Comps.SynapseCorePawnComp>();
                        var memories = coreComp != null ? coreComp.memories : new System.Collections.Generic.List<RimSynapse.Models.WeightedMemory>();
                        RimSynapse.Psychology.API.SynapsePsychology.QueueDailyPsychologyReview(pawn, averageMood, memories);
                        
                        // Reset daily tracking
                        dailyMoodAccumulator = 0f;
                        moodSamples = 0;
                    }
                    wasAsleep = isAsleep;
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (parent is Pawn pawn && pawn.Faction == Faction.OfPlayer)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Psychology",
                    defaultDesc = "View this pawn's psychological profile, traits, and journal of core memories.",
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Commands/PsychologyIcon", true),
                    action = () =>
                    {
                        Find.WindowStack.Add(new UI.Dialog_PawnPsychology(pawn));
                    }
                };
            }
        }

        private void GenerateStubBackstory(Pawn pawn)
        {
            var coreComp = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (coreComp == null) return;

            coreComp.dynamicBackstory = $"{pawn.Name.ToStringShort} grew up in a harsh, unforgiving environment, learning early on that survival requires a pragmatic approach to morality. Despite their rugged exterior, they harbor a secret fascination with ancient literature and find solace in quiet moments studying the stars. This dichotomy of raw survivalism and quiet intellectualism defines their approach to life on the Rim.";
            
            coreComp.llmTraits.Clear();
            coreComp.llmTraits.Add("INTJ");
            coreComp.llmTraits.Add("Hypervigilant");
            coreComp.llmTraits.Add("Pragmatic");

            string title = "Backstory Discovered";
            string text = $"{pawn.Name.ToStringShort} has shared their backstory with you.\n\nOpen their Psychology tab to learn more about their past and personality traits.";
            Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.NeutralEvent, pawn);
        }

    }
}
