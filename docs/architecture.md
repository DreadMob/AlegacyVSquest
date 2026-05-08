# VSQuest Architecture

> **Documentation Version:** v3.0.0

---

## Overview

VSQuest is a Vintage Story mod that provides a comprehensive quest system with support for:
- Quest givers (NPCs that offer quests)
- Quest objectives and stages
- Reputation system integration
- Boss hunt events
- Action-based rewards

---

## Core Systems

### QuestSystem
The central mod system that manages:
- **QuestRegistry**: All loaded quests
- **PlayerQuests**: Active quests per player
- **CompletedQuests**: Tracking of completed quests
- **ActionRegistry**: Available quest actions

### BossHuntSystem
Manages rotating boss events:
- Boss rotation schedule
- Arena management
- Spawn/relocation logic
- Tracker items

### ReputationSystem
Handles player reputation with NPCs and factions:
- Reputation values and ranks
- Reward thresholds
- Reputation requirements for quests

### QuestCompletionRewardSystem
Manages rewards for completing quests:
- Reward tracking
- Claim status
- Requirements checking

### RerollSystem
Manages boss hunt reward item exchange:
- Reroll group definitions from `rerollconfig.json`
- Item counting per group per player
- Random reward selection from group pool
- Quality application (when enabled)
- Pending reward storage until animation completes
- Auto-claim after animation finish

### ItemQualitySystem
Applies randomized quality tiers to action items:
- 5 tiers: Common, Uncommon, Rare, Epic, Legendary
- Attribute multipliers based on tier
- Per-item quality configuration in `itemconfig.json`
- Group-level quality via `applyQuality` in `rerollconfig.json`

---

## Entity Behaviors

### EntityBehaviorQuestGiver
**Location:** `src/Entity/Behavior/EntityBehaviorQuestGiver.cs`

The main behavior that allows NPCs to offer quests. Decomposed into services:

| Service | Responsibility |
|---------|---------------|
| `QuestSelectionService` | Quest rotation, exclusion, selection logic |
| `QuestEligibilityChecker` | Cooldowns, predecessors, reputation requirements |
| `QuestGiverMessageBuilder` | Network message building, reputation info |
| `QuestGiverConstants` | Centralized string constants |

#### Configuration Properties
- `quests`: Array of quest IDs this NPC can offer
- `alwaysQuests`: Quests always available
- `rotationPool`: Pool for rotation selection
- `rotationDays`: Days between rotations
- `rotationCount`: Max quests offered per rotation
- `excludeQuests`: Specific quests to exclude
- `excludeQuestPrefixes`: Prefix patterns to exclude
- `priorityQuests`: Quests evaluated first
- `chainCooldownDays`: Global cooldown after any completion
- `singleQuestAtATime`: Only one active quest allowed
- `bossHuntActiveOnly`: Only offer active boss hunt quest
- `reputationNpcId`: NPC reputation binding
- `reputationFactionId`: Faction reputation binding

### EntityBehaviorQuestTarget
**Location:** `src/Entity/Behavior/Target/EntityBehaviorQuestTarget.cs`

Marks entities as quest targets for kill/interact objectives.

### EntityBehaviorConversable
**Location:** Vintage Story core

Used by quest givers to trigger dialog-based quest interactions.

---

## Network Layer

### Channels
| Channel | Purpose |
|---------|---------|
| `alegacyvsquest` | Main quest communication |
| `alegacyvsquest-itemaction` | Action item handling |
| `alegacyvsquestmusic` | Boss music sync |

### Key Messages
- `QuestInfoMessage`: Quest list sent to client
- `StartQuestMessage`: Quest acceptance
- `CompleteQuestMessage`: Quest completion
- `ShowQuizMessage`: Quiz UI trigger

---

## Data Flow

```
Player interacts with NPC
        ↓
EntityBehaviorQuestGiver.OnInteract()
        ↓
QuestSelectionService.GetCurrentQuestSelection()
        ↓
QuestEligibilityChecker.CheckEligibility() [for each quest]
        ↓
QuestGiverMessageBuilder.CreateBaseMessage()
        ↓
QuestGiverMessageBuilder.PopulateReputationInfo()
        ↓
Network channel sends QuestInfoMessage
        ↓
Client displays quest selection UI
```

---

## Quest Structure

```json
{
  "id": "mod:quest-id",
  "title": "Quest Title",
  "description": "Quest description",
  "cooldown": 7,
  "perPlayer": true,
  "predecessor": "mod:previous-quest",
  "predecessors": ["mod:quest1", "mod:quest2"],
  "reputationRequirements": [...],
  "actionObjectives": [...],
  "stages": [...],
  "actionRewards": [...],
  "onAcceptedActions": [...],
  "onFailedActions": [...]
}
```

---

## Exception Hierarchy

```
QuestException (base)
├── QuestNotFoundException
├── QuestConfigurationException
├── QuestGiverException
└── QuestIneligibleException
```

---

## Constants

All magic strings are centralized in:
- `QuestGiverConstants.cs` - Quest giver specific
- `ItemAttributeUtils.cs` - Item attributes
- `BossBehaviorUtils.cs` - Boss behaviors
- `VsQuestNetworkRegistry.cs` - Network channels

---

## Testing

Unit tests are located in `vsquest.Tests/` project:
- `QuestSelectionServiceTests.cs`
- `QuestEligibilityCheckerTests.cs`
- `QuestGiverConstantsTests.cs`

Run tests: `dotnet test vsquest.Tests`

---

## File Organization

```
src/
├── BossHunt/           # Boss hunt system
├── Commands/           # Chat commands
├── Entity/Behavior/    # Entity behaviors
│   └── QuestGiver/     # Quest giver services
├── Gui/                # UI components
│   └── Reroll/         # Reroll dialog and animation
├── Network/            # Network messages
├── Quests/             # Quest definitions
│   ├── Actions/        # Action handlers
│   └── Core/           # Core quest classes
├── Systems/            # Mod systems
│   ├── Reroll/         # Reroll service and quality
│   └── Services/       # Quest services
└── Utils/              # Utility classes
```
