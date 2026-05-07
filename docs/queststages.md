# Alegacy VS Quest Stages System (Quests in Quest)

> **Documentation Version:** v3.0.0

---

## Overview

The **Quest Stages** system allows creating multi-phase quests where objectives are grouped into sequential stages. When a player completes all objectives in the current stage, they see a "Stage Completed" message and the next stage's objectives appear in the same quest GUI.

This enables complex quest chains like:
- **Phase 1**: Gather initial items
- **Phase 2**: Travel to a location and complete sub-tasks
- **Phase 3**: Return to the quest giver with results

---

## How It Works

```
Quest: "Epic Journey"
├── Stage 1: "Preparation"
│   ├── Objective: Collect 5 herbs
│   └── Objective: Talk to the merchant
│   └── [Stage Complete] → Auto-advance to Stage 2
├── Stage 2: "The Journey"
│   ├── Objective: Reach the mountain
│   └── Objective: Defeat the guardian
│   └── [Stage Complete] → Auto-advance to Stage 3
└── Stage 3: "Return"
    └── Objective: Report back to the elder
    └── [Quest Complete] → Rewards given
```

---

## Quest Definition with Stages

Create a quest with the `stages` array instead of (or in addition to) regular `actionObjectives`:

```json
{
  "id": "yourmod:multi-stage-quest",
  "cooldown": -1,
  "perPlayer": true,
  "stages": [
    {
      "stageTitleLangKey": "yourmod:stage1-title",
      "gatherObjectives": [
        {
          "validCodes": ["game:herb"],
          "demand": 5
        }
      ],
      "actionObjectives": [
        {
          "id": "interactwithentity",
          "objectiveId": "merchant",
          "args": ["yourmod:multi-stage-quest", "yourmod:merchant", "1"]
        }
      ],
      "onStageCompleteActions": [
        {
          "id": "playsound",
          "args": ["sounds/tutorialstepsuccess"]
        },
        {
          "id": "addjournalentry",
          "args": ["yourmod:multi-stage-quest", "yourmod:stage1-complete-title", "yourmod:stage1-complete-text"]
        },
        {
          "id": "setplayervariable",
          "args": ["yourmod:current_stage", "2"]
        }
      ]
    },
    {
      "stageTitleLangKey": "yourmod:stage2-title",
      "actionObjectives": [
        {
          "id": "inland",
          "objectiveId": "reach_mountain",
          "args": ["mountain_zone"]
        },
        {
          "id": "killactiontarget",
          "objectiveId": "defeat_guardian",
          "args": ["yourmod:multi-stage-quest", "guardian", "yourmod:guardian_target", "1"]
        }
      ],
      "onStageCompleteActions": [
        {
          "id": "playsound",
          "args": ["sounds/tutorialstepsuccess"]
        }
      ]
    },
    {
      "stageTitleLangKey": "yourmod:stage3-title",
      "actionObjectives": [
        {
          "id": "interactwithentity",
          "objectiveId": "report",
          "args": ["yourmod:multi-stage-quest", "yourmod:elder", "1"]
        }
      ],
      "onStageCompleteActions": [
        {
          "id": "addjournalentry",
          "args": ["yourmod:multi-stage-quest", "yourmod:quest-complete-title", "yourmod:quest-complete-text"]
        }
      ]
    }
  ],
  "actionRewards": [
    {
      "id": "giveactionitem",
      "args": ["yourmod:reward_item", "1"]
    },
    {
      "id": "playsound",
      "args": ["sounds/quest-complete"]
    }
  ]
}
```

---

## Stage Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `stageTitleLangKey` | string | Yes | Language key for the stage title displayed in UI |
| `gatherObjectives` | array | No | Item collection objectives (see below) |
| `killObjectives` | array | No | Entity kill objectives (see below) |
| `actionObjectives` | array | No | Regular action-based objectives |
| `onStageCompleteActions` | array | No | Actions executed when stage completes |

---

## Objective Types in Stages

### Gather Objectives (Item Collection)

```json
"gatherObjectives": [
  {
    "validCodes": ["game:herb", "game:flower"],
    "demand": 5
  }
]
```

- `validCodes` — Array of item codes that count toward the objective
- `demand` — Number of items required

### Kill Objectives

