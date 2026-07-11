using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using RimSynapse.Models;
using RimSynapse.Utils;
using Newtonsoft.Json;

namespace RimSynapse.Psychology.Comps
{
    /// <summary>
    /// Memory-first backstory generation pipeline.
    ///
    /// Flow:
    ///   Step 1: Generate a childhood memory (100-200 words) from the vanilla childhood backstory + skill bonuses
    ///   Step 2: Generate an adulthood memory (100-200 words) from the vanilla adulthood backstory + skill bonuses
    ///   Step 3: Generate psychological traits + personality summary using both memories as context
    ///
    /// Each step is a separate LLM call. Memories are stored immediately as standalone
    /// psychological data points (memoryType "BackstoryChildhood" / "BackstoryAdulthood").
    /// These never decay (decayRate = 0).
    ///
    /// The final backstory narrative on the pawn comp is assembled from the memories,
    /// NOT the other way around. This means Chat, mood evaluation, and social
    /// interactions can reference individual memories without parsing a prose blob.
    /// </summary>
    public partial class SynapsePawnComp
    {
        private bool isGeneratingBackstory = false;

        /// <summary>
        /// Entry point — called from CompTickRare when the pawn doesn't have a backstory yet.
        /// Kicks off Step 1 (childhood memory generation).
        /// </summary>
        private void GenerateAIBackstory(Pawn pawn)
        {
            var coreComp = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (coreComp == null) return;

            isGeneratingBackstory = true;

            bool hasChildhood = coreComp.memories.Any(m => m.memoryType == "BackstoryChildhood");
            bool hasAdulthood = coreComp.memories.Any(m => m.memoryType == "BackstoryAdulthood");

            if (!hasChildhood)
            {
                // Step 1: Generate childhood memory
                GenerateChildhoodMemory(pawn, coreComp);
            }
            else if (!hasAdulthood && pawn.story?.Adulthood != null)
            {
                // Step 2: Skip childhood, go straight to adulthood
                GenerateAdulthoodMemory(pawn, coreComp);
            }
            else
            {
                // Both exist (or adulthood not applicable), skip straight to Personality Synthesis
                GeneratePersonalityProfile(pawn, coreComp);
            }
        }

        // ────────────────────────────────────────────────────────
        //  Step 1: Childhood Memory
        // ────────────────────────────────────────────────────────

        private void GenerateChildhoodMemory(Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            var childhood = pawn.story?.Childhood;
            string childhoodTitle = childhood?.title ?? "Unknown";
            string childhoodDesc = childhood?.description ?? "An unremarkable childhood.";

            // Extract skill bonuses from the backstory
            string skillBonuses = FormatSkillGains(childhood);
            string disabledWork = FormatDisabledWork(childhood);

            // Faction context for place-of-origin generation
            string factionType = pawn.Faction?.def?.LabelCap ?? "Unknown";
            string factionContext = "";
            if (pawn.kindDef?.backstoryCategories != null && pawn.kindDef.backstoryCategories.Count > 0)
            {
                factionContext = $"\nBackstory Categories: {string.Join(", ", pawn.kindDef.backstoryCategories)}";
            }

            string systemPrompt = @"You are writing a vivid first-person memory for a colonist in the RimWorld universe.
This memory is from their CHILDHOOD. It should be a specific, concrete scene — not a summary.

RULES:
- Write 100-200 words, first person (""I"", ""me"", ""my"")
- This is a SINGLE vivid memory, not a life summary
- Ground the memory in the skill bonuses: if they got +4 Mining, describe WHY through experience (""I spent years chipping limestone..."")
- If work types are disabled, hint at WHY (trauma, cultural taboo, physical limitation)
- The memory should feel personal and emotionally resonant — a moment they'd actually remember
- RimWorld setting: frontier planets, crashlanded survivors, tribal societies, harsh conditions
- You MUST also generate a ""Hometown"" — their place of origin. This should match their background:
  - Outlander/Settler → a named settlement or outpost (e.g., ""Kharstead"", ""Port Valen"")
  - Tribal → a geographic feature, camp, or caravan route (e.g., ""the Redstone caravan"", ""the marshlands east of Sleeping Ridge"")
  - Pirate → a ship, station, or raider den (e.g., ""the Rust Fang"", ""Scrapheap Station"")
  - Imperial → a named city or estate (e.g., ""the Stellarch's court at Novium"")
  - If their backstory implies they moved a lot or are orphaned, something vague is fine (""the roads between nowhere"")

You MUST respond in valid JSON:
{
  ""Memory"": ""I remember the first time I...(100-200 words)..."",
  ""Hometown"": ""Kharstead"",
  ""Tags"": [""Origin"", ""Childhood"", ""Mining""],
  ""EmotionalTone"": ""bittersweet""
}";

            string userMessage = $@"Colonist: {pawn.Name.ToStringShort}
Gender: {pawn.gender}
Faction Background: {factionType}
Childhood Backstory: ""{childhoodTitle}""
Vanilla Description: ""{childhoodDesc}""
Skill Bonuses from Childhood: {skillBonuses}
{(string.IsNullOrEmpty(disabledWork) ? "" : $"Disabled Work Types: {disabledWork}\n")}{factionContext}
Write a vivid childhood memory grounded in these skills.";

            var options = new ChatOptions { priority = 1, requestName = "Childhood Backstory", targetName = pawn.Name.ToStringShort };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => OnChildhoodMemoryGenerated(result, pawn, coreComp),
                options
            );
        }

