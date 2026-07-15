# AI Backstory Generation

RimSynapse-Psychology brings your colonists to life by rewriting their histories using LLMs.

## Opportunistic Generation
Instead of freezing the game when you spawn a new map to generate backstories for 3 colonists simultaneously, RimSynapse utilizes an **Opportunistic Task Engine**.
When the game begins, pawns spawn with a placeholder backstory stub. Over the first few in-game hours, RimSynapse schedules low-priority background tasks to generate their backstories dynamically, yielding to real gameplay.

## Dynamic Adulthood
In vanilla RimWorld, colonists who grow up in the colony simply receive a generic backstory.
In RimSynapse, when a colony-born pawn reaches the age of 20, the Opportunistic Task Engine intercepts the event. 
It sends the pawn's traits, childhood backstory, and the current state of the colony to the LLM, asking it to write a custom "Adulthood" backstory that reflects exactly how they grew up in *your* settlement.

## Visitor Backstories
It's not just your colonists! Important NPCs, visitors, and prisoners also receive custom backstories. These are queued at an even lower priority, ensuring that your local LLM is constantly churning out rich lore for the world while your game runs flawlessly.
