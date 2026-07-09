using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimSynapse.Comps;
using RimSynapse.Psychology.Comps;

namespace RimSynapse.Psychology.UI
{
    public class Dialog_PawnPsychology : Window
    {
        private enum PsychologyTab
        {
            Profile,
            Backstory,
            Journal
        }

        private Pawn pawn;
        private SynapseCorePawnComp coreComp;
        private PsychologyTab currentTab = PsychologyTab.Profile;
        
        private Vector2 backstoryScrollPosition = Vector2.zero;
        private Vector2 journalScrollPosition = Vector2.zero;
        private Vector2 profileScrollPosition = Vector2.zero;
        
        public override Vector2 InitialSize => new Vector2(650f, 750f);
        protected override float Margin => 0f;

        public Dialog_PawnPsychology(Pawn pawn)
        {
            this.pawn = pawn;
            this.coreComp = pawn.TryGetComp<SynapseCorePawnComp>();
            this.forcePause = false;
            this.doCloseX = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (pawn == null || coreComp == null) return;

            // Draw header area
            Rect headerRect = new Rect(0f, 0f, inRect.width, 45f);
            Widgets.DrawWindowBackground(headerRect);
            
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, $"{pawn.Name.ToStringShort}'s Psychological Profile");
            Text.Font = GameFont.Small;

            // Setup Tab space
            Rect tabRect = new Rect(0f, headerRect.yMax + 30f, inRect.width, inRect.height - headerRect.yMax - 30f);
            
            List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("Profile", () => currentTab = PsychologyTab.Profile, currentTab == PsychologyTab.Profile),
                new TabRecord("Backstory", () => currentTab = PsychologyTab.Backstory, currentTab == PsychologyTab.Backstory),
                new TabRecord("Journal", () => currentTab = PsychologyTab.Journal, currentTab == PsychologyTab.Journal)
            };
            
            TabDrawer.DrawTabs(tabRect, tabs, 200f);

            // Draw the inner content window
            Rect contentRect = tabRect.ContractedBy(18f);
            
