using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimSynapse.Comps;
using RimSynapse.Psychology.Comps;
using RimSynapse.Utils;

namespace RimSynapse.Psychology.UI
{
    public class Dialog_PawnPsychology : Window
    {
        private enum PsychologyTab
        {
            Profile,
            Memories,
            SocialNetwork
        }

        private Pawn pawn;
        private SynapseCorePawnComp coreComp;
        private PsychologyTab currentTab = PsychologyTab.Profile;
        
        private Vector2 memoriesScrollPosition = Vector2.zero;
        private Vector2 profileScrollPosition = Vector2.zero;
        private Vector2 socialScrollPosition = Vector2.zero;
        private Dictionary<string, Pawn> cachedTrustPawns = null;
        
        public override Vector2 InitialSize => new Vector2(650f, 750f);

        public Dialog_PawnPsychology(Pawn pawn)
        {
            this.pawn = pawn;
            this.coreComp = pawn.TryGetComp<SynapseCorePawnComp>();
            this.forcePause = false;
            this.doCloseX = true;
            this.draggable = true;
            this.resizeable = true;
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
            Text.Anchor = TextAnchor.MiddleCenter;

            string fullName = pawn.LabelShort;
            if (pawn.Name is NameTriple nameTriple)
            {
                if (!string.IsNullOrEmpty(nameTriple.Nick))
                {
                    fullName = $"{nameTriple.First} \"{nameTriple.Nick}\" {nameTriple.Last}";
                }
                else
                {
                    fullName = $"{nameTriple.First} {nameTriple.Last}";
                }
            }
            else if (pawn.Name != null)
            {
                fullName = pawn.Name.ToStringFull;
            }

            Widgets.Label(headerRect, fullName);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Force Review Button removed from here

            // Setup Tab space
            Rect tabRect = new Rect(0f, headerRect.yMax + 30f, inRect.width, inRect.height - headerRect.yMax - 30f);
            
            List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("Psychological Profile", () => currentTab = PsychologyTab.Profile, currentTab == PsychologyTab.Profile),
                new TabRecord("Memories", () => currentTab = PsychologyTab.Memories, currentTab == PsychologyTab.Memories),
                new TabRecord("Social Network", () => currentTab = PsychologyTab.SocialNetwork, currentTab == PsychologyTab.SocialNetwork)
            };
            
            TabDrawer.DrawTabs(tabRect, tabs, 200f);

            // Draw the inner content window
            Rect contentRect = tabRect.ContractedBy(18f);
            
            switch (currentTab)
            {
                case PsychologyTab.Profile:
                    DrawProfileTab(contentRect);
                    break;
                case PsychologyTab.Memories:
                    DrawMemoriesTab(contentRect);
                    break;
                case PsychologyTab.SocialNetwork:
                    DrawSocialNetworkTab(contentRect);
                    break;
            }
        }

