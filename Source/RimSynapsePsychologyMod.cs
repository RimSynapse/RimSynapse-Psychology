using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimSynapse.Psychology.Comps;
using RimSynapse.Psychology.Settings;
using HarmonyLib;
using UnityEngine;

namespace RimSynapse.Psychology
{
    public class RimSynapsePsychologyMod : Mod
    {
        public static RimSynapsePsychologySettings Settings;
        public static SynapseModHandle ModHandle;

        public RimSynapsePsychologyMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimSynapsePsychologySettings>();
            var harmony = new Harmony("RimSynapse.Psychology");
            harmony.PatchAll();
            
            // Register with Core
            ModHandle = SynapseCore.Register("RimSynapsePsychology", "RimSynapse Psychology");
            
            // Register opportunistic background tasks with scheduling metadata
            RimSynapse.SynapseClient.RegisterOpportunisticTask(ModHandle, "Psychology_OpportunisticMemory",
                (System.Func<bool>)API.SynapsePsychology.TriggerOpportunisticMemory,
                new RimSynapse.Internal.OpportunisticTaskConfig
                {
                    Label = "Memory Generation",
                    Description = "Generates personalized AI-written memories for colonists based on recent events.",
                    Priority = 5,
                    Weight = 2.0f,
                    CooldownTicks = 15000
                });
            RimSynapse.SynapseClient.RegisterOpportunisticTask(ModHandle, "Psychology_VisitorBackstory",
                (System.Func<bool>)API.SynapsePsychology.TriggerOpportunisticVisitorBackstory,
                new RimSynapse.Internal.OpportunisticTaskConfig
                {
                    Label = "Visitor Backstory",
                    Description = "Creates AI backstories for important NPCs during idle processing time.",
                    Priority = 2,
                    Weight = 1.0f,
                    CooldownTicks = 10000
                });
            
            RimSynapse.SynapseClient.RegisterOpportunisticTask(ModHandle, "Psychology_ProfileEvaluation",
                (System.Func<bool>)API.SynapsePsychology.TriggerOpportunisticProfileEvaluation,
                new RimSynapse.Internal.OpportunisticTaskConfig
                {
                    Label = "Clinical Evaluation",
                    Description = "Evaluates the psychological profile of colonists in the background based on their daily mood and recent events.",
                    Priority = 8, // High priority because this is core to the pawn's psychological state
                    Weight = 1.5f,
                    CooldownTicks = 5000 // Check frequently, since it only fires if a pawn is flagged
                });

            RimSynapse.SynapseClient.RegisterOpportunisticTask(ModHandle, "Psychology_LeaderBackstory",
                (System.Func<bool>)API.SynapsePsychology.TriggerLeaderBackstoryGeneration,
                new RimSynapse.Internal.OpportunisticTaskConfig
                {
                    Label = "Leader Backstory",
                    Description = "Generates AI backstories for all faction leaders (World VIPs). Required before StoryTeller can generate faction histories.",
                    Priority = 7, // High priority — Storyteller depends on these existing
                    Weight = 2.0f,
                    CooldownTicks = 5000 // Short cooldown — iterate through leaders quickly
                });
            
            Log.Message("[RimSynapse-Psychology] Mod initialized.");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            listingStandard.Label("Note: Debug logging is now globally configured in RimSynapse Core settings.");
            // ── Mechanics ───────────────────────────────────────────
            listingStandard.Label("Mechanics");
            listingStandard.GapLine();

            listingStandard.Label($"Memory Decay Speed: {Settings.memoryDecayMultiplier:F1}x", tooltip: "How fast colonists forget past events. Higher means they let go of grudges and trauma faster.");
            Settings.memoryDecayMultiplier = listingStandard.Slider(Settings.memoryDecayMultiplier, 0.1f, 5.0f);
            
            listingStandard.Label($"Sensitivity Minimum Burden Threshold: {Settings.sensitivityThreshold:F1}");
            Settings.sensitivityThreshold = listingStandard.Slider(Settings.sensitivityThreshold, 0.1f, 5.0f);

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RimSynapse - Psychology";
        }
    }

    [StaticConstructorOnStartup]
    public static class PsychologyInjector
    {
        static PsychologyInjector()
        {
            InjectComp();
        }

        private static void InjectComp()
        {
            int injectedCount = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where(d => d.race != null && d.race.Humanlike))
            {
                if (def.comps == null)
                {
                    def.comps = new List<CompProperties>();
                }
                
                // Add the SynapsePawnComp properties if it doesn't already exist
                if (!def.comps.Any(c => c.compClass == typeof(SynapsePawnComp)))
                {
                    var props = new CompProperties
                    {
                        compClass = typeof(SynapsePawnComp)
                    };
                    def.comps.Add(props);
                    injectedCount++;
                }
            }
            
            Log.Message($"[RimSynapse-Psychology] Injected SynapsePawnComp into {injectedCount} humanlike ThingDefs.");
        }
    }
}
