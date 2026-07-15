# Trust and Familiarity Network

RimSynapse - Psychology completely reimagines RimWorld's vanilla social mechanics by introducing dynamic Trust and Familiarity metrics, entirely backed by an LLM that reads your colonists' thoughts.

## Familiarity
Familiarity tracks how well two colonists know each other (from 0 to 100).
- **Passive Growth**: Colonists spending time in the same room, working together, or sleeping in the same barracks will slowly gain familiarity.
- **Active Growth**: Any social interaction (Chitchat, Deep Talk, Slight, Insult) will actively bump familiarity.
- **Decay**: If two colonists avoid each other, their familiarity will slowly decay.

## Trust
Trust measures the strength of the bond between two colonists (from -100 to 100).
- **Event-Driven**: Trust dynamically updates based on interactions. A Deep Talk or rescuing someone dramatically increases trust. Insults, slights, and social fights severely damage it.
- **Vanilla Integration**: To ensure RimWorld's base game functions properly (who becomes lovers, who starts fights), RimSynapse scales the vanilla `OpinionOf` calculation down by 50%, and injects your custom Trust metric at 50% weight.
- **Dialogue System Integration**: High trust ($> 20$) protects a target pawn from responding or retaliating when an acquaintance is on an insulting spree (reducing response likelihood by 70%).

## LLM Relationship Memories
The core of the system is the Opportunistic Task Engine. 
As colonists reach specific Familiarity milestones, or undergo massive swings in Trust, RimSynapse quietly sends their traits, lifetime burdens, and trust scores to the LLM in the background.
The LLM generates a deeply personal "Relationship Memory"—a snippet of internal monologue detailing exactly how one colonist feels about the other based on their shared history.

### Ritual Hooks
These generated relationship memories aren't just for show! They hook directly into RimWorld's intimate rituals:
- **Marriages**: When colonists get married, they will read out their genuine LLM-generated relationship memories as their Vows.
- **Funerals**: When a colonist dies, the funeral speaker will recall their personal relationship memory of the deceased and deliver it as a Eulogy.

## UI Integration
You can view the complete Social Network of any colonist by clicking the **Psychology** button on their inspect pane. The new **Social Network** tab displays every pawn they know, their exact Trust and Familiarity levels, their vanilla Affinity, and renders the LLM's relationship memories.
