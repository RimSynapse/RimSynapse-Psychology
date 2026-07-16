# RimSynapse-Psychology — Future Features

> This document consolidates all inspiration documents, design notes, and the previous backlog into a single source of truth. Each feature is cross-referenced against the current codebase to determine its implementation status.

---

## Design Decisions Log

> [!IMPORTANT]
> **Mod Rename Pending**: `RimSynapse-Chat` will be renamed to **`RimSynapse-Conversations`** to avoid confusion with LLM "chat completions" and in-game "chit-chat" interaction terminology. All references in this document use the new name.

> [!IMPORTANT]
> **Faction Leader Backstories**: Psychology does NOT run a separate generation pipeline for faction leaders. Instead, `RimSynapse-Factions` checks if Psychology is present and passes additional faction-specific context (ideology, tech level, political traits, faction history) into Psychology's **existing** backstory generation prompt. Psychology may expose hooks to accept supplemental context, but the core prompt and generation code is reused — not duplicated across mods.

> [!IMPORTANT]
> **Therapy Session LLM Dependency**: Full LLM-driven therapy dialogue (Guiding Hand, Watch mode, Resolve in Background) only activates if `RimSynapse-Conversations` is loaded. Without it, therapy sessions are a simple RNG roll that applies a temporary "counseling" mood modifier, keeping the pawn further from their break threshold. This avoids forcing Psychology to depend on a dialogue system.

---

## Implementation Status Legend

| Status | Meaning |
|--------|---------|
| ✅ Implemented | Feature is fully coded and functional |
| 🔶 Partial | Core mechanic exists but some sub-features are missing |
| ❌ Not Started | Feature exists only in design documents |

---

## 1. Feature Audit — What Has Been Built

### ✅ Weighted Memory System
> *Source: DESIGN_PSYCHOLOGY.md, Psychology_Clinical_Evaluations.md*

- Memory structure with summary, type, tags, weight, baseWeight, decayRate, timesReferenced
- Per-pawn storage via `SynapseCorePawnComp` (Core) and `SynapsePawnComp` (Psychology)
- Daily decay cycle running once per in-game day
- Memory bump when LLM references a memory
- Pruning of zero-weight memories
- Desensitization logic in prompt (lifetime kill count affects trauma generation)

### ✅ Opinion Integral and Trajectory
> *Source: DESIGN_PSYCHOLOGY.md, DESIGN_CONTEXT_INTEGRATION.md*

- `GetOpinionIntegral()` and `GetRelationshipTrajectory()` implemented in SynapsePsychologyQuery.cs
- Opinion sampling over time via `opinionHistory`
- Exposed to MCP tools and context system

### ✅ AI Personality Summary
> *Source: DESIGN_PSYCHOLOGY.md*

- LLM-generated personality summaries stored on `SynapseCorePawnComp.personalitySummary`
- Evolves as new memories accumulate

### ✅ Clinical Evaluations and Daily Review
> *Source: Psychology_Clinical_Evaluations.md*

- `QueueDailyPsychologyReview` queues evaluations when pawns sleep
- Determines break category (Homicidal, Suicidal, Issue-Averse) and break intensity
- Dynamic Trait Injection — LLM can add or remove traits with player notification
- Opportunistic Euphoria — high mood (>90%) for 24 hours triggers positive core memory
- Force Psych Review button in the Psychology UI
- Response parsing with social adjustments, abandonment risk, and trait changes

### ✅ AI Backstory Generation
> *Source: Psychology_AI_Backstories.md*

- Opportunistic backstory generation via background tasks (pawns spawn with stubs, backstories generated later)
- Dynamic Adulthood for colony-born pawns reaching adulthood
- Visitor and NPC backstories at lower priority

### ✅ PTSD and Trauma Trigger Breaks
> *Source: Psychology_Mental_Breaks.md*

- `Synapse_TraumaTrigger` mental state with wild firing, random miss chance, cowering behavior
- Custom `Synapse_PTSD` trait
- Custom break delegate interception (TrashClean, OverWrite, Depart, TraumaSnap)

### ✅ Trust and Familiarity Network
> *Source: Psychology_Trust_And_Familiarity.md*

