using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;
using System.Collections.Generic;

namespace RimSynapse.Psychology.MentalStates
{
    public class JobGiver_EuphoricReckless : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            RimSynapse.SynapseLogger.Info("psychology", $"[RimSynapse-Psychology] Euphoria AI invoked for {pawn.Name}.");

            // 1. Try to find the biggest reachable fire on the map
            var fires = pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Fire)
                .Where(f => pawn.CanReach(f, PathEndMode.Touch, Danger.Deadly))
                .ToList();
                
            if (fires.Count > 0)
            {
                Thing biggestFire = fires.OrderByDescending(f => ((Fire)f).fireSize).FirstOrDefault();
                if (biggestFire != null)
                {
                    // Run directly onto the fire
                    if (pawn.Position != biggestFire.Position)
                    {
                        RimSynapse.SynapseLogger.Info("psychology", $"[RimSynapse-Psychology] {pawn.Name} running into fire at {biggestFire.Position}.");
                        Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, biggestFire.Position);
                        gotoJob.locomotionUrgency = LocomotionUrgency.Sprint;
                        return gotoJob;
                    }
                    else
                    {
                        RimSynapse.SynapseLogger.Info("psychology", $"[RimSynapse-Psychology] {pawn.Name} standing in fire.");
                        // Already in the fire!
                        return JobMaker.MakeJob(JobDefOf.Wait_Wander);
                    }
                }
            }

            // 2. No fire? Find an animal that is dangerous but scales with their melee ability!
            var animals = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction == null && p.RaceProps.Animal && !p.Downed && !p.Position.Fogged(p.Map) && pawn.CanReach(p, PathEndMode.Touch, Danger.Deadly))
                .ToList();

            if (animals.Count > 0)
            {
                // Calculate their "reckless capacity"
                float meleeSkill = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                float weaponDPS = 0f;
                
                if (pawn.equipment?.Primary != null && pawn.equipment.Primary.def.IsMeleeWeapon)
                {
                    weaponDPS = pawn.equipment.Primary.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
                }

                // Base capacity (skill 0 unarmed = 20, skill 20 unarmed = 120)
                // Club adds ~80, so armed could be 100-200.
                float targetCombatPower = 20f + (meleeSkill * 5f) + (weaponDPS * 10f);
                
                // Add a "reckless" multiplier so they bite off slightly more than they can chew
                targetCombatPower *= 1.5f;

                // Find the animal whose combat power is closest to their reckless capacity
                Pawn targetAnimal = animals.OrderBy(a => System.Math.Abs(a.kindDef.combatPower - targetCombatPower)).FirstOrDefault();
                
                if (targetAnimal != null)
                {
                    RimSynapse.SynapseLogger.Info("psychology", $"[RimSynapse-Psychology] {pawn.Name} charging {targetAnimal.Name?.ToStringFull ?? targetAnimal.Label} (Target CP: {targetCombatPower:F1}, Animal CP: {targetAnimal.kindDef.combatPower:F1})");
                    Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, targetAnimal);
                    attackJob.locomotionUrgency = LocomotionUrgency.Sprint;
                    return attackJob;
                }
            }

            RimSynapse.SynapseLogger.Info("psychology", $"[RimSynapse-Psychology] {pawn.Name} couldn't find fire or animal, wandering wildly.");
            // 3. Fallback to just wandering wildly
            return JobMaker.MakeJob(JobDefOf.Wait_Wander);
        }
    }
}


