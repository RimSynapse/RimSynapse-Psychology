# Clinical Evaluations and Memory Journals

RimSynapse - Psychology completely replaces the rigid, numeric mood system of RimWorld with a dynamic, LLM-driven psychological profile for every colonist.

## The Memory Journal
As your colonists experience the harsh realities of the Rim—combat, starvation, loss, or triumph—RimSynapse tracks these events.
Using the Opportunistic Task Engine, the mod periodically evaluates these experiences and generates **Internal Monologues**.
These memories are saved into the colonist's Journal.
- **Trauma and Desensitization**: The AI looks at a colonist's lifetime statistics. A first kill will be incredibly traumatic, generating a memory with a `0.0` decay rate that becomes a permanent "Burden" on their psyche. Their 50th kill will barely register.

## Clinical Evaluations
Every night when a colonist goes to sleep (or at 22:00 if they don't sleep), RimSynapse reviews their daily average mood.
If flagged for evaluation, a background LLM task reads their traits, their recent memories, and their daily mood to generate a **Clinical Evaluation**.
- This evaluation determines their current `Break Category` (Homicidal, Suicidal, Issue-Averse) and `Break Intensity`.
- When a mental break occurs in RimWorld, it will no longer be random. The break will directly reflect their clinical evaluation.

### Dynamic Trait Injection
One of the most powerful features of RimSynapse-Psychology is **Dynamic Trait Injection**. Colonists are no longer locked into the traits they spawned with. If the LLM determines a colonist has undergone profound life changes or accumulated enough specific trauma/experiences, they may dynamically gain or lose RimWorld personality traits. 

For example, a peaceful farmer who miraculously survives three consecutive brutal raids might dynamically gain the `Bloodlust` or `Tough` trait. A colonist who constantly experiences the death of friends might gain `Depressive` or lose `Optimist`. Whenever the LLM decides a core aspect of their personality has fundamentally shifted, the trait is injected directly into their RimWorld character sheet, and a letter will notify the player of this psychological evolution along with the AI's reasoning.
## Opportunistic Euphoria
Evaluations aren't just for negative events. If a pawn maintains an extremely high mood (>90%) for a sustained 24 in-game hours, the psychology system flags them for an opportunistic review and generates a positive core memory. 

## The Psychology UI
Players can access a dedicated Psychology window for any colonist.
- The **Profile** tab displays their traits, their active Psychological Burdens, their overall mood analysis, and their Clinical Diagnosis (including break predictions). 
- **Forced Reviews**: You can click the "Force Psych Review" button to immediately queue a clinical evaluation instead of waiting for the pawn to sleep.
- The **Memories** tab allows players to read the colonist's journal—a chronological list of AI-generated internal monologues detailing their exact thoughts during major colony events.

## Expansion Dependencies
While the core Clinical Evaluation system is fully functional in the base game, certain psychological events and memory generation require specific DLCs:
> [!NOTE] 
> **Biotech Requirement**: Generating trauma memories regarding forced genetic modification, genetic dysphoria, and vat-grown backgrounds requires the **Biotech DLC**.
> 
> **Anomaly Requirement**: Generating "Void Taint" tags on memories, and suffering cosmic horror trauma from entity encounters requires the **Anomaly DLC**.
