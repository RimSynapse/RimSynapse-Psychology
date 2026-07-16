using System;
using System.Linq;
using System.Collections.Generic;
using Verse;
using RimWorld;
using RimSynapse.Psychology.Comps;
using RimSynapse.Models;
using Newtonsoft.Json;

namespace RimSynapse.Psychology.API
{
    /// <summary>
    /// Nightly clinical evaluation: Queues the pawn's daily mood, memories, and statistics
    /// for LLM processing to update their long-term psychological profile.
    /// </summary>
    public static partial class SynapsePsychology
    {
        /// <summary>
        /// Triggered when the pawn goes to sleep. Queues their daily events and average mood
        /// for LLM processing to update long-term context modifiers and break severity.
        /// </summary>
        public static void QueueDailyPsychologyReview(Pawn pawn, float averageMood, System.Collections.Generic.List<RimSynapse.Models.WeightedMemory> dailyEvents, Action<bool> onComplete = null, bool isOpportunistic = false)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var pawnComp = pawn.TryGetComp<SynapsePawnComp>();
            var coreComp = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (pawnComp == null || coreComp == null) return;

            string systemPrompt = @"You are a clinical psychologist in the RimWorld universe writing a formal medical evaluation.
Based on this colonist's average mood today, their recent memories, their survival skills, and the state of the colony, assess the following 9 categories (write 1-2 sentences each), and then provide a final Summary:
- Mood (Emotional baseline, volatility, and general disposition)
- Interpersonal (Attachment style, sociability, relationships with others)
- Trauma (Lingering psychological effects of past horrors, deaths, or recent brutal raids)
- Cognitive (Paranoia, grandiosity, irrational fears, or hyper-fixations)
- Motivations (What keeps them going: duty, survival, greed, ideology?)
- Identity (How they view themselves and their role within the colony)
- Morality (Capacity for cruelty versus compassion: e.g., handling prisoners, organ harvesting)
- Authority (Rebelliousness versus obedience to colony leadership and drafted orders)
- Addiction (Psychological reliance on substances or specific comforts)
- Summary (The final overarching clinical assessment and prognosis)

You MUST analyze the 'Tags' attached to their recent memories. If you see recurring themes (e.g. 'Food', 'Starving', 'Safety'), you must explicitly address this growing concern in your evaluation.

You must also provide an 'AbandonmentRiskScore' (0-100) representing how likely they are to permanently abandon the colony (or rebel if a slave). High survival skills and low satisfaction increase this risk.

Finally, output a 'SocialAdjustments' object reflecting changes in their Trust (-100 to +100) and Familiarity (0 to 100) with other colonists based on recent events. Use the colonist's short name as the key. Trust offsets should be between -15 and +15. Familiarity offsets should be positive (0 to +10).

Additionally, evaluate if their recent experiences are profound enough to change their personality traits. If so, return a 'TraitChanges' object with an 'Add' array (containing RimWorld trait defNames to add, e.g. 'Bloodlust', 'Nerves') and/or a 'Remove' array (trait defNames to remove). Keep this rare; leave arrays empty if no profound change occurred.
The colonist currently has the following dynamically added traits (which you previously added): {DYNAMIC_TRAITS}. If you determine the pawn has moved past the psychological phase that caused these traits, you should include them in the 'Remove' array so they can decay.

You MUST respond strictly in valid JSON format. Do not include markdown formatting or extra text.
{
  ""Mood"": ""1-2 sentences..."",
  ""Interpersonal"": ""1-2 sentences..."",
  ""Trauma"": ""1-2 sentences..."",
  ""Cognitive"": ""1-2 sentences..."",
  ""Motivations"": ""1-2 sentences..."",
  ""Identity"": ""1-2 sentences..."",
  ""Morality"": ""1-2 sentences..."",
  ""Authority"": ""1-2 sentences..."",
  ""Addiction"": ""1-2 sentences..."",
  ""Summary"": ""1-2 sentences..."",
  ""AbandonmentRiskScore"": 0,
  ""TraitChanges"": {
    ""Add"": [""Bloodlust""],
    ""Remove"": [""Kind""]
  },
  ""SocialAdjustments"": {
    ""ColonistName1"": {
      ""trustOffset"": 2.5,
      ""familiarityOffset"": 1.0
    }
  }
}";

