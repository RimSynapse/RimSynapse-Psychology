# Psychological Breaks

RimSynapse - Psychology introduces LLM-driven mental break systems that evaluate a pawn's long-term memory to determine how they break down.

## PTSD and Trauma Triggers
A new extreme mental break: `Synapse_TraumaTriggerBreak`.

If a pawn's daily evaluations identify severe, unresolved trauma (e.g., losing a loved one, surviving starvation), they are at risk of a PTSD break.

**Behavior:**
1. **The Trigger**: The pawn enters the `Synapse_TraumaTrigger` mental state.
2. **Wild Firing**: If they have a ranged weapon, they will seek out a wall or door within 8 squares and fire wildly at it. If no walls are nearby, they target the loudest noise (animals, other pawns, or powered buildings) within 12 squares.
3. **Random Miss Chance**: To simulate panic, their shots have a 50% chance to target a random adjacent cell instead of the direct target.
4. **Cowering**: After firing, the pawn will cower in fear for 1 in-game hour.
5. **Combat Snap**: If their wild firing accidentally hits a living target and initiates combat, the adrenaline snaps them out of the trauma state and they will fight normally.

## Expansion Dependencies
> [!NOTE]
> **Anomaly Requirement**: Void studies and entity attacks often result in extremely severe trauma. Without the **Anomaly DLC**, cosmic horror trauma triggers will not occur.
>
> **Biotech Requirement**: Trauma related to forced genetic mutation requires the **Biotech DLC**.
