using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using RimSynapse.Psychology.UI;

namespace RimSynapse.Psychology.Jobs
{
    public class JobDriver_TherapySession : JobDriver
    {
        private Pawn TargetPawn => (Pawn)job.GetTarget(TargetIndex.A).Thing;
        private Thing SeatA => job.GetTarget(TargetIndex.B).Thing;
        private Thing SeatB => job.GetTarget(TargetIndex.C).Thing;

        public bool backgroundResolution = false;
        private List<string> backgroundTranscript;
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetPawn, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDowned(TargetIndex.A);
            this.FailOnNotAwake(TargetIndex.A);

            // Find seating for both
            yield return new Toil
            {
                initAction = delegate
                {
                    // Find a gather spot or seats nearby TargetPawn
                    Thing seat1 = GenClosest.ClosestThingReachable(TargetPawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.OnCell, TraverseParms.For(pawn), 20f, 
                        t => t.def.building != null && t.def.building.isSittable && (t as RimWorld.Building_Bed) == null && pawn.CanReserve(t) && TargetPawn.CanReserve(t));
                    
                    Thing seat2 = null;
                    if (seat1 != null)
                    {
                        seat2 = GenClosest.ClosestThingReachable(seat1.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.OnCell, TraverseParms.For(pawn), 4f, 
                            t => t != seat1 && t.def.building != null && t.def.building.isSittable && (t as RimWorld.Building_Bed) == null && pawn.CanReserve(t) && TargetPawn.CanReserve(t));
                    }

                    if (seat1 != null && seat2 != null)
                    {
                        job.SetTarget(TargetIndex.B, seat1);
                        job.SetTarget(TargetIndex.C, seat2);
                    }
                    else
                    {
                        // Fallback to just standing near each other
                        job.SetTarget(TargetIndex.B, pawn.Position);
                        job.SetTarget(TargetIndex.C, TargetPawn.Position);
                    }
                }
            };

            // Path to seating
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil chatToil = new Toil();
            chatToil.initAction = delegate
            {
                // Force target to wait and face us or sit
                Job waitJob = null;
                if (SeatB != null && SeatB.def != null && SeatB.def.building != null && SeatB.def.building.isSittable)
                {
                    waitJob = JobMaker.MakeJob(JobDefOf.Wait_Combat, 4000); // Replaced SitFacing
                    TargetPawn.jobs.StartJob(waitJob, JobCondition.InterruptForced);
                    
                    pawn.pather.StartPath(SeatA, PathEndMode.OnCell);
                }
                else
                {
                    waitJob = JobMaker.MakeJob(JobDefOf.Wait_Combat, 4000);
                    TargetPawn.jobs.StartJob(waitJob, JobCondition.InterruptForced);
                    pawn.rotationTracker.FaceCell(TargetPawn.Position);
                    TargetPawn.rotationTracker.FaceCell(pawn.Position);
                }

                // Open the Therapy UI Window
                var window = new Dialog_TherapySession(pawn, TargetPawn, this);
                Find.WindowStack.Add(window);
            };
            
            chatToil.tickAction = delegate
            {
                if (SeatA == null || SeatA.def == null || !SeatA.def.building.isSittable)
                {
                    pawn.rotationTracker.FaceCell(TargetPawn.Position);
                    TargetPawn.rotationTracker.FaceCell(pawn.Position);
                }
                
                // Tick joy for both
                pawn.needs?.joy?.GainJoy(0.0001f, JoyKindDefOf.Social);
                TargetPawn.needs?.joy?.GainJoy(0.0001f, JoyKindDefOf.Social);

                if (backgroundResolution)
                {
                    // Randomly add a log in the background every once in a while to simulate talking,
                    // but we will offload the real completion to the API at the end of the job
                    if (Find.TickManager.TicksGame % 500 == 0)
                    {
                        backgroundTranscript.Add($"[System] Background chat ticked at {Find.TickManager.TicksGame}");
                    }
                }
            };

            chatToil.defaultCompleteMode = ToilCompleteMode.Delay;
            chatToil.defaultDuration = 5000; // ~2 in-game hours
            // chatToil.socialMode = RecordWorker_TimeGettingJoy.SocialMode.Normal;

            chatToil.AddFinishAction(() => 
            {
                if (backgroundResolution)
                {
                    SaveTranscriptAndEnd(backgroundTranscript);
                }
            });
            
            yield return chatToil;
        }

        public void EnableBackgroundResolution(List<string> currentLog)
        {
            backgroundResolution = true;
            backgroundTranscript = currentLog;
            Messages.Message($"Therapy session pushed to background. It will conclude in a few hours.", MessageTypeDefOf.PositiveEvent, false);
        }

        public void EndJobManually(List<string> finalLog)
        {
            SaveTranscriptAndEnd(finalLog);
            this.EndJobWith(JobCondition.Succeeded);
            if (TargetPawn.jobs.curJob?.def == JobDefOf.Wait_Combat) // Removed SitFacing check
            {
                TargetPawn.jobs.EndCurrentJob(JobCondition.Succeeded);
            }
        }

        private void SaveTranscriptAndEnd(List<string> log)
        {
            // Save transcript
            var comp = pawn.TryGetComp<RimSynapse.Psychology.Comps.SynapsePawnComp>();
            if (comp != null)
            {
                comp.therapyTranscripts.Add(new Models.TherapyTranscript
                {
                    otherPawnName = TargetPawn.NameShortColored.Resolve(),
                    sessionTick = Find.TickManager.TicksGame,
                    lines = new List<string>(log)
                });
            }

            var tComp = TargetPawn.TryGetComp<RimSynapse.Psychology.Comps.SynapsePawnComp>();
            if (tComp != null)
            {
                tComp.therapyTranscripts.Add(new Models.TherapyTranscript
                {
                    otherPawnName = pawn.NameShortColored.Resolve(),
                    sessionTick = Find.TickManager.TicksGame,
                    lines = new List<string>(log)
                });
            }

            // Fire off API request to summarize key points permanently
            RimSynapse.Psychology.API.SynapsePsychology.SummarizeTherapySession(pawn, TargetPawn, log);
        }
    }
}
