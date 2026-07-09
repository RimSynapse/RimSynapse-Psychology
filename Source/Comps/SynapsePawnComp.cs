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
        
        public int lastJournalUpdateDay = -1;
        private bool isAwaitingJournalUpdate = false;

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
                            float averageMood = moodSamples > 0 ? (dailyMoodAccumulator / moodSamples) : pawn.needs.mood.CurLevelPercentage;
                            
                            RimSynapse.Utils.SynapseFileLogger.LogEvent("Psychology", pawn, "DailyReview", $"Triggered. Asleep: {isAsleep}, Hour: {currentHour}, Avg Mood: {averageMood:F2}");

                            // Route to API, passing memories from Core
                            var coreComp = pawn.GetComp<RimSynapse.Comps.SynapseCorePawnComp>();
                            var memories = coreComp != null ? coreComp.memories : new System.Collections.Generic.List<RimSynapse.Models.WeightedMemory>();
                            
                            // Reset daily tracking immediately so we can start recording the next day
                            dailyMoodAccumulator = 0f;
                            moodSamples = 0;

                            RimSynapse.Psychology.API.SynapsePsychology.QueueDailyPsychologyReview(pawn, averageMood, memories, (success) => {
                                isAwaitingJournalUpdate = false;
                                if (success)
                                {
                                    lastJournalUpdateDay = currentDay;
                                }
                            });
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

        private bool isGeneratingBackstory = false;

        private void GenerateAIBackstory(Pawn pawn)
        {
            var coreComp = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (coreComp == null) return;

            if (!SynapseClient.IsOnline)
            {
                // Wait and try again later
                ticksToGenerateBackstory = 2500;
                return;
            }

            isGeneratingBackstory = true;

            string childhood = pawn.story.Childhood?.description ?? "Unknown childhood.";
            string adulthood = pawn.story.Adulthood?.description ?? "Unknown adulthood.";
            string traits = string.Join(", ", pawn.story.traits.allTraits.Select(t => t.Label));

            string systemPrompt = @"You are a master storyteller in the RimWorld universe.
Write a 300-500 word psychological profile for the colonist.
Your task is to weave the provided childhood and adulthood backstories into a single, cohesive narrative.

CRITICAL REQUIREMENTS:
1. You MUST invent one specific, vivid memory (good or bad) from their childhood that explains their behavior.
2. You MUST invent one specific, vivid memory (good or bad) from their adulthood that shaped who they are today.
3. The story MUST be 300-500 words long.
4. After the story, provide exactly 3 psychological archetype keywords (e.g., INTJ, Hypervigilant, Pragmatic).
5. Finally, provide a 1-sentence journal entry from the pawn's perspective describing their first impression of arriving/surviving on this world.

Format your response exactly like this:
[STORY]
(Your 300-500 word story here)
[TRAITS]
Keyword1, Keyword2, Keyword3
[FIRST_IMPRESSION]
(1 sentence journal entry here)";

            string userMessage = $@"Colonist Name: {pawn.Name.ToStringShort}
Childhood: {childhood}
Adulthood: {adulthood}
Current Traits: {traits}";

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => OnBackstoryGenerated(result, pawn, coreComp)
            );
        }

        private void OnBackstoryGenerated(ChatResult result, Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            isGeneratingBackstory = false;
            
            if (!result.success)
            {
                Log.Warning($"[RimSynapse-Psychology] Failed to generate backstory for {pawn.Name.ToStringShort}: {result.error}");
                ticksToGenerateBackstory = 5000; // Try again in 5000 ticks
                return;
            }

            string response = result.content;
            
            string story = "";
            string traitString = "";
            string firstImpression = "";

            try
            {
                if (response.Contains("[STORY]") && response.Contains("[TRAITS]") && response.Contains("[FIRST_IMPRESSION]"))
                {
                    int storyIdx = response.IndexOf("[STORY]") + "[STORY]".Length;
                    int traitsIdx = response.IndexOf("[TRAITS]");
                    int impIdx = response.IndexOf("[FIRST_IMPRESSION]");
                    
                    story = response.Substring(storyIdx, traitsIdx - storyIdx).Trim();
                    traitString = response.Substring(traitsIdx + "[TRAITS]".Length, impIdx - (traitsIdx + "[TRAITS]".Length)).Trim();
                    firstImpression = response.Substring(impIdx + "[FIRST_IMPRESSION]".Length).Trim();
                }
                else if (response.Contains("[STORY]") && response.Contains("[TRAITS]"))
                {
                    int storyIdx = response.IndexOf("[STORY]") + "[STORY]".Length;
                    int traitsIdx = response.IndexOf("[TRAITS]");
                    
                    story = response.Substring(storyIdx, traitsIdx - storyIdx).Trim();
                    traitString = response.Substring(traitsIdx + "[TRAITS]".Length).Trim();
                    firstImpression = "The Rim is a harsh place, but I will survive.";
                }
                else
                {
                    // Fallback parsing if LLM disobeys format
                    story = response;
                    traitString = "Complex, Unpredictable, Resilient";
                    firstImpression = "The Rim is a harsh place, but I will survive.";
                }

                coreComp.dynamicBackstory = story;
                
                coreComp.llmTraits.Clear();
                var traits = traitString.Split(new[] { ',', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var t in traits)
                {
                    string cleanedTrait = t.Trim();
                    if (!string.IsNullOrEmpty(cleanedTrait))
                    {
                        coreComp.llmTraits.Add(cleanedTrait);
                    }
                }
                
                // Inject the first memory!
                var memory = new RimSynapse.Models.WeightedMemory
                {
                    summary = firstImpression,
                    weight = 1.0f,
                    gameTick = Find.TickManager.TicksGame,
                    tags = new List<string> { "Arrival" }
                };
                coreComp.memories.Add(memory);

                hasBackstoryMemory = true;

                string title = "Backstory Discovered";
                string text = $"{pawn.Name.ToStringShort} has shared their backstory with you.\n\nOpen their Psychology tab to learn more about their past and personality traits.";
                Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.NeutralEvent, pawn);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimSynapse-Psychology] Error parsing backstory for {pawn.Name.ToStringShort}: {ex.Message}");
                ticksToGenerateBackstory = 5000;
            }
        }

    }
}
