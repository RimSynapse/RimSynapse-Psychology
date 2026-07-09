using Verse;

namespace RimSynapse.Psychology.Settings
{
    public class RimSynapsePsychologySettings : ModSettings
    {
        public bool enableDebugLogging = false;
        public float memoryDecayMultiplier = 1.0f;
        public float sensitivityThreshold = 0.5f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableDebugLogging, "enableDebugLogging", false);
            Scribe_Values.Look(ref memoryDecayMultiplier, "memoryDecayMultiplier", 1.0f);
            Scribe_Values.Look(ref sensitivityThreshold, "sensitivityThreshold", 0.5f);
            base.ExposeData();
        }
    }
}
