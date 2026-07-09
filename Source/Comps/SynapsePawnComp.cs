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

    public partial class SynapsePawnComp : ThingComp
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

        // Psychological Profile Data
        public Dictionary<string, string> medicalProfile = new Dictionary<string, string>();

        // Daily sleep tracking
        private bool wasAsleep = false;
        private float dailyMoodAccumulator = 0f;
        private int moodSamples = 0;
        
        public int lastExtremeNegativeTick = -1;
        
        public int lastJournalUpdateDay = -1;
        public bool isAwaitingJournalUpdate = false;
        public float savedAverageMood = 0.5f;

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
            Scribe_Values.Look(ref lastJournalUpdateDay, "lastJournalUpdateDay", -1);
            Scribe_Values.Look(ref isAwaitingJournalUpdate, "isAwaitingJournalUpdate", false);
            Scribe_Values.Look(ref savedAverageMood, "savedAverageMood", 0.5f);
            Scribe_Values.Look(ref hasCheckedAdulthood, "hasCheckedAdulthood", false);

            Scribe_Collections.Look(ref medicalProfile, "medicalProfile", LookMode.Value, LookMode.Value);
            
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (medicalProfile == null) medicalProfile = new Dictionary<string, string>();
            }
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            
            if (parent is Pawn pawn && pawn.Spawned && !pawn.Dead)
            {
                // Async Backstory Stub
                if (!hasBackstoryMemory && pawn.Faction == Faction.OfPlayer && !isGeneratingBackstory)
                {
                    ticksToGenerateBackstory -= 250;
                    if (ticksToGenerateBackstory <= 0)
                    {
                        GenerateAIBackstory(pawn);
                    }
                }

                // LLM-driven adulthood backstory for colony-born pawns turning 20
                if (hasBackstoryMemory && !hasCheckedAdulthood)
                {
                    CheckAdulthoodBackstoryNeeded(pawn);
                }

                // Sleep Tracking & Daily Review (TickRare is 250 ticks)
                if (pawn.needs != null && pawn.needs.mood != null)
                {
                    dailyMoodAccumulator += pawn.needs.mood.CurLevelPercentage;
                    moodSamples++;
                    
                    int currentDay = GenDate.DaysPassed;
                    
                    if (currentDay > lastJournalUpdateDay && !isAwaitingJournalUpdate)
                    {
                        bool isAsleep = pawn.jobs != null && pawn.jobs.curDriver != null && pawn.jobs.curDriver.asleep;
                        int currentHour = GenDate.HourOfDay(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(pawn.Map.Tile).x);

                        // Trigger if they just fell asleep OR if it's late (22:00) and they don't sleep
                        if ((isAsleep && !wasAsleep) || currentHour >= 22)
                        {
                            isAwaitingJournalUpdate = true;
                            savedAverageMood = moodSamples > 0 ? (dailyMoodAccumulator / moodSamples) : pawn.needs.mood.CurLevelPercentage;
                            
                            RimSynapse.Utils.SynapseFileLogger.LogEvent("Psychology", pawn, "DailyReview", $"Flagged for Opportunistic Review. Asleep: {isAsleep}, Hour: {currentHour}, Avg Mood: {savedAverageMood:F2}");

                            // Reset daily tracking immediately so we can start recording the next day
                            dailyMoodAccumulator = 0f;
                            moodSamples = 0;
                        }
                        
                        wasAsleep = isAsleep;
                    }
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
    }
}
