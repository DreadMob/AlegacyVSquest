# VSQuest Architecture

> **Documentation Version:** v3.1.0

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

### HollowTrialSystem
Manages solo boss challenges (Hollow Trials):
- Tiered bosses (3 tiers per boss with different stats)
- Challenge system (speedkill, deathless, nofood, lowgear, etc.)
- Weekly modifiers (14 types affecting all trial bosses)
- Solo enforcement (kills voided if multiple players participate)
- Void Rift Anchor blocks for boss summoning
- Trial Shop with Void Shards currency
- Reputation and progression tracking
- Combat tracking (armor, saturation, damage, deaths)
- Rotation schedule (configurable days between rotations)
- Config loaded from `config/hollowtrials/*.json`

### PromoCodeSystem
Manages promo code creation and redemption:
- Asset-based codes (read-only, from `config/promocodes.json`)
- Runtime codes (admin-created, persisted in mod config)
- Reward types: actionItem, item, quest, reputation
- Conditions: date ranges, required quests
- Security: rate limiting, lockout, case-insensitive matching
- Database sync for redemption tracking

### QuizSystem
Interactive quiz engine:
- Multiple-choice questions (4 options A/B/C/D)
- Randomized option order per player
- Score tracking with correct/wrong counts
- Result body text based on score thresholds
- Integration with quest objectives via `checkvariable`
- Config loaded from `config/quizzes/*.json`

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
| `alegacyvsquest-trialshop` | Trial shop purchases and case opening |
| `alegacyvsquest-promo` | Promo code redemption |
| `alegacyvsquest-quiz` | Quiz messages |

### Key Messages
- `QuestInfoMessage`: Quest list sent to client
- `StartQuestMessage`: Quest acceptance
- `CompleteQuestMessage`: Quest completion
- `ShowQuizMessage`: Quiz UI trigger
- `TrialShopMessage`: Trial shop purchase request/response
- `CaseOpenMessages`: Case opening animation data
- `PromoCodeRedeemMessage`: Promo code redemption

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
├── BlockEntity/        # Block entity implementations
├── Blocks/             # Custom blocks (VoidRiftAnchor, etc.)
├── BossHunt/           # Boss hunt system
├── Commands/           # Chat commands
│   └── PromoCodes/     # Promo code commands
├── Entity/Behavior/    # Entity behaviors
│   └── QuestGiver/     # Quest giver services
├── Gui/                # UI components
│   ├── PromoCodes/     # Promo code redemption GUI
│   ├── Reroll/         # Reroll dialog and animation
│   └── ServerInfo/     # Server info tabbed dialog (Herald NPC)
├── Harmony/            # Harmony patches
│   ├── Blocks/         # Block patches
│   ├── Core/           # Core patches
│   ├── Dialogue/       # Dialogue patches
│   ├── Entities/       # Entity patches
│   ├── Items/          # Item patches
│   └── Players/        # Player patches
├── HollowTrials/       # Hollow Trials system
│   ├── Actions/        # Trial quest actions
│   ├── BlockEntities/  # VoidRiftAnchor block entity
│   ├── Blocks/         # VoidRiftAnchor block
│   ├── Challenges/     # Challenge evaluation and tracking
│   ├── Commands/       # Trial admin commands
│   ├── Config/         # Trial configuration classes
│   ├── Gui/            # Trial shop and case opening GUI
│   ├── Network/        # Trial network messages
│   ├── Objectives/     # Trial quest objectives
│   ├── Progression/    # Reputation and progression
│   ├── Quality/        # Trial quality roller
│   └── System/         # Core trial system (partial classes)
├── Item/               # Custom item classes
├── Network/            # Network messages
├── Quests/             # Quest definitions
│   ├── Actions/        # Action handlers
│   └── Core/           # Core quest classes
├── Systems/            # Mod systems
│   ├── ActionItems/    # Action item management
│   ├── Client/         # Client-side systems
│   ├── Config/         # Core configuration
│   ├── Database/       # External DB integration
│   ├── Performance/    # Performance optimizations
│   ├── PromoCodes/     # Promo code system
│   ├── Quiz/           # Quiz system
│   ├── Reroll/         # Reroll service and quality
│   └── Services/       # Quest services
└── Utils/              # Utility classes
```
