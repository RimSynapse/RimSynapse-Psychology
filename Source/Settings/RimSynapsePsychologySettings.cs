using Verse;

namespace RimSynapse.Psychology.Settings
{
    public class RimSynapsePsychologySettings : ModSettings
    {
        public bool enableDebugLogging = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableDebugLogging, "enableDebugLogging", false);
            base.ExposeData();
        }
    }
}
