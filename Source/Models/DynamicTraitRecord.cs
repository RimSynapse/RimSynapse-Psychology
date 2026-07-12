using RimWorld;
using Verse;

namespace RimSynapse.Psychology.Models
{
    public class DynamicTraitRecord : IExposable
    {
        public TraitDef traitDef;
        public int tickAdded;
        public string reason;

        public DynamicTraitRecord() { }

        public DynamicTraitRecord(TraitDef traitDef, int tickAdded, string reason)
        {
            this.traitDef = traitDef;
            this.tickAdded = tickAdded;
            this.reason = reason;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref traitDef, "traitDef");
            Scribe_Values.Look(ref tickAdded, "tickAdded");
            Scribe_Values.Look(ref reason, "reason");
        }
    }
}