        private void OnChildhoodMemoryGenerated(ChatResult result, Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            if (!result.success)
            {
                RimSynapse.SynapseLogger.Warn("psychology", $"[RimSynapse-Psychology] Failed childhood memory for {pawn.Name.ToStringShort}: {result.error}");
                isGeneratingBackstory = false;
                ticksToGenerateBackstory = 5000;
                return;
            }

            try
            {
                string json = JsonHelper.ExtractJson(result.content);
                if (json == null) { RimSynapse.SynapseLogger.Warn("psychology", "[RimSynapse-Psychology] No JSON in childhood memory response."); isGeneratingBackstory = false; return; }

                var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (parsed == null || !parsed.ContainsKey("Memory")) { isGeneratingBackstory = false; return; }

                string memoryText = parsed["Memory"].ToString();
                var tags = new List<string> { "Childhood", "Origin" };
                if (parsed.ContainsKey("Tags") && parsed["Tags"] is Newtonsoft.Json.Linq.JArray arr)
                {
                    tags = arr.Select(t => t.ToString()).ToList();
                    if (!tags.Contains("Childhood")) tags.Insert(0, "Childhood");
                }

                // Store hometown
                if (parsed.ContainsKey("Hometown"))
                {
                    coreComp.hometown = parsed["Hometown"].ToString();
                }

                // Store the childhood memory with a historically accurate date
                long childTick = SynapseDateHelper.GetChildhoodMemoryTick(pawn);
                coreComp.memories.Add(new WeightedMemory
                {
                    summary = memoryText,
                    weight = 3.0f,
                    baseWeight = 3.0f,
                    decayRate = 0f, // Backstory memories never decay
                    tags = tags,
                    memoryType = "BackstoryChildhood",
                    absTick = childTick,
                    gameTick = (int)(childTick - SynapseDateHelper.GetAdjustmentTick())
                });

                RimSynapse.SynapseLogger.Info("psychology", $"[RimSynapse-Psychology] Childhood memory generated for {pawn.Name.ToStringShort} ({memoryText.Length} chars).");

                // Chain to Step 2: Adulthood memory
                GenerateAdulthoodMemory(pawn, coreComp);
            }
            catch (Exception ex)
            {
                RimSynapse.SynapseLogger.Warn("psychology", $"[RimSynapse-Psychology] Failed to parse childhood memory: {ex.Message}");
                isGeneratingBackstory = false;
                ticksToGenerateBackstory = 5000;
            }
        }

        // ────────────────────────────────────────────────────────
        //  Step 2: Adulthood Memory
        // ────────────────────────────────────────────────────────

