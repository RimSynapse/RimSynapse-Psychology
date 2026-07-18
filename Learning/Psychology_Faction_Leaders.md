# Faction Leader Psychology

RimSynapse-Psychology extends LLM lore generation beyond your settlement to the entire planet.

## World VIP Backstories
Faction Leaders are the most important NPCs in RimWorld, yet they often feel generic.
RimSynapse introduces a specialized pipeline to give every Faction Leader a rich, generated history.

### The 3-Step Generation Pipeline
Because Faction Leaders represent entire cultures, their backstories are generated using a complex, multi-stage LLM pipeline:
1. **Faction History**: The LLM first analyzes the faction's ideology (if Ideology DLC is active), their tech level, and their relationship with the player's colony to generate a cohesive "Faction History".
2. **Leader Backstory**: The LLM then generates the leader's personal backstory, ensuring it logically aligns with the newly generated Faction History.
3. **Psychological Profile**: Finally, the LLM determines what psychological traits a leader of this specific faction would have (e.g., a brutal warlord vs. a diplomatic trader).

This ensures that when a Faction Leader eventually visits your colony, contacts you on the comms console, or leads a raid, they arrive with a deep, ideologically consistent history and personality.

> [!NOTE]
> **Faction Lore Migration**: The broader "Faction History" generation mentioned above is managed by the **`RimSynapse-Factions`** module. `RimSynapse-Psychology` hooks into that generated lore to construct the leader's specific psychological profile and interpersonal backstories.

## Expansion Dependencies
> [!NOTE]
> **Ideology Requirement**: The 3-Step Generation Pipeline heavily relies on analyzing the faction's core precepts to determine their history and the leader's personality. This deep cultural generation requires the **Ideology DLC**. Without it, generating a history falls back on basic tech-level and vanilla relationship status.
