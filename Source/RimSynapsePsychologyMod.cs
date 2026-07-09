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
            
            Log.Message("[RimSynapse-Psychology] Mod initialized.");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            listingStandard.Label("Note: Debug logging is now globally configured in RimSynapse Core settings.");
            
            listingStandard.GapLine();
            listingStandard.Label("Content Settings");
            listingStandard.CheckboxLabeled("Enable Suicidal Mental Breaks", ref Settings.enableSuicidalBehaviors, "Warning: Sensitive Content. Disabling this completely removes suicidal behaviors from the mod.");

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
