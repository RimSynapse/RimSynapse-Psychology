using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using RimSynapse.Models;

namespace RimSynapse.Psychology.Comps
{
    /// <summary>
    /// Handles the LLM-driven backstory generation for newly spawned colonists.
    /// Separated from the main SynapsePawnComp for clarity.
    /// </summary>
    public partial class SynapsePawnComp
    {
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

            string systemPrompt = @"You are a colonist in the harsh RimWorld universe, writing in your personal journal.
Write a 300-500 word autobiographical backstory. 
Your task is to weave your provided childhood and adulthood backstories into a single, cohesive narrative.

CRITICAL REQUIREMENTS:
1. You MUST write ENTIRELY in the first person (using 'I', 'me', 'my'). Do NOT use the third person.
2. You MUST invent one specific, vivid memory (good or bad) from your childhood that explains your behavior.
3. You MUST invent one specific, vivid memory (good or bad) from your adulthood that shaped who you are today.
4. The story MUST be 300-500 words long.
5. After the story, under [TRAITS], you MUST provide exactly 3 psychological archetypes:
   - First: A Jungian personality type (MBTI, e.g., ENFP, INTJ, INTP, INFJ, ESTP, etc. - This must always be evaluated and included).
   - Second: A Core Archetype (e.g., Caregiver, Outlaw, Creator, Jester, Sage, Ruler, Hero).
   - Third: A Temperament/Behavioral trait (e.g., Sanguine, Melancholic, Phlegmatic, Choleric).

Format your response exactly like this:
[STORY]
(Your 300-500 word story here)
[TRAITS]
Jungian Type: ENFP, Core Archetype: Outlaw, Temperament: Sanguine
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
