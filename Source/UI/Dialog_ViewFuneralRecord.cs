using UnityEngine;
using Verse;

namespace RimSynapse.Psychology.UI
{
    public class Dialog_ViewFuneralRecord : Window
    {
        private string deceasedName;
        private string recordText;
        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(600f, 500f);

        public Dialog_ViewFuneralRecord(string deceasedName, string recordText)
        {
            this.deceasedName = deceasedName;
            this.recordText = recordText;
            this.doCloseX = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 35f), $"Funeral Record: {deceasedName}");
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(0, 40f, inRect.width);

            Rect outRect = new Rect(0, 50f, inRect.width, inRect.height - 110f);
            
            // Calculate height of the text block to size the viewRect scroll area correctly
            float textHeight = Text.CalcHeight(recordText, outRect.width - 20f);
            Rect viewRect = new Rect(0, 0, outRect.width - 20f, textHeight + 20f);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            Widgets.Label(new Rect(0, 0, viewRect.width, viewRect.height), recordText);
            Widgets.EndScrollView();

            if (Widgets.ButtonText(new Rect(inRect.width / 2f - 60f, inRect.height - 45f, 120f, 35f), "Close"))
            {
                Close();
            }
        }
    }
}
