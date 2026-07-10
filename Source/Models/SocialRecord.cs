using System;
using Verse;

namespace RimSynapse.Psychology.Models
{
    public class SocialRecord : IExposable
    {
        public float trust = 0f;
        public float familiarity = 0f;
        public System.Collections.Generic.List<string> relationshipMemories = new System.Collections.Generic.List<string>();


        public SocialRecord()
        {
        }

        public void AddFamiliarity(float amount)
        {
            familiarity = UnityEngine.Mathf.Clamp(familiarity + amount, 0f, 100f);
        }

        public void AddTrust(float amount)
        {
            trust = UnityEngine.Mathf.Clamp(trust + amount, -100f, 100f);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref trust, "trust", 0f);
            Scribe_Values.Look(ref familiarity, "familiarity", 0f);
            Scribe_Collections.Look(ref relationshipMemories, "relationshipMemories", LookMode.Value);
            
            if (Scribe.mode == LoadSaveMode.LoadingVars && relationshipMemories == null)
            {
                relationshipMemories = new System.Collections.Generic.List<string>();
            }
        }
    }
}