            switch (currentTab)
            {
                case PsychologyTab.Profile:
                    DrawProfileTab(contentRect);
                    break;
                case PsychologyTab.Backstory:
                    DrawBackstoryTab(contentRect);
                    break;
                case PsychologyTab.Journal:
                    DrawJournalTab(contentRect);
                    break;
            }
        }

        private void DrawProfileTab(Rect rect)
        {
            float curY = rect.y;

            // Draw Portrait
            Rect portraitRect = new Rect(rect.x, curY, 150f, 150f);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawn, new Vector2(150f, 150f), Rot4.South));
            
            // Draw Traits next to Portrait
            Rect traitsHeaderRect = new Rect(portraitRect.xMax + 20f, curY + 10f, rect.width - portraitRect.width - 20f, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(traitsHeaderRect, "Psychological Archetypes");
            Text.Font = GameFont.Small;
            
            Rect traitsRect = new Rect(portraitRect.xMax + 20f, traitsHeaderRect.yMax, traitsHeaderRect.width, 60f);
            string traitsLabel = coreComp.llmTraits.Any() 
                ? string.Join(" • ", coreComp.llmTraits) 
                : "Awaiting LLM psychological evaluation...";
            GUI.color = new Color(0.7f, 0.7f, 1f);
            Widgets.Label(traitsRect, traitsLabel);
            GUI.color = Color.white;
            
            curY = portraitRect.yMax + 30f;
            
            Widgets.DrawLineHorizontal(rect.x, curY, rect.width);
            curY += 15f;

            // Draw Clinical Assessment (Medical Form)
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, curY, rect.width, 30f), "Medical Evaluation");
            Text.Font = GameFont.Small;
            curY += 35f;
            
            var pawnComp = pawn.TryGetComp<SynapsePawnComp>();
            if (pawnComp == null) return;
            
            string[] formFields = { "Relationships", "Trauma", "ShapingEvents", "Disorders", "Satisfaction", "Fulfillment", "Arrogance", "Dedication" };
            string[] formLabels = { "Relationships", "Trauma", "Shaping Events", "Disorders", "Satisfaction", "Fulfillment", "Arrogance", "Dedication" };
            
            float remainingHeight = rect.yMax - curY;
            Rect viewRect = new Rect(rect.x, curY, rect.width - 20f, 1000f); 
            Widgets.BeginScrollView(new Rect(rect.x, curY, rect.width, remainingHeight), ref profileScrollPosition, viewRect);
            
            float formY = curY;
            for (int i = 0; i < formFields.Length; i++)
            {
                string key = formFields[i];
                string label = formLabels[i];
                string val = pawnComp.medicalProfile.TryGetValue(key, out string v) ? v : "[Awaiting Evaluation]";
                
                // Draw Header
                GUI.color = new Color(0.7f, 0.9f, 1f);
                Text.Font = GameFont.Small;
                Rect headerRect = new Rect(rect.x, formY, viewRect.width, 22f);
                Widgets.Label(headerRect, $"<b>{label}</b>");
                GUI.color = Color.white;
                formY += 22f;
                
                // Draw Value
                float textHeight = Text.CalcHeight(val, viewRect.width);
                Rect valRect = new Rect(rect.x, formY, viewRect.width, textHeight);
                Widgets.Label(valRect, val);
                formY += textHeight + 10f;
                
                // Draw divider
                Widgets.DrawLineHorizontal(rect.x, formY, viewRect.width);
                formY += 10f;
            }
            
            Widgets.EndScrollView();
        }

        private void DrawBackstoryTab(Rect rect)
        {
            string backstory = string.IsNullOrEmpty(coreComp.dynamicBackstory) 
                ? "The depths of this pawn's mind remain a mystery. More time is needed to form a psychological profile." 
                : coreComp.dynamicBackstory;

            float textHeight = Text.CalcHeight(backstory, rect.width - 20f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, textHeight);
            
            Widgets.BeginScrollView(rect, ref backstoryScrollPosition, viewRect);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, viewRect.height), backstory);
            Widgets.EndScrollView();
        }

        private void DrawJournalTab(Rect rect)
        {
            if (coreComp.memories.Count == 0)
            {
                Widgets.Label(rect, "No memories recorded yet.");
                return;
            }

            float viewHeight = 0f;
            foreach (var memory in coreComp.memories.OrderByDescending(m => m.gameTick))
            {
                viewHeight += Text.CalcHeight(memory.summary, rect.width - 20f) + 30f;
            }

            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, viewHeight);
            
            Widgets.BeginScrollView(rect, ref journalScrollPosition, viewRect);
            float currentEntryY = 0f;
            
            foreach (var memory in coreComp.memories.OrderByDescending(m => m.gameTick))
            {
                int ticks = memory.gameTick;
                int day = GenDate.DayOfYear(ticks, Find.WorldGrid.LongLatOf(pawn.Map.Tile).x);
                Quadrum quadrum = GenDate.Quadrum(ticks, Find.WorldGrid.LongLatOf(pawn.Map.Tile).x);
                int year = GenDate.Year(ticks, Find.WorldGrid.LongLatOf(pawn.Map.Tile).x);
                string dateStr = $"{day.ToString()} of {quadrum.Label()}, {year}";

                string tagsStr = memory.tags.Any() ? $"[{string.Join("] [", memory.tags)}]" : "[Misc]";

                Rect dateRect = new Rect(0f, currentEntryY, viewRect.width, 20f);
                GUI.color = Color.gray;
                Widgets.Label(dateRect, $"{dateStr}   {tagsStr}");
                GUI.color = Color.white;
                currentEntryY += 20f;

                float textHeight = Text.CalcHeight(memory.summary, viewRect.width);
                Rect textRect = new Rect(0f, currentEntryY, viewRect.width, textHeight);
                GUI.color = new Color(1f, 1f, 1f, 0.4f + (memory.weight * 0.6f));
                Widgets.Label(textRect, memory.summary);
                GUI.color = Color.white;

                currentEntryY += textHeight + 10f;
            }
            
            Widgets.EndScrollView();
        }
    }
}
