using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimSynapse.Comps;

namespace RimSynapse.Psychology.UI
{
    public class Dialog_PawnPsychology : Window
    {
        private Pawn pawn;
        private SynapseCorePawnComp coreComp;
        private Vector2 scrollPosition = Vector2.zero;
        
        public override Vector2 InitialSize => new Vector2(600f, 700f);

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

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), $"{pawn.Name.ToStringShort}'s Psychological Profile");
            Text.Font = GameFont.Small;
            
            float curY = 40f;
            
            // --- TRAITS & ARCHETYPES ---
            Rect traitsRect = new Rect(0f, curY, inRect.width, 30f);
            string traitsLabel = coreComp.llmTraits.Any() 
                ? string.Join(" • ", coreComp.llmTraits) 
                : "No psychological traits identified yet.";
            
            GUI.color = new Color(0.7f, 0.7f, 1f);
            Widgets.Label(traitsRect, traitsLabel);
            GUI.color = Color.white;
            curY += 35f;

            // --- BACKSTORY ---
            string backstory = string.IsNullOrEmpty(coreComp.dynamicBackstory) 
                ? "The depths of this pawn's mind remain a mystery. More time is needed to form a psychological profile." 
                : coreComp.dynamicBackstory;

            Rect backstoryRect = new Rect(0f, curY, inRect.width, Text.CalcHeight(backstory, inRect.width));
            Widgets.Label(backstoryRect, backstory);
            curY += backstoryRect.height + 15f;

            Widgets.DrawLineHorizontal(0f, curY, inRect.width);
            curY += 15f;

            // --- JOURNAL MEMORIES ---
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 30f), "Core Memories & Journal");
            Text.Font = GameFont.Small;
            curY += 35f;

            Rect journalOutRect = new Rect(0f, curY, inRect.width, inRect.height - curY);
            
            if (coreComp.memories.Count == 0)
            {
                Widgets.Label(journalOutRect, "No memories recorded yet.");
                return;
            }

            // Calculate total height of the journal entries
            float viewHeight = 0f;
            foreach (var memory in coreComp.memories.OrderByDescending(m => m.gameTick))
            {
                string text = memory.summary;
                viewHeight += Text.CalcHeight(text, journalOutRect.width - 20f) + 30f; // Add space for date/tags
            }

            Rect journalViewRect = new Rect(0f, 0f, journalOutRect.width - 16f, viewHeight);
            
            Widgets.BeginScrollView(journalOutRect, ref scrollPosition, journalViewRect);
            float currentEntryY = 0f;
            
            foreach (var memory in coreComp.memories.OrderByDescending(m => m.gameTick))
            {
                // Format Date
                int ticks = memory.gameTick;
                int day = GenDate.DayOfYear(ticks, Find.WorldGrid.LongLatOf(pawn.Map.Tile).x);
                Quadrum quadrum = GenDate.Quadrum(ticks, Find.WorldGrid.LongLatOf(pawn.Map.Tile).x);
                int year = GenDate.Year(ticks, Find.WorldGrid.LongLatOf(pawn.Map.Tile).x);
                string dateStr = $"{day.ToString()} of {quadrum.Label()}, {year}";

                // Format Tags
                string tagsStr = memory.tags.Any() ? $"[{string.Join("] [", memory.tags)}]" : "[Misc]";

                // Draw Date and Tags
                Rect dateRect = new Rect(0f, currentEntryY, journalViewRect.width, 20f);
                GUI.color = Color.gray;
                Widgets.Label(dateRect, $"{dateStr}   {tagsStr}");
                GUI.color = Color.white;
                currentEntryY += 20f;

                // Draw Journal Text
                float textHeight = Text.CalcHeight(memory.summary, journalViewRect.width);
                Rect textRect = new Rect(0f, currentEntryY, journalViewRect.width, textHeight);
                GUI.color = new Color(1f, 1f, 1f, 0.4f + (memory.weight * 0.6f)); // Dimmer if weight is low
                Widgets.Label(textRect, memory.summary);
                GUI.color = Color.white;

                currentEntryY += textHeight + 10f;
            }
            
            Widgets.EndScrollView();
        }
    }
}
