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

        public RimSynapsePsychologyMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimSynapsePsychologySettings>();
            var harmony = new Harmony("RimSynapse.Psychology");
            harmony.PatchAll();
            Log.Message("[RimSynapse-Psychology] Mod initialized.");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            listingStandard.CheckboxLabeled("Enable Debug Logging", ref Settings.enableDebugLogging, "Dumps all AI psychology updates and events to a text file.");

            if (listingStandard.ButtonText("Open Log Folder"))
            {
                string path = System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, "RimSynapse_Logs", "Psychology");
                if (System.IO.Directory.Exists(path))
                {
                    Application.OpenURL("file://" + path);
                }
                else
                {
                    Messages.Message("Log folder does not exist yet.", MessageTypeDefOf.RejectInput, false);
                }
            }

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