        private void GenerateAdulthoodMemory(Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            var adulthood = pawn.story?.Adulthood;
            string adulthoodTitle = adulthood?.title ?? "Unknown";
            string adulthoodDesc = adulthood?.description ?? "An uneventful adult life.";

            string skillBonuses = FormatSkillGains(adulthood);
            string disabledWork = FormatDisabledWork(adulthood);

            // Include the childhood memory we just generated so the AI maintains continuity
            var childhoodMemory = coreComp.memories.LastOrDefault(m => m.memoryType == "BackstoryChildhood");
            string childhoodContext = childhoodMemory != null
                ? $"\nChildhood Memory (already generated — maintain continuity):\n\"{childhoodMemory.summary}\""
                : "";

            string systemPrompt = @"You are writing a vivid first-person memory for a colonist in the RimWorld universe.
This memory is from their ADULTHOOD. It should be a specific, concrete scene — not a summary.

RULES:
- Write 100-200 words, first person (""I"", ""me"", ""my"")
- This is a SINGLE vivid memory, not a life summary
- Ground the memory in the skill bonuses: if they got +6 Shooting, describe the experience that gave them that skill
- If work types are disabled, hint at WHY (trauma, injury, philosophical opposition)
- If a childhood memory is provided, maintain narrative continuity (same character, consistent tone)
- The memory should mark a turning point or defining moment in their adult life
- RimWorld setting: frontier planets, caravans, sieges, research labs, tribal wars

You MUST respond in valid JSON:
{
  ""Memory"": ""The day I first...(100-200 words)..."",
  ""Tags"": [""Adulthood"", ""Combat"", ""Survival""],
  ""EmotionalTone"": ""determined""
}";

            string userMessage = $@"Colonist: {pawn.Name.ToStringShort}
Gender: {pawn.gender}
Adulthood Backstory: ""{adulthoodTitle}""
Vanilla Description: ""{adulthoodDesc}""
Skill Bonuses from Adulthood: {skillBonuses}
{(string.IsNullOrEmpty(disabledWork) ? "" : $"Disabled Work Types: {disabledWork}\n")}{childhoodContext}

Write a vivid adulthood memory grounded in these skills.";

            var options = new ChatOptions { priority = 2, requestName = "Adulthood Backstory", targetName = pawn.Name.ToStringShort };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => OnAdulthoodMemoryGenerated(result, pawn, coreComp),
                options
            );
        }

        private void OnAdulthoodMemoryGenerated(ChatResult result, Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            if (!result.success)
            {
                RimSynapse.SynapseLogger.Warn("psychology", $"[RimSynapse-Psychology] Failed adulthood memory for {pawn.Name.ToStringShort}: {result.error}");
                // Childhood memory was already stored — just mark backstory complete with what we have
                FinalizeBackstory(pawn, coreComp);
                return;
            }

            try
            {
                string json = JsonHelper.ExtractJson(result.content);
                if (json == null) { RimSynapse.SynapseLogger.Warn("psychology", "[RimSynapse-Psychology] No JSON in adulthood memory response."); FinalizeBackstory(pawn, coreComp); return; }

                var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (parsed == null || !parsed.ContainsKey("Memory")) { FinalizeBackstory(pawn, coreComp); return; }

                string memoryText = parsed["Memory"].ToString();
                var tags = new List<string> { "Adulthood" };
                if (parsed.ContainsKey("Tags") && parsed["Tags"] is Newtonsoft.Json.Linq.JArray arr)
                {
                    tags = arr.Select(t => t.ToString()).ToList();
                    if (!tags.Contains("Adulthood")) tags.Insert(0, "Adulthood");
                }

                // Store the adulthood memory with a historically accurate date
                long adultTick = SynapseDateHelper.GetAdulthoodMemoryTick(pawn);
                coreComp.memories.Add(new WeightedMemory
                {
                    summary = memoryText,
                    weight = 3.0f,
                    baseWeight = 3.0f,
                    decayRate = 0f, // Backstory memories never decay
                    tags = tags,
                    memoryType = "BackstoryAdulthood",
                    absTick = adultTick,
                    gameTick = (int)(adultTick - SynapseDateHelper.GetAdjustmentTick())
                });

                RimSynapse.SynapseLogger.Info("psychology", $"[RimSynapse-Psychology] Adulthood memory generated for {pawn.Name.ToStringShort} ({memoryText.Length} chars).");
            }
            catch (Exception ex)
            {
                RimSynapse.SynapseLogger.Warn("psychology", $"[RimSynapse-Psychology] Failed to parse adulthood memory: {ex.Message}");
            }

            // Chain to Step 3: Psychological profile synthesis
            GeneratePersonalityProfile(pawn, coreComp);
        }