```json
"killObjectives": [
  {
    "validCodes": ["game:drifter", "game:wolf"],
    "demand": 3
  }
]
```

- `validCodes` — Array of entity codes that count when killed
- `demand` — Number of kills required

### Action Objectives

Standard action objectives work the same as in regular quests:

```json
"actionObjectives": [
  {
    "id": "interactwithentity",
    "objectiveId": "talk_npc",
    "onCompleteActions": "playsound 'sounds/dialogue' 0.5",
    "args": ["yourmod:quest", "yourmod:npc", "1"]
  }
]
```

---

## Stage Completion Actions

The `onStageCompleteActions` array executes when all objectives in a stage are completed:

```json
"onStageCompleteActions": [
  {
    "id": "playsound",
    "args": ["sounds/stage-complete"]
  },
  {
    "id": "notify",
    "args": ["yourmod:stage-complete-message"]
  },
  {
    "id": "setplayervariable",
    "args": ["yourmod:quest_progress", "stage2"]
  },
  {
    "id": "addjournalentry",
    "args": ["yourmod:quest", "yourmod:journal-title", "yourmod:journal-stage2"]
  }
]
```

Common actions for stage completion:
- `playsound` — Play a success sound
- `notify` — Show a notification message
- `setplayervariable` — Set a player attribute for tracking
- `addjournalentry` — Add a journal entry
- `questitem` — Give the player an item for the next stage

---

## Localization

Add these language keys to your `lang/*.json`:

```json
{
  "yourmod:multi-stage-quest-title": "Epic Journey",
  "yourmod:multi-stage-quest-desc": "A multi-part adventure",
  
  "yourmod:stage1-title": "Stage 1: Preparation",
  "yourmod:stage2-title": "Stage 2: The Journey",
  "yourmod:stage3-title": "Stage 3: Return",
  
  "yourmod:stage1-complete-title": "Stage 1 Complete",
  "yourmod:stage1-complete-text": "You've prepared for the journey. Now head to the mountain.",
  
  "yourmod:quest-complete-title": "Quest Complete",
  "yourmod:quest-complete-text": "The elder thanks you for your bravery."
}
```

---

## Backward Compatibility

Quests without the `stages` array work exactly as before. The stages system is opt-in:

```json
// Traditional single-stage quest (still works)
{
  "id": "yourmod:simple-quest",
  "actionObjectives": [
    { "id": "walkdistance", "args": ["yourmod:simple-quest", "100"] }
  ]
}
```

---

## Example: ALStory's last_breath_collect

A real-world example showing 3 stages:

```json
{
  "id": "alstory:last_breath_collect",
  "stages": [
    {
      "stageTitleLangKey": "alstory:last_breath_collect-stage1-title",
      "gatherObjectives": [
        { "validCodes": ["alstory-armor-helm"], "demand": 1 },
        { "validCodes": ["alstory-armor-chest"], "demand": 1 }
      ],
      "killObjectives": [
        { "validCodes": ["alstory:alstory-eidolon-guardian"], "demand": 1 },
        { "validCodes": ["alstory:bloodhand-clawchief"], "demand": 1 }
      ]
    },
    {
      "stageTitleLangKey": "alstory:last_breath_collect-stage2-title",
      "actionObjectives": [
        { "id": "interactwithentity", "objectiveId": "heinrich", ... },
        { "id": "interactat", "objectiveId": "crystal1", ... },
        { "id": "interactat", "objectiveId": "crystal2", ... },
        { "id": "interactat", "objectiveId": "crystal3", ... },
        { "id": "interactwithentity", "objectiveId": "illa", ... }
      ]
    },
    {
      "stageTitleLangKey": "alstory:last_breath_collect-stage3-title",
      "actionObjectives": [
        { "id": "interactwithentity", "args": ["alstory:last_breath_collect", "alstory:elrik", "1"] }
      ]
    }
  ]
}
```

---

## Tips

1. **Use meaningful stage titles** — Players see these in the quest GUI
2. **Add journal entries on stage completion** — Helps players track progress
3. **Use `setplayervariable` for persistence** — Track stage progress across sessions
4. **Combine with embedded quests** — A stage can trigger a separate `village_heart` quest
5. **Stage rewards** — Give items in `onStageCompleteActions` to prepare for next stage