            string dlcInstructions = "";
            if (ModsConfig.RoyaltyActive)
            {
                dlcInstructions += "\n- Royalty DLC: If the colonist recently gained a title or cast psycasts, evaluate if this develops a royal entitlement complex, class friction, or mental fatigue.";
            }
            if (ModsConfig.IdeologyActive)
            {
                dlcInstructions += "\n- Ideology DLC: If the colonist recently witnessed a ritual or conversion, evaluate if this triggers a crisis of faith or deepens their fanaticism/zealotry.";
            }
            if (ModsConfig.BiotechActive)
            {
                dlcInstructions += "\n- Biotech DLC: If the colonist gave birth, was vat-grown, or received gene enhancements, reflect their physical/genetic identity dysphoria or parentage complexes.";
            }
            if (ModsConfig.AnomalyActive)
            {
                dlcInstructions += "\n- Anomaly DLC: If the colonist has void/entity exposure tags in their memories, let it warp their cognitive profile towards void obsession or paranoia.";
            }
            if (pawn.Map != null && pawn.Map.Biome != null && pawn.Map.Biome.defName.IndexOf("Space", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                dlcInstructions += "\n- Save Our Ship 2: The colonist is currently in orbit/space. Reflect the psychological impact of cosmic isolation and void melancholy.";
            }

            if (!string.IsNullOrEmpty(dlcInstructions))
            {
                systemPrompt = systemPrompt.Replace("You MUST analyze the 'Tags' attached to their recent memories.",
                    "You MUST analyze the 'Tags' attached to their recent memories." + dlcInstructions);
            }

            string recentEvents = dailyEvents == null || dailyEvents.Count == 0 
                ? "No significant memories today." 
                : string.Join("\n", dailyEvents.Select(e => $"- {e.summary} [Tags: {string.Join(", ", e.tags)}]"));

            float threshold = RimSynapsePsychologyMod.Settings != null ? RimSynapsePsychologyMod.Settings.sensitivityThreshold : 0.5f;
            string lifetimeBurdens = coreComp.GetTopMemoryBurdens(5, threshold);

            int colonySize = pawn.Map?.mapPawns?.FreeColonistsCount ?? 1;
            int melee = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            int shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
            int cooking = pawn.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? 0;
            int medicine = pawn.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
            
            int lifetimeKills = pawn.records?.GetAsInt(RecordDefOf.KillsHumanlikes) ?? 0;
            float damageTaken = pawn.records?.GetValue(RecordDefOf.DamageTaken) ?? 0f;
            float timeAsColonist = (pawn.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) / 60000f; // in days

            string statusText = pawn.IsColonist ? "Colonist" : (pawn.IsPrisoner ? "Prisoner" : (pawn.IsSlave ? "Slave" : "Guest"));
            NeedDef suppressionDef = DefDatabase<NeedDef>.GetNamedSilentFail("Suppression");
            string suppression = (pawn.IsSlave && suppressionDef != null) ? $"\nSuppression: {pawn.needs?.TryGetNeed(suppressionDef)?.CurLevelPercentage:P0}" : "";

            // Initialize missing colonists in social network
            if (pawn.Map != null)
            {
                foreach (Pawn p in pawn.Map.mapPawns.FreeColonists)
                {
                    if (p != pawn && !pawnComp.socialNetwork.ContainsKey(p.GetUniqueLoadID()))
                    {
                        pawnComp.socialNetwork[p.GetUniqueLoadID()] = new RimSynapse.Psychology.Models.SocialRecord();
                    }
                }
            }

            string socialNetworkStr = "None";
            if (pawnComp.socialNetwork.Count > 0)
            {
                var allPawns = (pawn.Map?.mapPawns?.AllPawnsSpawned ?? Enumerable.Empty<Pawn>()).Concat(Find.WorldPawns.AllPawnsAliveOrDead);
                var socialLines = new List<string>();
                foreach (var kvp in pawnComp.socialNetwork)
                {
                    Pawn target = allPawns.FirstOrDefault(p => p.GetUniqueLoadID() == kvp.Key);
                    if (target != null)
                    {
                        int affinity = target.relations != null ? pawn.relations.OpinionOf(target) : 0;
                        socialLines.Add($"- {target.Name.ToStringShort} ({target.gender}): Trust {kvp.Value.trust:F0}, Familiarity {kvp.Value.familiarity:F0}, Affinity {affinity}");
                    }
                }
                if (socialLines.Count > 0)
                {
                    socialNetworkStr = string.Join("\n", socialLines);
                }
            }

            string dynamicTraitsStr = "None";
            if (pawnComp.dynamicTraits.Count > 0)
            {
                dynamicTraitsStr = string.Join(", ", pawnComp.dynamicTraits.Select(t => $"{t.traitDef.defName} (Added {((Find.TickManager.TicksGame - t.tickAdded)/60000f):F1} days ago for: {t.reason})"));
            }
            systemPrompt = systemPrompt.Replace("{DYNAMIC_TRAITS}", dynamicTraitsStr);

            string userMessage = $@"Patient Name: {pawn.Name.ToStringShort}
Gender: {pawn.gender}
Status: {statusText}
Colony Size: {colonySize}
Time as Colonist: {timeAsColonist:F1} days
Survival Stats: Melee {melee}, Shooting {shooting}, Medicine {medicine}, Lifetime Human Kills: {lifetimeKills}, Damage Taken: {damageTaken:F0}
Average Mood Today: {averageMood:F2}{suppression}
Psychological Burdens (Sensitivity): {lifetimeBurdens}
Current Social Network (Trust, Familiarity, Affinity):
{socialNetworkStr}
Recent Memories:
{recentEvents}";

            var options = new ChatOptions { priority = isOpportunistic ? -1 : 0, requestName = "Daily Psychology Review", targetName = pawn.Name.ToStringShort };

            SynapseClient.PromptAsync(
                RimSynapsePsychologyMod.ModHandle,
                systemPrompt,
                userMessage,
                result => ParseEvaluationResult(result, pawn, pawnComp, onComplete, sw),
                options
            );
        }
    }
}