- Familiarity (0-100) with passive growth and decay
- Trust (-100 to 100) event-driven updates via social interactions
- Vanilla OpinionOf scaled 50% down, trust injected at 50% weight
- Active growth from social interactions (Chitchat, Deep Talk, Insult)
- Passive growth from proximity and shared rooms

### ✅ LLM Relationship Memories
> *Source: Psychology_Trust_And_Familiarity.md*

- Opportunistic relationship memory generation at familiarity milestones and trust swings
- LLM generates personal internal monologue snippets per relationship
- Stored in `SocialRecord.relationshipMemories`

### ✅ Ritual Hooks (Marriage Vows and Funeral Eulogies)
> *Source: Psychology_Trust_And_Familiarity.md*

- Marriage: colonists read LLM-generated relationship memories as vows
- Funerals: speaker delivers personal relationship memory as eulogy

### ✅ Psychology MCP Tools
> *Source: Psychology_MCP_Endpoints.md*

- `get_colonist_psychology_profile` — full psychological profile query
- `get_recent_social_interactions` — vanilla PlayLog social chatter query

### ✅ DLC Expansion Hooks
> *Source: Previous FutureFeatures.md*

- **Royalty**: Nobility title and psycast hooks
- **Ideology**: Ritual conclusions and conversion events
- **Biotech**: Births and gene modification hooks
- **Anomaly**: Entity damage and monolith study hooks
- **Save Our Ship 2 / Odyssey**: Space melancholy detection

### 🔶 Therapy Sessions
> *Source: Psychology_Therapy_Sessions.md*

**Implemented:**
- Right-click "Initiate Therapy Session" float menu option
- Therapy job driver (JobDriver_TherapySession.cs)
- Therapy session UI with Guiding Hand toggle
- Therapy transcript storage
- `SummarizeTherapySession` LLM summarization
- Slave and prisoner acceptance logic

**Not Implemented:**
- "Resolve in Background" auto-mode (close UI, let pawns converse in the background)
- "Watch" mode (leave UI open, watch auto-generated dialogue in real-time)

**Architectural Decision:** Full LLM dialogue modes (Guiding Hand, Watch, Resolve in Background) require `RimSynapse-Conversations` to be loaded. Without Conversations, therapy falls back to an RNG roll that applies a temporary counseling mood modifier, keeping the pawn further from breaking. This keeps Psychology standalone.

### 🔶 Faction Leader Psychology
> *Source: Psychology_Faction_Leaders.md*

**Implemented:**
- Faction leaders are detected as important pawns for backstory generation (priority flag)

**Not Implemented:**
- Context injection hooks for Factions mod to pass additional leader context into the backstory prompt
- Factions-side integration to check for Psychology and supply faction history, ideology, and political traits

**Architectural Decision:** Psychology does NOT own a separate 3-step pipeline. Instead, the existing backstory generation prompt is reused. `RimSynapse-Factions` detects if Psychology is loaded, and if so, injects supplemental context (faction history, ideology precepts, tech level, political constraints) into the backstory generation request. Psychology may need to expose an `additionalContext` parameter on its backstory prompt builder to accept this data. This keeps faction-specific knowledge in Factions and generation logic in Psychology.

### 🔶 Backstory Memory Injection
> *Source: DESIGN_CONTEXT_INTEGRATION.md*

**Implemented:**
- `AddMemory` public API for other mods to inject memories
- `BumpMemory` API for LLM reference tracking
- Memory type system (backstory, raid, social, event, trade, quest, daily)

**Not Implemented:**
- Dedicated backstory memory with near-permanent decay rate (0.001/day)
- Formal contract with Storyteller mod for planting defining arrival memories

---

## 2. Future Backlog — Unimplemented Features

### Tier 1 — High Impact, Design Ready

#### Therapy Session Auto-Modes (Requires RimSynapse-Conversations)
> *Source: Psychology_Therapy_Sessions.md*

The Guiding Hand (manual) mode exists. Two auto-modes remain, both gated behind `RimSynapse-Conversations`:

- **Resolve in Background**: Player clicks a button, UI closes, pawns converse via LLM in the background. Both pawns generate opportunistic memories about the conversation when complete.
- **Watch Mode**: UI stays open, auto-generated dialogue renders in real-time as game ticks. The initiator's dialogue is generated by the LLM using their Intelligence, Social skill, and Ideology precepts.

Without Conversations loaded, the therapy system skips LLM dialogue entirely and applies a temporary counseling modifier via RNG roll.

