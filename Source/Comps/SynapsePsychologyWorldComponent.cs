using System;
using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace RimSynapse.Psychology.Comps
{
    public class SynapsePsychologyWorldComponent : WorldComponent
    {
        public Dictionary<string, string> funeralRecords = new Dictionary<string, string>();

        public SynapsePsychologyWorldComponent(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref funeralRecords, "funeralRecords", LookMode.Value, LookMode.Value);
            
            if (Scribe.mode == LoadSaveMode.LoadingVars && funeralRecords == null)
            {
                funeralRecords = new Dictionary<string, string>();
            }
        }
    }
}
