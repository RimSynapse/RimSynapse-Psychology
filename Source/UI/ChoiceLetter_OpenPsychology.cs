using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimSynapse.Psychology.UI
{
    public class ChoiceLetter_OpenPsychology : ChoiceLetter
    {
        public Pawn targetPawn;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly)
                {
                    yield return base.Option_Close;
                    yield break;
                }

                var openOption = new DiaOption("Open Backstory");
                openOption.action = () =>
                {
                    if (targetPawn != null && !targetPawn.Dead)
                    {
                        Find.WindowStack.Add(new Dialog_PawnPsychology(targetPawn));
                        Find.LetterStack.RemoveLetter(this);
                    }
                };
                openOption.resolveTree = true;
                yield return openOption;

                yield return base.Option_Close;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref targetPawn, "targetPawn");
        }
    }
}
