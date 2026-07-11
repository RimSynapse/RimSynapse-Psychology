using Verse;
using RimWorld;
using RimSynapse.Psychology.Comps;
using System.Collections.Generic;

namespace RimSynapse.Psychology.GameComponents
{
    /// <summary>
    /// Ensures that pawns in existing save games get the SynapsePawnComp safely 
    /// if the mod is added mid-playthrough. 
    /// </summary>
    public class PsychologySaveSafetyComponent : GameComponent
    {
        public PsychologySaveSafetyComponent(Game game)
        {
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            EnsureCompsOnExistingPawns();
        }

        private void EnsureCompsOnExistingPawns()
        {
            int addedCount = 0;

            // Check world pawns
            if (Find.WorldPawns != null)
            {
                foreach (Pawn pawn in Find.WorldPawns.AllPawnsAlive)
                {
                    if (TryAddCompToPawn(pawn))
                    {
                        addedCount++;
                    }
                }
            }

            // Check map pawns across all maps
            if (Find.Maps != null)
            {
                foreach (Map map in Find.Maps)
                {
                    if (map.mapPawns != null)
                    {
                        foreach (Pawn pawn in map.mapPawns.AllPawns)
                        {
                            if (!pawn.Dead && TryAddCompToPawn(pawn))
                            {
                                addedCount++;
                            }
                        }
                    }
                }
            }

            if (addedCount > 0)
            {
                RimSynapse.SynapseLogger.Info("psychology", $"[RimSynapse-Psychology] Added SynapsePawnComp to {addedCount} existing pawns from a previous save.");
            }
        }

        private bool TryAddCompToPawn(Pawn pawn)
        {
            // Only care about Humanlike pawns
            if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
            {
                return false;
            }

            // Skip if the pawn already has the comp
            if (pawn.GetComp<SynapsePawnComp>() != null)
            {
                return false;
            }

            // Create and initialize the comp
            var newComp = new SynapsePawnComp();
            newComp.parent = pawn;
            
            // Reinitialize the comp's properties by finding them from the pawn's def
            var props = pawn.def.comps?.Find(c => c.compClass == typeof(SynapsePawnComp));
            
            // Standard RimWorld comp initialization
            newComp.Initialize(props);
            
            // Add to the pawn's comps list
            if (pawn.AllComps == null)
            {
                // Edge case where pawn has absolutely no comps, though rare for Humanlikes
                // We'd have to use reflection to set it, but usually AllComps is instantiated.
                // In vanilla, AllComps is a private field accessible via a getter. We'll use Reflection.
                var compsField = typeof(ThingWithComps).GetField("comps", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (compsField != null)
                {
                    compsField.SetValue(pawn, new List<ThingComp>());
                }
            }
            
            pawn.AllComps?.Add(newComp);
            return true;
        }
    }
}


