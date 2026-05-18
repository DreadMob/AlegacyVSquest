# Alegacy VS Quest Mod Structure Documentation

> **Documentation Version:** v3.1.0

---

## Documentation Index

| Document | Description |
|----------|-------------|
| [start.md](start.md) | Mod structure overview (this file) |
| [architecture.md](architecture.md) | Technical architecture and systems |
| [example.md](example.md) | Step-by-step quest creation guide |
| [queststages.md](queststages.md) | Quest Stages system (multi-phase quests) |
| [actions.md](actions.md) | All available quest actions |
| [objectives.md](objectives.md) | All available quest objectives |
| [actionitems.md](actionitems.md) | Action Items system |
| [itemquality.md](itemquality.md) | Item quality and attributes |
| [journal.md](journal.md) | Quest Journal (UI, storage, migration) |
| [spawner.md](spawner.md) | Quest Spawner (mobs & bosses) |
| [dialogue.md](dialogue.md) | NPC dialogue system |
| [commands.md](commands.md) | Chat commands |
| [questland.md](questland.md) | QuestLand (land-claim notifications) |
| [bosshunt.md](bosshunt.md) | Boss Hunt system (rotation, anchors, tracking) |
| [bossbehaviors.md](bossbehaviors.md) | Boss Entity Behaviors (abilities, phases, defense) |
| [hollowtrials.md](hollowtrials.md) | Hollow Trials (solo boss challenges, tiers, shop) |
| [promocodes.md](promocodes.md) | Promo Codes (redemption, rewards, admin) |
| [quiz.md](quiz.md) | Quiz System (interactive quizzes) |
| [reroll.md](reroll.md) | Reroll system (boss reward exchange) |

---

## Dependencies

From `modinfo.json`:
- **Game**: Vintage Story 1.22.1+
- **Target Framework**: .NET 10.0

---

## Mod Structure

```
vsquest/
├── src/
│   ├── BlockEntity/        # Block entity implementations
│   ├── Blocks/             # Custom blocks
│   ├── BossHunt/           # Boss Hunt rotation system
│   ├── Commands/           # Chat commands (/avq, /promo)
│   ├── Entity/             # Entity behaviors (quest givers, targets, bosses)
│   ├── Gui/                # UI components (quest dialogs, reroll, server info, promo)
│   ├── Harmony/            # Harmony patches (blocks, core, dialogue, entities, items, players)
│   ├── HollowTrials/       # Hollow Trials system (solo boss challenges)
│   ├── Item/               # Custom item classes
│   ├── Network/            # Network messages and handlers
│   ├── Quests/             # Quest definitions, actions, objectives
│   ├── Systems/            # Core mod systems
│   │   ├── ActionItems/    # Action item management
│   │   ├── Client/         # Client-side systems (music, fog, etc.)
│   │   ├── Config/         # Core configuration classes
│   │   ├── Database/       # External DB integration
│   │   ├── Performance/    # Performance optimizations
│   │   ├── PromoCodes/     # Promo code system
│   │   ├── Quiz/           # Quiz system
│   │   ├── Reroll/         # Reroll service
│   │   └── Services/       # Quest services (completion, reputation, rewards)
│   └── Utils/              # Utility classes
├── quests/                 # Quest pack assets (separate mods)
├── resources/              # Mod resources (modinfo.json, icon)
├── libs/                   # External libraries (NLayer.dll)
└── docs/                   # Documentation (this folder)
```

---

## Quest Packs

Quest content is organized into separate packs in the `quests/` folder. Each pack is a self-contained mod with its own assets, entities, and quest definitions. The framework itself is content-agnostic — quest packs provide the actual quests, bosses, and items.

---

## Configuration

Main config file: `alegacy-vsquest-config.json`

```json
{
  "Debug": false,
  "DefaultNotifyOnComplete": true,
  "ShowQuestDescriptionInHover": true,
  "OnlyCustomHoverText": false,
  "NestedLocalizationDomains": ["alegacyvsquest", "yourmod"],
  "BossHunt": {
    "Debug": false,
    "SoftResetIdleHours": 1.0,
    "RelocatePostponeHours": 0.25,
    "DefaultActivationRange": 200.0,
    "SkipBossKeys": []
  },
  "HollowTrials": {
    "Debug": false,
    "RotationDays": 60,
    "ActiveTrialCount": 3,
    "SoftResetIdleHours": 2.0,
    "DefaultActivationRange": 120,
    "DefaultRespawnHours": 168
  }
}
```
