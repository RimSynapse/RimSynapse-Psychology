using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using RimSynapse.Psychology.Models;
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
        public List<WeightedMemory> memories = new List<WeightedMemory>();
        public List<OpinionSample> opinionHistory = new List<OpinionSample>();
        public string personalitySummary;
        
        // Track whether this pawn has had a backstory memory generated yet.
        // This helps queue LLM calls safely instead of freezing the game on spawn.
        public bool hasBackstoryMemory = false;

        // Active AI-driven modifiers
        public Dictionary<string, float> thoughtSensitivities = new Dictionary<string, float>();
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
            Scribe_Collections.Look(ref memories, "synapseMemories", LookMode.Deep);
            Scribe_Collections.Look(ref opinionHistory, "synapseOpinionHistory", LookMode.Deep);
            Scribe_Values.Look(ref personalitySummary, "synapsePersonality");
            Scribe_Values.Look(ref hasBackstoryMemory, "hasBackstoryMemory", false);
            
            Scribe_Collections.Look(ref thoughtSensitivities, "thoughtSensitivities", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref breakCategory, "breakCategory", BreakCategory.Default);
            Scribe_Values.Look(ref ideologyZealot, "ideologyZealot", false);

            Scribe_Defs.Look(ref predictedBreakState, "predictedBreakState");
            Scribe_Values.Look(ref currentBreakWarning, "currentBreakWarning");
            Scribe_Values.Look(ref isEuphoric, "isEuphoric", false);
            
            Scribe_Values.Look(ref breakDurationHours, "breakDurationHours", 6f);
            Scribe_Values.Look(ref breakIntensity, "breakIntensity", BreakIntensity.Medium);
            Scribe_Values.Look(ref wasAsleep, "wasAsleep", false);
            Scribe_Values.Look(ref lastExtremeNegativeTick, "lastExtremeNegativeTick", -1);
            
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (memories == null) memories = new List<WeightedMemory>();
                if (opinionHistory == null) opinionHistory = new List<OpinionSample>();
                if (thoughtSensitivities == null) thoughtSensitivities = new Dictionary<string, float>();
            }
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            
            if (parent is Pawn pawn && pawn.Spawned && !pawn.Dead)
            {
                // Memory decay once per day
                if (pawn.IsHashIntervalTick(TickIntervalDay))
                {
                    DoMemoryDecay();
                }

                // Sample opinions periodically (e.g. every 6 in-game hours)
                if (pawn.IsHashIntervalTick(TickInterval6Hours))
                {
                    SampleOpinions(pawn);
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
                        
                        RimSynapse.Psychology.Utils.PsychologyLogger.LogEvent(pawn, "SleepReview", $"Average Mood: {averageMood:F2}");

                        // Route to API
                        RimSynapse.Psychology.API.SynapsePsychology.QueueDailyPsychologyReview(pawn, averageMood, memories);
                        
                        // Reset daily tracking
                        dailyMoodAccumulator = 0f;
                        moodSamples = 0;
                    }
                    wasAsleep = isAsleep;
                }
            }
        }

        private void DoMemoryDecay()
        {
            for (int i = memories.Count - 1; i >= 0; i--)
            {
                var mem = memories[i];
                mem.weight -= mem.decayRate;
                
                if (mem.weight <= 0f)
                {
                    memories.RemoveAt(i);
                }
            }
        }

        private void SampleOpinions(Pawn pawn)
        {
            if (pawn.relations == null || pawn.Map == null) return;

            var colonists = pawn.Map.mapPawns?.FreeColonists;
            if (colonists != null)
            {
                int currentTick = GenTicks.TicksGame;
                
                foreach (var other in colonists)
                {
                    if (other == pawn) continue;

                    int opinion = pawn.relations.OpinionOf(other);
                    
                    opinionHistory.Add(new OpinionSample
                    {
                        targetPawnId = other.ThingID,
                        opinion = opinion,
                        gameTick = currentTick
                    });
                }
                
                // Keep history trimmed to save space (e.g., last 20 samples per target)
                // This is a naive implementation; in a full version we'd group by target and prune
                TrimOpinionHistory();
            }
        }

        private void TrimOpinionHistory()
        {
            // Group by targetPawnId and keep only the latest 20 samples per pawn
            var grouped = opinionHistory.GroupBy(o => o.targetPawnId);
            var newHistory = new List<OpinionSample>();
            
            foreach (var group in grouped)
            {
                var recentSamples = group.OrderByDescending(o => o.gameTick).Take(20);
                newHistory.AddRange(recentSamples);
            }
            
            opinionHistory = newHistory;
        }
    }
}
