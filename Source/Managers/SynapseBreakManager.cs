using RimWorld;
using Verse;
using RimSynapse.Psychology.Comps;

namespace RimSynapse.Psychology.Managers
{
    public class SynapseBreakManager : GameComponent
    {
        // Global toggle for the system. 
        // Can be hooked into RimSynapse-Core settings to disable if the LLM backend is offline.
        public static bool Enabled = true; 

        public SynapseBreakManager(Game game) { }

        public override void GameComponentTick()
        {
            // Evaluate every 150 ticks, matching vanilla MentalBreaker update interval
            if (Find.TickManager.TicksGame % 150 == 0)
            {
                if (!Enabled) return;

                foreach (var map in Find.Maps)
                {
                    foreach (var pawn in map.mapPawns.FreeColonists)
                    {
                        var comp = pawn.GetComp<SynapsePawnComp>();
                        if (comp == null) continue;

                        CheckPawnMentalState(pawn, comp);
                    }
                }
            }
        }

        private void CheckPawnMentalState(Pawn pawn, SynapsePawnComp comp)
        {
            // Skip pawns currently having a mental breakdown
            if (pawn.InMentalState) return;
            if (pawn.mindState == null || pawn.mindState.mentalBreaker == null) return;

            // --- BREAK EVALUATION ---
            if (pawn.needs != null && pawn.needs.mood != null && 
                pawn.needs.mood.CurLevel < pawn.mindState.mentalBreaker.BreakThresholdExtreme)
            {
                if (comp.lastExtremeNegativeTick < Find.TickManager.TicksGame - 100) 
                {
                    RimSynapse.Psychology.Utils.PsychologyLogger.LogEvent(pawn, "ExtremeNegative", $"Crossed extreme threshold. Mood: {pawn.needs.mood.CurLevelPercentage:F2}");
                }
                comp.lastExtremeNegativeTick = Find.TickManager.TicksGame;

                // If they crossed the threshold and we haven't asked the AI yet
                if (comp.predictedBreakState == null && comp.currentBreakWarning == null)
                {
                    // Mark as pending to prevent spamming the LLM
                    comp.currentBreakWarning = "pending";
                    
                    // Request LLM Break Profile
                    // (This will eventually hook into Core to queue an actual LLM contextual request)
                    // SynapsePsychology.RequestBreakWarning(pawn);
                }
                else if (comp.predictedBreakState != null)
                {
                    // AI has returned a predicted break. Wait for them to actually snap.
                    // Extreme break MTB is ~0.6 days in vanilla.
                    if (Rand.MTBEventOccurs(0.6f, 60000f, 150f))
                    {
                        pawn.mindState.mentalStateHandler.TryStartMentalState(comp.predictedBreakState, "AI-Driven Break");
                        
                        // Clear the cache so it evaluates fresh next time
                        comp.predictedBreakState = null;
                        comp.currentBreakWarning = null;
                    }
                }
            }
            else
            {
                // If they recovered their mood above extreme, clear the impending doom
                if (comp.predictedBreakState != null || comp.currentBreakWarning != null)
                {
                    comp.predictedBreakState = null;
                    comp.currentBreakWarning = null;
                }
            }

            // --- EUPHORIA EVALUATION ---
            if (pawn.needs != null && pawn.needs.mood != null && pawn.needs.mood.CurLevelPercentage >= 0.85f)
            {
                bool hasBipolar = pawn.story?.traits?.HasTrait(DefDatabase<TraitDef>.GetNamedSilentFail("Bipolar")) == true ||
                                  pawn.story?.traits?.HasTrait(DefDatabase<TraitDef>.GetNamedSilentFail("Synapse_Bipolar")) == true;
                
                // Extreme negative occurred within the last 5 days (300,000 ticks)
                bool recentExtremeNegative = comp.lastExtremeNegativeTick > 0 && 
                                             (Find.TickManager.TicksGame - comp.lastExtremeNegativeTick) < 300000;

                if (hasBipolar || recentExtremeNegative)
                {
                    if (!comp.isEuphoric)
                    {
                        comp.isEuphoric = true;
                        RimSynapse.Psychology.Utils.PsychologyLogger.LogEvent(pawn, "EuphoriaStart", $"Entered Euphoria. Bipolar: {hasBipolar}, RecentNegative: {recentExtremeNegative}");
                        // Trigger euphoria event or queue LLM for specific inspiration
                    }
                    
                    // MTB for reckless positive actions (e.g., 2 days)
                    if (Rand.MTBEventOccurs(2.0f, 60000f, 150f))
                    {
                        RimSynapse.Psychology.Utils.PsychologyLogger.LogEvent(pawn, "EuphoriaAction", $"Triggering Euphoric Reckless state.");
                        pawn.mindState.mentalStateHandler.TryStartMentalState(DefDatabase<MentalStateDef>.GetNamed("Synapse_EuphoricReckless"), "Euphoria");
                    }
                }
            }
            else
            {
                comp.isEuphoric = false;
            }
        }
    }
}
