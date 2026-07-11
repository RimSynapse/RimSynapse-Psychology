using System.Collections.Generic;
using Verse;

namespace RimSynapse.Psychology.Models
{
    public class TherapyTranscript : IExposable
    {
        public string otherPawnName;
        public int sessionTick;
        public List<string> lines = new List<string>();

        public TherapyTranscript() { }

        public void ExposeData()
        {
            Scribe_Values.Look(ref otherPawnName, "otherPawnName");
            Scribe_Values.Look(ref sessionTick, "sessionTick");
            Scribe_Collections.Look(ref lines, "lines", LookMode.Value);

            if (lines == null) lines = new List<string>();
        }
    }
}