        // ────────────────────────────────────────────────────────
        //  Step 3: Personality Profile (using memories as context)
        // ────────────────────────────────────────────────────────

        private void GeneratePersonalityProfile(Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            string traits = string.Join(", ", pawn.story?.traits?.allTraits?.Select(t => t.Label) ?? Enumerable.Empty<string>());

            // Gather the memories we just generated
            var childhoodMem = coreComp.memories.LastOrDefault(m => m.memoryType == "BackstoryChildhood");
            var adulthoodMem = coreComp.memories.LastOrDefault(m => m.memoryType == "BackstoryAdulthood");

            string memoriesContext = "";
            if (childhoodMem != null) memoriesContext += $"Childhood Memory:\n\"{childhoodMem.summary}\"\n\n";
            if (adulthoodMem != null) memoriesContext += $"Adulthood Memory:\n\"{adulthoodMem.summary}\"\n\n";

            string systemPrompt = @"You are analyzing the psychology of a RimWorld colonist based on their life memories.
Given their childhood memory, adulthood memory, and current personality traits, synthesize a psychological profile.

OUTPUT FORMAT:
1. [PERSONALITY] — A 2-3 sentence personality summary (third person). How do they come across? What drives them? What are they afraid of?
2. [ARCHETYPES] — Exactly 3 psychological archetypes on one line, comma-separated:
   - Jungian Type (MBTI: e.g., ENFP, INTJ, INFJ, ESTP)
   - Core Archetype (e.g., Caregiver, Outlaw, Creator, Sage, Ruler, Hero, Explorer, Jester)
   - Temperament (e.g., Sanguine, Melancholic, Phlegmatic, Choleric)
3. [FIRST_IMPRESSION] — A single first-person sentence: what this pawn thinks on arrival at the colony. Written as ""I"".

You MUST respond in valid JSON:
{
  ""Personality"": ""She is a quiet, resourceful survivor who..."",
  ""JungianType"": ""INTJ"",
  ""CoreArchetype"": ""Explorer"",
  ""Temperament"": ""Melancholic"",
  ""FirstImpression"": ""I've been walking for weeks, and this place will have to do.""
}";

            string userMessage = $@"Colonist: {pawn.Name.ToStringShort}
Gender: {pawn.gender}
Age: {pawn.ageTracker?.AgeBiologicalYears ?? 0}
Gender: {pawn.gender}
Current Traits: {traits}

{memoriesContext}Synthesize their psychological profile.";

            var options = new ChatOptions { priority = 3, requestName = "Psychological Profile", targetName = pawn.Name.ToStringShort };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => OnPersonalityProfileGenerated(result, pawn, coreComp),
                options
            );
        }

        private void OnPersonalityProfileGenerated(ChatResult result, Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            if (result.success)
            {
                try
                {
                    string json = JsonHelper.ExtractJson(result.content);
                    if (json != null)
                    {
                        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (parsed != null)
                        {
                            // Personality summary
                            if (parsed.TryGetValue("Personality", out object personalityObj))
                            {
                                coreComp.personalitySummary = personalityObj.ToString();
                            }

                            // LLM traits (archetypes)
                            coreComp.llmTraits.Clear();
                            if (parsed.TryGetValue("JungianType", out object jungian))
                                coreComp.llmTraits.Add($"Jungian Type: {jungian}");
                            if (parsed.TryGetValue("CoreArchetype", out object archetype))
                                coreComp.llmTraits.Add($"Core Archetype: {archetype}");
                            if (parsed.TryGetValue("Temperament", out object temperament))
                                coreComp.llmTraits.Add($"Temperament: {temperament}");

                            // First impression as a memory
                            if (parsed.TryGetValue("FirstImpression", out object impression))
                            {
                                long nowTick = SynapseDateHelper.GetCurrentAbsTick();
                                coreComp.memories.Add(new WeightedMemory
                                {
                                    summary = impression.ToString(),
                                    weight = 1.0f,
                                    baseWeight = 1.0f,
                                    decayRate = 0.02f,
                                    tags = new List<string> { "Arrival", "FirstImpression" },
                                    memoryType = "Arrival",
                                    absTick = nowTick,
                                    gameTick = Find.TickManager.TicksGame
                                });
                            }

                            // Build the composite backstory narrative from the memories
                            BuildDynamicBackstory(pawn, coreComp);

                            RimSynapse.SynapseLogger.Info("psychology", $"[RimSynapse-Psychology] Personality profile synthesized for {pawn.Name.ToStringShort}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    RimSynapse.SynapseLogger.Warn("psychology", $"[RimSynapse-Psychology] Failed to parse personality profile: {ex.Message}");
                }
            }

            FinalizeBackstory(pawn, coreComp);
        }

        // ────────────────────────────────────────────────────────
        //  Finalization
        // ────────────────────────────────────────────────────────

        /// <summary>
        /// Assembles the dynamic backstory from stored memories (no extra LLM call needed).
        /// </summary>
        private void BuildDynamicBackstory(Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            var sb = new StringBuilder();
            
            var childhoodMem = coreComp.memories.LastOrDefault(m => m.memoryType == "BackstoryChildhood");
            var adulthoodMem = coreComp.memories.LastOrDefault(m => m.memoryType == "BackstoryAdulthood");
            
            if (childhoodMem != null)
            {
                sb.AppendLine(childhoodMem.summary);
                sb.AppendLine();
            }
            if (adulthoodMem != null)
            {
                sb.AppendLine(adulthoodMem.summary);
                sb.AppendLine();
            }
            if (!string.IsNullOrEmpty(coreComp.personalitySummary))
            {
                sb.AppendLine(coreComp.personalitySummary);
            }

            coreComp.dynamicBackstory = sb.ToString().Trim();
        }

        /// <summary>
        /// Marks the backstory as complete and notifies the player.
        /// Called after all steps finish (or after partial completion if later steps fail).
        /// </summary>
        private void FinalizeBackstory(Pawn pawn, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            isGeneratingBackstory = false;
            hasBackstoryMemory = true;

            // Even if later steps failed, build a backstory from whatever memories we have
            if (string.IsNullOrEmpty(coreComp.dynamicBackstory))
            {
                BuildDynamicBackstory(pawn, coreComp);
            }

            string title = "Backstory Discovered";
            string text = $"{pawn.Name.ToStringShort} has shared their backstory with you.\n\n" +
                          "Open their Psychology tab to learn more about their past and personality traits.";

            var letter = new RimSynapse.Psychology.UI.ChoiceLetter_OpenPsychology
            {
                def = LetterDefOf.NeutralEvent,
                Label = title,
                Text = text,
                lookTargets = pawn,
                targetPawn = pawn,
                ID = Find.UniqueIDsManager.GetNextLetterID()
            };
            Find.LetterStack.ReceiveLetter(letter);
        }

        // ────────────────────────────────────────────────────────
        //  Utility: Extract skill info from vanilla BackstoryDef
        // ────────────────────────────────────────────────────────

        /// <summary>
        /// Formats the skill gains from a BackstoryDef into a human-readable string.
        /// e.g., "+4 Mining, +2 Crafting, -3 Social"
        /// </summary>
        private static string FormatSkillGains(BackstoryDef backstory)
        {
            if (backstory?.skillGains == null || backstory.skillGains.Count == 0)
                return "None";

            var parts = new List<string>();
            foreach (var sg in backstory.skillGains)
            {
                string sign = sg.amount >= 0 ? "+" : "";
                parts.Add($"{sign}{sg.amount} {sg.skill.label}");
            }
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Formats disabled work types from a BackstoryDef.
        /// e.g., "Violent, Caring"
        /// </summary>
        private static string FormatDisabledWork(BackstoryDef backstory)
        {
            if (backstory == null || backstory.workDisables == WorkTags.None)
                return "";

            var disabled = new List<string>();
            foreach (WorkTags tag in Enum.GetValues(typeof(WorkTags)))
            {
                if (tag == WorkTags.None) continue;
                if ((backstory.workDisables & tag) != 0)
                {
                    disabled.Add(tag.ToString());
                }
            }
            return disabled.Count > 0 ? string.Join(", ", disabled) : "";
        }
    }
}