#### PTSD Counseling and Desensitization Therapy
> *Source: Previous FutureFeatures.md (Feature 2)*

- Psychologist-recommended treatment actions (e.g., supervised target shooting practice) to desensitize and cure `Synapse_PTSD`
- Framework for treating other vanilla mental conditions through directed activities
- Could tie into the existing Therapy Session system — a therapy session with a high-Social pawn could have a chance to reduce PTSD severity

#### High-Trust Insult Shield
> *Source: Psychology_Trust_And_Familiarity.md*

- When trust > 20, reduce the target pawn's likelihood of retaliating to insults by 70%
- Currently trust modifies OpinionOf, but there is no explicit insult suppression patch
- Would require a Harmony postfix on `InteractionWorker_Insult` or the retaliation path

### Tier 2 — Medium Impact, Needs Design

#### Colony Cliques and The Rumor Mill
> *Source: Previous FutureFeatures.md (Feature 5)*

- Rolling short-term social log trackers to detect sub-groups (cliques) among colonists
- Rumor-mill logs that propagate news and gossip between colonists during chit-chat interactions
- Could use the existing social interaction patch to detect interaction frequency patterns
- Clique detection could feed into the LLM context for more socially-aware dialogue

#### Faction Leader Context Injection Hooks
> *Source: Psychology_Faction_Leaders.md*

Psychology needs to expose an `additionalContext` parameter on its backstory prompt builder so that Factions can inject:
- Faction history summary (generated by Factions mod)
- Ideology precepts and cultural values (if Ideology DLC present)
- Tech level and political stance
- Leadership style constraints (warlord vs. diplomat vs. trader)

This is a Factions-side integration task — Psychology only needs to accept and weave in the extra context. Without Ideology DLC, Factions falls back to tech-level and vanilla relationship data.

#### Backstory Arrival Memory Contract
> *Source: DESIGN_CONTEXT_INTEGRATION.md*

- Define a formal inter-mod API contract for Storyteller to plant "defining memories" on pawn arrival
- Use near-permanent decay rate (0.001/day, ~3 years to zero) for backstory-type memories
- These memories should be exempt from normal pruning and serve as permanent character anchors
- Tags from backstory memories feed into a "resonance engine" for future narrative callbacks

### Tier 3 — Stretch Goals

#### Familiarity Milestone Events
> *Source: Psychology_Trust_And_Familiarity.md*

- Relationship memories are generated at familiarity milestones (this exists), but visible milestone notifications to the player are not implemented
- Player-facing letters or UI indicators when colonists reach "Close Friends", "Best Friends", or "Confidant" familiarity thresholds

#### Social Network Visualization
> *Source: Psychology_Trust_And_Familiarity.md*

The Social Network tab exists in the Psychology UI, but could be enhanced with:
- Visual graph/web showing trust and familiarity connections between all colonists
- Color-coded connection lines (green = high trust, red = hostile, gray = strangers)
- Cluster detection highlighting cliques visually

#### Dynamic Trait History Timeline
> *Source: Psychology_Clinical_Evaluations.md*

- Currently traits are added/removed with a letter notification
- A timeline view in the Psychology UI showing when each dynamic trait was gained or lost, with the LLM's reasoning preserved chronologically

---

## 3. Source Document Index

All inspiration documents that were consolidated into this file:

| Document | Location | Status |
|----------|----------|--------|
| DESIGN_PSYCHOLOGY.md | Root | Can be archived — fully implemented |
| DESIGN_CONTEXT_INTEGRATION.md | Root | Partially implemented — backstory memory contract pending |
| Psychology_AI_Backstories.md | Learning/ | Can be archived — fully implemented |
| Psychology_Clinical_Evaluations.md | Learning/ | Can be archived — fully implemented |
| Psychology_Faction_Leaders.md | Learning/ | Revised — context injection hooks pending (not separate pipeline) |
| Psychology_MCP_Endpoints.md | Learning/ | Can be archived — fully implemented |
| Psychology_Mental_Breaks.md | Learning/ | Can be archived — fully implemented |
| Psychology_Therapy_Sessions.md | Learning/ | Revised — auto-modes gated behind Conversations mod |
| Psychology_Trust_And_Familiarity.md | Learning/ | Mostly implemented — insult shield and milestones pending |
