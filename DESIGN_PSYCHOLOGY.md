# RimSynapse-Psychology Design Document

## Overview
RimSynapse-Psychology introduces an AI-driven personality system for pawns, allowing their personalities to evolve over time based on in-game events, weighted memories, and relationship tracking.

**Note:** With context assembly now embedded in RimSynapse Core, Psychology depends solely on Core for its prompt generation and state serialization needs.

## Core Features

### 1. Weighted Memory System
*   **Memory Structure:** Each memory consists of a summary, type (e.g., `raid`, `social`, `event`, `trade`, `quest`, `daily`), tags, weight (0.0 to 1.0), base weight, decay rate, and times referenced.
*   **Storage:** Memories are stored per-pawn via a `ThingComp` (`SynapsePawnComp`), ensuring they persist through the RimWorld save file.
*   **Memory Decay:** Runs once per in-game day. The weight is reduced by a `decayRate` (default 0.05). Memories with a weight of 0 or less are pruned to conserve token space and processing power.
*   **Memory Bump:** When the LLM references a specific memory in its response, the memory's weight is bumped (e.g., by 0.2, up to a maximum of 1.0) and its `timesReferenced` counter is incremented, solidifying it in the pawn's long-term memory.

### 2. Opinion Integral / Trajectory
*   **Sampling:** Pawn opinions are sampled periodically (e.g., every N ticks).
*   **Moving Average:** A moving average (integral) is computed across these samples.
*   **LLM Context:** Provides a richer context for the LLM. For instance, if a pawn has a +80 opinion today but an integral of -15, the LLM understands that the relationship has only recently improved, allowing for nuanced dialogue and interactions.

### 3. AI Personality Summary
*   **Generation:** The LLM generates concise personality summaries based on accumulated events, traits, and memories.
*   **Storage:** Stored as a `personalitySummary` string on the pawn's `SynapsePawnComp`.
*   **Evolution:** As new memories accumulate, this summary evolves, reflecting the pawn's changing worldview and experiences.

## Data Model (Scribe-Persisted)

All data is natively serialized into the RimWorld save file using Scribe.

```csharp
public class SynapsePawnComp : ThingComp
{
    public List<WeightedMemory> memories = new();
    public List<OpinionSample> opinionHistory = new();
    public string personalitySummary;

    public override void CompExposeData()
    {
        Scribe_Collections.Look(ref memories, "synapseMemories", LookMode.Deep);
        Scribe_Collections.Look(ref opinionHistory, "synapseOpinionHistory", LookMode.Deep);
        Scribe_Values.Look(ref personalitySummary, "synapsePersonality");
    }
}

public class WeightedMemory : IExposable
{
    public string summary;
    public string memoryType;
    public List<string> tags;
    public int gameTick;
    public float weight;
    public float baseWeight;
    public float decayRate;
    public int timesReferenced;

    public void ExposeData() { /* Scribe_Values for fields */ }
}

public class OpinionSample : IExposable
{
    public string targetPawnId;
    public int opinion;
    public int gameTick;

    public void ExposeData() { /* Scribe_Values for fields */ }
}
```

## Integration with Core
The Psychology mod utilizes the Core mod's Context Assembly system to build comprehensive pawn data packets. This includes injecting the `SynapsePawnComp` data (memories, opinion history, personality summary) into the `PawnPacket` sent to the LLM.
