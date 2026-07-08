# RimSynapse-Psychology — Backstory Memory & Pawn Arrival Addendum

> **Source:** Ideas extracted from Core context-planning-1 review.
> These features involve creating persistent pawn memories from backstory data,
> which is Psychology's domain (per-pawn ThingComp data via Scribe).
>
> **Status:** Design notes for future planning iteration.

---

## 1. Defining Memories on Pawn Arrival

When a new pawn joins the colony, Psychology should be ready to **receive a defining memory** planted by the Storyteller mod.

### Psychology's Role

Psychology **does not** generate the backstory itself (that's Storyteller's LLM call). Psychology provides the **storage and decay API** that Storyteller uses:

```csharp
public static class SynapsePsychology
{
    /// <summary>
    /// Public API for other mods (primarily Storyteller) to add a memory
    /// to a pawn's SynapsePawnComp.
    /// </summary>
    public static void AddMemory(Pawn pawn, WeightedMemory memory)
    {
        var comp = pawn.TryGetComp<SynapsePawnComp>();
        if (comp == null)
        {
            SynapseLog.Warn("psychology",
                $"Cannot add memory — SynapsePawnComp not found on {pawn.Name}.");
            return;
        }

        comp.memories.Add(memory);
        SynapseLog.Info("psychology",
            $"Memory added to {pawn.Name}: \"{memory.summary}\" " +
            $"(type: {memory.memoryType}, weight: {memory.weight})");
    }

    /// <summary>
    /// Bump a memory's weight when the LLM references it.
    /// </summary>
    public static void BumpMemory(Pawn pawn, string memorySummaryFragment,
        float bumpAmount = 0.2f)
    {
        var comp = pawn.TryGetComp<SynapsePawnComp>();
        if (comp == null) return;

        var match = comp.memories.FirstOrDefault(m =>
            m.summary.Contains(memorySummaryFragment));

        if (match != null)
        {
            match.weight = Math.Min(1.0f, match.weight + bumpAmount);
            match.timesReferenced++;
        }
    }
}
```

### Backstory Memory Characteristics

When Storyteller plants a backstory memory, it should have these properties:

| Field | Value | Rationale |
|---|---|---|
| `memoryType` | `"backstory"` | Distinct type for filtering/querying |
| `baseWeight` | `1.0` | Maximum importance — this is who they are |
| `weight` | `1.0` | Starts at full strength |
| `decayRate` | `0.001` | Almost never decays (~3 years to reach 0) |
| `tags` | 2–5 keywords from LLM | Used by Storyteller's resonance engine |
| `summary` | LLM-generated paragraph | Rich narrative for future context |

### Memory Type Hierarchy

With backstory memories added, the full memory type set becomes:

```
backstory   — planted once on pawn arrival, near-permanent
raid        — combat and defense events
social      — interpersonal interactions
event       — colony-wide events (eclipse, manhunter, etc.)
trade       — economic interactions
quest       — quest-related memories
daily       — routine events (meals, recreation, work)
```

Backstory memories are **never pruned by decay** in practice (0.001/day → 1000 days to zero). They can only be displaced by the memory limit if the pawn accumulates many higher-weight memories — which would represent genuine character evolution.

---

## 2. Opinion Integral — Context Provision

Psychology already owns the opinion integral system (sampling pawn opinions over time). For the context embedding system, Psychology should expose this cleanly for Core to read:

```csharp
public static class SynapsePsychology
{
    /// <summary>
    /// Get the opinion integral (moving average) for a pawn→target relationship.
    /// Returns null if Psychology mod is not loaded or no samples exist.
    /// </summary>
    public static float? GetOpinionIntegral(Pawn pawn, Pawn target)
    {
        var comp = pawn.TryGetComp<SynapsePawnComp>();
        if (comp == null) return null;

        var samples = comp.opinionHistory
            .Where(s => s.targetPawnId == target.ThingID)
            .ToList();

        if (samples.Count == 0) return null;

        return samples.Average(s => s.opinion);
    }

    /// <summary>
    /// Get the relationship trajectory (current opinion minus integral).
    /// Positive = improving, negative = deteriorating.
    /// </summary>
    public static float? GetRelationshipTrajectory(Pawn pawn, Pawn target)
    {
        float? integral = GetOpinionIntegral(pawn, target);
        if (integral == null) return null;

        int currentOpinion = pawn.relations.OpinionOf(target);
        return currentOpinion - integral.Value;
    }
}
```

### What Psychology Provides to Context

When Core builds a `PawnPacket`, it checks if Psychology is loaded and reads:

| Field | Source | Notes |
|---|---|---|
| `memories` | `SynapsePawnComp.memories` | Filtered by weight ≥ threshold |
| `personalitySummary` | `SynapsePawnComp.personalitySummary` | May be null if never generated |
| `opinionIntegral` | `SynapsePsychology.GetOpinionIntegral()` | Per target pawn |
| `relationshipTrajectory` | `SynapsePsychology.GetRelationshipTrajectory()` | Improving/deteriorating |

Core accesses all of this via `TryGetComp` — if Psychology isn't loaded, these fields are null/empty and gracefully omitted from context.

---

## 3. Relationship to Core

```
Core (vanilla+)                      Psychology (pawn data layer)
────────────────                     ──────────────────────────────
Reads SynapsePawnComp if present     Owns SynapsePawnComp (ThingComp)
Null-safe: works without Psychology  Manages memory lifecycle (add/decay/prune)
Includes memories in PawnPacket      Provides API: AddMemory, BumpMemory
Includes personality in PawnPacket   Generates personality summaries via LLM
Includes opinion integral if avail   Samples opinions, computes integral
```

Psychology is the **pawn memory and personality layer**. Core reads from it; Storyteller writes to it.
