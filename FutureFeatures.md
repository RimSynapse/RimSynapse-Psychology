# Future Features & MCP Architecture: RimSynapse Psychology

This document outlines the architectural roadmap, completed achievements, and backlog plans for **RimSynapse Psychology** using the Model Context Protocol (MCP) tool integration.

---

## 1. Accomplished Features & MCP Tools

### Shared Baseline Caching & Memory Storage (Feature 1)
- **Data Layers**: Baseline pawn memories (`memories` list of `WeightedMemory`) and opinon samples (`opinionHistory` list of `OpinionSample`) are stored in Core's `SynapseCorePawnComp` component. This allows other companion mods (like Chat and Storyteller) to access basic history without direct assembly references to Psychology.
- **Advanced State Layers**: Psychology-specific personality states (dynamic traits list, therapy session records, predicted break states, and target break categories) are kept inside Psychology's `SynapsePawnComp` comp.

### Psychology MCP Tools (Proposed in inspirations2.md)
- **`get_colonist_psychology_profile`**: Dynamically compiles deep clinical details—biography, traits, pending break categories, top 5 memory burdens, recent memories, and social networks (opinion integrals, trajectories, familiarity, trust, and shared memories).
- **`get_recent_social_interactions`**: Queries vanilla RimWorld's `PlayLog` entries to list recent social chats, deep talks, and insults, calculated chronologically with name-filtering.

### Custom Break Delegate Interception (Feature 2)
- Registers the custom break callback `SynapseToolRegistry.CustomBreakHandler = API.SynapsePsychologyTools.HandleCustomBreak` on startup.
- Overrides Core's default possession fallbacks to trigger actual Psychology mental states:
  - **`TrashClean` (Suicide)**: Triggers `Synapse_SuicidalAntagonize`, `Synapse_SuicidalBurn`, or `Synapse_SuicidalStarve`.
  - **`OverWrite` (Homicide)**: Triggers `Synapse_MentalState_Homicidal`.
  - **`Depart` (Abandon Colony)**: Triggers `Synapse_MentalState_AbandonColony`.
  - **`TraumaSnap` (PTSD Trigger)**: Triggers `Synapse_TraumaTrigger` (blind shooting and cowering).

### Custom PTSD Trait (Feature 2)
- Registered the custom trait `Synapse_PTSD` in [Traits_Synapse.xml](file:///d:/github/RimSynapse-Psychology/Defs/TraitDefs/Traits_Synapse.xml). This is dynamically assigned by the AI review engine.

### Daily Review Ticker & Force Review Button (Feature 3)
- Implemented the daily clinical review queue (`QueueDailyPsychologyReview`) when pawns sleep, updating their personality profiles and evaluating dynamic trait changes.
- Added a player-facing **"Force Psych Review"** button in [Dialog_PawnPsychology.cs](file:///d:/github/RimSynapse-Psychology/Source/UI/Dialog_PawnPsychology.cs) to queue assessments early.

### Expansion DLC Integrations (Features 7 to 11)
- **Royalty**: Hooks nobility title awards (`AwardTitle`) and psycasts (`Ability.Activate`) to record title complexes and psycast usage.
- **Ideology**: Intercepts ritual conclusions (`RitualOutcomeEffectWorker.Apply` via `positivityIndex`) and conversion events (`SetIdeo` using private field traversing) to track spiritual shifts.
- **Biotech**: Hooks births (`DoBirth`) and gene modifications (`ImplantXenogerm`) to capture parental and genetic dysphoria states.
- **Anomaly**: Hooks damage from entity factions (`PostApplyDamage` checking entity categories) and monolith studies (`CompStudiableMonolith.Study`) to track void obsessions.
- **Save Our Ship 2 / Odyssey**: Detects spatial orbit biomes to dynamically inject space melancholy prompt guidelines during daily clinical reviews.

---

## 2. Future Backlog Features

### counseling PTSD Cures & desensitization Therapy (Feature 2)
- Support psychologist-recommended treatment actions (such as supervised target shooting practice) to desensitize and cure the `Synapse_PTSD` trait or other treatable vanilla mental traits.

### Colony Cliques & The Rumor Mill (Feature 5)
- Leverage rolling short-term social log trackers to form sub-groups (cliques) and implement rumor-mill logs that propagate news and gossip between colonists during chit-chat.

### Interpersonal Pawn-to-Pawn Therapy Sessions (Feature 6)
- Implement a right-click order option for players to direct a counselor to perform therapy on a target.
- Target can say "no" (if busy, or if their opinion index/relationship value of the therapist is low), except for prisoners and slaves who must comply.
