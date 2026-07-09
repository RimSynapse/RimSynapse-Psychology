using Verse;

namespace RimSynapse.Psychology.Settings
{
    public class RimSynapsePsychologySettings : ModSettings
    {
        public bool enableDebugLogging = false;
        public bool enableSuicidalBehaviors = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableDebugLogging, "enableDebugLogging", false);
            Scribe_Values.Look(ref enableSuicidalBehaviors, "enableSuicidalBehaviors", true);
            base.ExposeData();
        }
    }
}
