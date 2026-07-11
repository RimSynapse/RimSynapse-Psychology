# RimChat Inspirations - Psychology Module

### Feature 1
**Rimchat Feature:** Long-Term Memory Storage
**RimSynapse Feature:** <missing>
**AI Suggestion:** Write a custom `WorldComponent` to store significant Pawn memories permanently.
**User Input:** We absolutely have this!  THis is the memory setup we built!.  We should create a short term memory context which works over the last 48 ingame hours for context prompting and psychological evaluations.
**(Core Integration Complete)**: Core now handles rolling 48-hour short-term memory through `SynapseCoreWorldComponent` and Tier 6 Context Packets.

### Feature 2
**Rimchat Feature:** PTSD & Trauma Triggers
**RimSynapse Feature:** <missing>
**AI Suggestion:** Write a hook that checks if the current chat topic overlaps with a strong negative memory, injecting a "Trauma" prompt to the LLM.
**User Input:** This will be quick Psychological break.  If they have a fire arm.  They will run into a room shoot at the door and nearby wall and then calm down.  The will then cower for one ingame hour.  If they are not close to a room (8 squares)  They will shoot in the general direction of the loudest noise (based on pawn/animal size or furniture that uses/genrates the most electricity within 12 squares)  If this initatites combat if they HIT the animal, they will snap out and actually attempt to kill the animal.  The shot fired has a 50% chance to hit a random target within 3 squares of the pawn.  
**Additional User Input (Later Feature):** I think PTSD should be a custom trait we add in. This will allow the game to track it as well in the vanilla style. Consoling will be able to be used to remove it. If a pawn has the PTSD trait, upon addition, triggers need to be defined (e.g. only triggers if combat has occurred and they are drafted). All vanilla mental traits should be treatable with counseling (except genetic ones), and the psychologist can recommend treatment (like desensitization by shooting a gun).

### Feature 3
**Rimchat Feature:** Dynamic Personality Progression (RPG Stats)
**RimSynapse Feature:** <missing>
**AI Suggestion:** Create a new `PawnComp_Personality` to track evolving traits based on LLM outputs over time.
**User Input:** I mean, this should be included in the memory fucntionality everytime a long term memory is created, The psychology review will determine if they are developing a new personality trait.  These should be included in the reports.  We should also include a short psychology report for the pawn every 24 hours at a minimum.  We should add a button to force a review of the pawn early.  This will provide interesting game context.

### Feature 4
**Rimchat Feature:** Interpersonal Relationship Tracker
**RimSynapse Feature:** <missing>
**AI Suggestion:** Write a lightweight tracker for Pawn opinions that feeds into the Long-Term Memory.
**User Input:** This is already in the works, it might need further development.  Specifically I thought that high mood things can atomatically triggger memories.

### Feature 5
**Rimchat Feature:** Colony Cliques & The Rumor Mill
**RimSynapse Feature:** <missing>
**AI Suggestion:** Pawns talk behind each other's backs. The Short-Term Memory system tracks who interacts with whom, leading to the organic formation of social "cliques".
**User Input:** I love this!!!!

### Feature 6
**Rimchat Feature:** Nuanced Grief & Therapy Sessions
**RimSynapse Feature:** <missing>
**AI Suggestion:** When a pawn suffers a traumatic event, initiate a "Therapy Chat" to process the trauma into a long-term memory.
**User Input:** YES!!!! I think this should be done pawn to pawn though. But driven by the player. So it's an action that another pawn can activate against any other pawn. These are specific types of conversations though. Not generic talk. The other pawn can say no too. Like if they are working, or absolutely hate the pawn. If they are prisoner or slave they have no choice but to accept though. The social status may also mean they accept, even if they hate them.

## Expansion Inspirations

### Feature 7: Memories of Nobility & Power (Royalty)
**Source Credit:** Royalty DLC
**RimSynapse Feature:** <missing>
**AI Suggestion:** Unique defining memories for gaining titles or using powerful psycasts. ("The day I silenced a crowd with my mind.") Nobles develop psychological complexes about their status.

### Feature 8: Spiritual Rebirth & Crises of Faith (Ideology)
**Source Credit:** Vanilla Ideology Expanded
**RimSynapse Feature:** <missing>
**AI Suggestion:** Rituals and conversion attempts are highly impactful memories. A pawn being converted forms a "Rebirth" memory, while seeing a relic destroyed causes a "Crisis of Faith" trauma.

### Feature 9: Genetic Dysphoria & Vat-Grown Trauma (Biotech)
**Source Credit:** Biotech DLC / Questionable Ethics Enhanced
**RimSynapse Feature:** <missing>
**AI Suggestion:** Memories of giving birth, growing up in a vat, or receiving forced genetic modifications. ("When the mechanites altered my DNA, I felt like a stranger in my own body.")

### Feature 10: Void Taint & Cosmic Horror (Anomaly)
**Source Credit:** Anomaly DLC
**RimSynapse Feature:** <missing>
**AI Suggestion:** Deep trauma memories from entity encounters. Studying the void leaves permanent "Void Taint" tags on memories, subtly altering the pawn's personality profile towards madness or obsession.

### Feature 11: The Weight of the Stars (Odyssey)
**Source Credit:** Save Our Ship 2
**RimSynapse Feature:** <missing>
**AI Suggestion:** Unique psychological profiles for pawns who have left the planet. The harshness of the void and missing the dirt of the Rim creates deep melancholic or transcendent memories.