        private void DrawProfileTab(Rect rect)
        {
            float curY = rect.y;

            // Draw Portrait
            Rect portraitRect = new Rect(rect.x, curY, 150f, 150f);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawn, new Vector2(150f, 150f), Rot4.South));
            
            // Force Review Button (moved below portrait)
            Rect forceReviewRect = new Rect(rect.x, portraitRect.yMax + 10f, 150f, 30f);
            if (Widgets.ButtonText(forceReviewRect, "Force Psych Review"))
            {
                var cc = pawn.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
                var pc = pawn.TryGetComp<SynapsePawnComp>();
                
                if (cc != null && pc != null)
                {
                    float mood = pawn.needs?.mood?.CurLevelPercentage ?? 0.5f;
                    var recentEvents = cc.memories
                        .Where(m => Find.TickManager.TicksAbs - m.absTick < 60000)
                        .ToList();
                        
                    Messages.Message($"Queueing immediate psychological review for {pawn.LabelShort}...", MessageTypeDefOf.NeutralEvent, false);
                    
                    RimSynapse.Psychology.API.SynapsePsychology.QueueDailyPsychologyReview(pawn, mood, recentEvents, (success) => {
                        if (success) 
                        {
                            Messages.Message($"Completed psychological review for {pawn.LabelShort}.", MessageTypeDefOf.PositiveEvent, false);
                            pc.lastJournalUpdateDay = GenDate.DaysPassed; // Reset cooldown
                        }
                    }, true);
                }
            }
            
            // Draw Traits next to Portrait
            Rect traitsHeaderRect = new Rect(portraitRect.xMax + 20f, curY + 10f, rect.width - portraitRect.width - 20f, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(traitsHeaderRect, "Psychological Archetypes");
            Text.Font = GameFont.Small;
            
            float bulletY = traitsHeaderRect.yMax + 5f;
            GUI.color = new Color(0.7f, 0.7f, 1f);
            if (coreComp.llmTraits.Any())
            {
                foreach (var trait in coreComp.llmTraits)
                {
                    Rect bulletRect = new Rect(portraitRect.xMax + 20f, bulletY, traitsHeaderRect.width, 24f);
                    Widgets.Label(bulletRect, $"• {trait}");
                    bulletY += 24f;
                }
            }
            else
            {
                Rect bulletRect = new Rect(portraitRect.xMax + 20f, bulletY, traitsHeaderRect.width, 40f);
                Widgets.Label(bulletRect, "• Awaiting LLM psychological evaluation...");
            }
            GUI.color = Color.white;
            
            curY = Mathf.Max(forceReviewRect.yMax + 30f, bulletY + 20f);
            
            Widgets.DrawLineHorizontal(rect.x, curY, rect.width);
            curY += 15f;

            // Draw Daily Evaluation (Medical Form) Header
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, curY, rect.width, 30f), "Medical Evaluation");
            Text.Font = GameFont.Small;
            curY += 35f;
            
            var pawnComp = pawn.TryGetComp<SynapsePawnComp>();
            if (pawnComp == null) return;
            
            string[] formFields = { "Mood", "Interpersonal", "Trauma", "Cognitive", "Motivations", "Identity", "Morality", "Authority", "Addiction" };
            string[] formLabels = { "Mood & Affect", "Interpersonal Dynamics", "Trauma & PTSD", "Cognitive Distortions", "Core Motivations", "Identity & Self-Image", "Moral Alignment", "Authority & Compliance", "Addiction & Dependencies" };
            
            float remainingHeight = rect.yMax - curY;
            Rect viewRect = new Rect(rect.x, curY, rect.width - 20f, 1000f); 
            Widgets.BeginScrollView(new Rect(rect.x, curY, rect.width, remainingHeight), ref profileScrollPosition, viewRect);
            
            float formY = curY;

            // Draw Summary inside Scroll View (Top)
            GUI.color = new Color(0.7f, 0.9f, 1f);
            Rect headerRectAssess = new Rect(rect.x, formY, viewRect.width, 22f);
            Widgets.Label(headerRectAssess, "<b>Summary / Prognosis</b>");
            GUI.color = Color.white;
            formY += 22f;

            string initialAssessment = pawnComp.medicalProfile.TryGetValue("Summary", out string sVal) ? sVal : "Awaiting clinical assessment...";
            float initAssessHeight = Text.CalcHeight(initialAssessment, viewRect.width);
            Widgets.Label(new Rect(rect.x, formY, viewRect.width, initAssessHeight), initialAssessment);
            formY += initAssessHeight + 10f;
            
            Widgets.DrawLineHorizontal(rect.x, formY, viewRect.width);
            formY += 10f;

            // Draw Daily Evaluation Fields
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

        private void DrawMemoriesTab(Rect rect)
        {
            if (coreComp.memories.Count == 0)
            {
                Widgets.Label(rect, "No memories recorded yet.");
                return;
            }

            // Sort chronologically (oldest first) using absTick
            var sortedMemories = coreComp.memories.OrderBy(m => m.absTick).ToList();

            float viewHeight = 0f;
            foreach (var memory in sortedMemories)
            {
                viewHeight += Text.CalcHeight(memory.summary, rect.width - 20f) + 30f;
            }

            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, viewHeight);
            
            Widgets.BeginScrollView(rect, ref memoriesScrollPosition, viewRect);
            float currentEntryY = 0f;
            
            // Get longitude for date formatting (use 0 if pawn has no map)
            float longitude = pawn.Map != null ? Find.WorldGrid.LongLatOf(pawn.Map.Tile).x : 0f;
            
            foreach (var memory in sortedMemories)
            {
                // Use absTick for proper date rendering
                long ticks = memory.absTick;
                string dateStr = SynapseDateHelper.FormatAbsTick(ticks, longitude);
                
                // Calculate pawn's age at this memory
                int ageAtMemory = SynapseDateHelper.GetAgeAtAbsTick(pawn, ticks);
                string ageStr = ageAtMemory >= 0 ? $"Age {ageAtMemory}" : "";

                string tagsStr = memory.tags.Any() ? $"[{string.Join("] [", memory.tags)}]" : "[Misc]";

                Rect dateRect = new Rect(0f, currentEntryY, viewRect.width, 20f);
                GUI.color = Color.gray;
                string headerLine = !string.IsNullOrEmpty(ageStr) 
                    ? $"{dateStr}  ({ageStr})   {tagsStr}" 
                    : $"{dateStr}   {tagsStr}";
                Widgets.Label(dateRect, headerLine);
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

        private void DrawSocialNetworkTab(Rect rect)
        {
            var pawnComp = pawn.TryGetComp<SynapsePawnComp>();
            if (pawnComp == null || pawnComp.socialNetwork == null || pawnComp.socialNetwork.Count == 0)
            {
                Widgets.Label(rect, "No social relationships recorded yet.");
                return;
            }

            if (cachedTrustPawns == null)
            {
                cachedTrustPawns = new Dictionary<string, Pawn>();
                var allPawns = (pawn.Map?.mapPawns?.AllPawnsSpawned ?? Enumerable.Empty<Pawn>())
                    .Concat(Find.WorldPawns.AllPawnsAliveOrDead);
                foreach (var kvp in pawnComp.socialNetwork)
                {
                    Pawn target = allPawns.FirstOrDefault(p => p.GetUniqueLoadID() == kvp.Key);
                    if (target != null) cachedTrustPawns[kvp.Key] = target;
                }
            }

            float viewHeight = 0f;
            foreach (var kvp in pawnComp.socialNetwork)
            {
                viewHeight += 60f;
                if (kvp.Value.relationshipMemories.Count > 0)
                {
                    string mem = kvp.Value.relationshipMemories.Last();
                    viewHeight += Text.CalcHeight($"\"{mem}\"", rect.width - 90f) + 10f;
                }
            }

            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, viewHeight);
            
            Widgets.BeginScrollView(rect, ref socialScrollPosition, viewRect);
            
            float curY = 0f;
            foreach (var kvp in pawnComp.socialNetwork)
            {
                string loadId = kvp.Key;
                var record = kvp.Value;
                
                cachedTrustPawns.TryGetValue(loadId, out Pawn targetPawn);
                
                Rect entryRect = new Rect(0f, curY, viewRect.width, 50f);
                
                if (targetPawn != null)
                {
                    Rect portraitRect = new Rect(0f, curY, 50f, 50f);
                    GUI.DrawTexture(portraitRect, PortraitsCache.Get(targetPawn, new Vector2(50f, 50f), Rot4.South));
                    
                    Rect nameRect = new Rect(60f, curY + 5f, 200f, 20f);
                    Text.Font = GameFont.Small;
                    Widgets.Label(nameRect, targetPawn.Name.ToStringShort);
                }
                else
                {
                    Rect nameRect = new Rect(60f, curY + 5f, 200f, 20f);
                    Text.Font = GameFont.Small;
                    Widgets.Label(nameRect, $"Unknown ({loadId})");
                }
                
                // Get Affinity (Opinion)
                int affinity = targetPawn != null && targetPawn.relations != null ? pawn.relations.OpinionOf(targetPawn) : 0;
                
                // Draw Metrics
                Text.Font = GameFont.Tiny;
                
                // Trust
                Rect trustRect = new Rect(250f, curY + 5f, 100f, 20f);
                if (record.trust >= 50) GUI.color = Color.green;
                else if (record.trust >= 10) GUI.color = new Color(0.6f, 0.9f, 0.6f);
                else if (record.trust > -10) GUI.color = Color.white;
                else if (record.trust > -50) GUI.color = new Color(0.9f, 0.6f, 0.6f);
                else GUI.color = Color.red;
                Widgets.Label(trustRect, $"Trust: {record.trust:F0}");
                
                // Familiarity
                Rect famRect = new Rect(360f, curY + 5f, 100f, 20f);
                GUI.color = new Color(0.6f + (record.familiarity/100f)*0.4f, 0.6f + (record.familiarity/100f)*0.4f, 1f);
                Widgets.Label(famRect, $"Familiarity: {record.familiarity:F0}");
                
                // Affinity
                Rect affRect = new Rect(470f, curY + 5f, 100f, 20f);
                if (affinity >= 50) GUI.color = Color.green;
                else if (affinity >= 10) GUI.color = new Color(0.6f, 0.9f, 0.6f);
                else if (affinity > -10) GUI.color = Color.white;
                else if (affinity > -50) GUI.color = new Color(0.9f, 0.6f, 0.6f);
                else GUI.color = Color.red;
                Widgets.Label(affRect, $"Affinity: {affinity}");
                
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                
                float extraHeight = 0f;
                if (record.relationshipMemories.Count > 0)
                {
                    string mem = record.relationshipMemories.Last();
                    float memHeight = Text.CalcHeight($"\"{mem}\"", viewRect.width - 70f);
                    Rect memRect = new Rect(60f, curY + 25f, viewRect.width - 70f, memHeight);
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.8f, 0.8f, 0.8f);
                    Widgets.Label(memRect, $"\"{mem}\"");
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    extraHeight = memHeight + 10f;
                }
                
                curY += 60f + extraHeight;
            }
            
            Widgets.EndScrollView();
        }
    }
}
