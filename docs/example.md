# Alegacy VS Quest Example: Creating a Quest Giver

> **Documentation Version:** v3.0.0

This guide walks through creating a complete quest system using the **Priest** from `albase` as an example.

---

## What You'll Create

A quest giver NPC that:
- Has dialogue when the player talks to them
- Offers quests from a menu
- Rewards the player upon completion

---

## File Structure

```
your-mod/
├── modinfo.json
└── assets/
    └── yourmod/
        ├── config/
        │   ├── dialogue/
        │   │   └── priest.json       # NPC dialogue tree
        │   └── quests/
        │       └── priest-quest.json # Quest definition
        ├── entities/
        │   └── priest.json           # Entity definition
        └── lang/
            └── en.json               # Translations
```

---

## Step 1: modinfo.json

Declare dependency on Alegacy VS Quest:

```json
{
  "type": "content",
  "modid": "yourmod",
  "name": "Your Quest Pack",
  "version": "1.0.0",
  "dependencies": {
    "game": "1.21.6",
    "alegacyvsquest": "*"
  }
}
```

---

## Step 2: Entity Definition

Create `entities/priest.json`:

```json
{
  "code": "priest",
  "class": "EntityAgent",
  "client": {
    "renderer": "Shape",
    "shape": { "base": "albase:entity/humanoid/trader" },
    "texture": { "base": "albase:entity/humanoid/trader" },
    "behaviors": [
      { "code": "nametag", "showtagonlywhentargeted": true },
      { "code": "repulseagents" },
      { "code": "controlledphysics", "stepHeight": 1.01 },
      { "code": "interpolateposition" },
      { "code": "conversable", "dialogue": "config/dialogue/priest" },
      { "code": "questgiver" }
    ]
  },
  "server": {
    "behaviors": [
      {
        "code": "nametag",
        "showtagonlywhentargeted": true,
        "selectFromRandomName": ["Priest"]
      },
      { "code": "controlledphysics", "stepHeight": 1.01 },
      { "code": "health", "currenthealth": 1000000, "maxhealth": 1000000 },
      { "code": "conversable", "dialogue": "config/dialogue/priest" },
      {
        "code": "questgiver",
        "quests": ["yourmod:priest-quest"],
        "noAvailableQuestDescLangKey": "yourmod:no-quest",
        "noAvailableQuestCooldownDescLangKey": "yourmod:quest-cooldown",
        "selectrandom": false
      }
    ]
  },
  "hitboxSize": { "x": 0.6, "y": 1.75 }
}
```

Key behaviors:
- `conversable` — Enables dialogue
- `questgiver` — Lists available quests

---

## Step 3: Dialogue

Create `config/dialogue/priest.json`:

```json
{
  "components": [
    {
      "code": "intro",
      "owner": "npc",
      "type": "talk",
      "text": [{ "value": "yourmod:dialogue-intro" }],
      "jumpTo": "main"
    },
    {
      "code": "main",
      "owner": "player",
      "type": "talk",
      "text": [
        { "value": "yourmod:dialogue-quests", "jumpTo": "openquests" },
        { "value": "yourmod:dialogue-leave", "jumpTo": "close" }
      ]
    },
    {
      "code": "openquests",
      "owner": "npc",
      "type": "talk",
      "trigger": "openquests",
      "text": [{ "value": "yourmod:dialogue-opening" }]
    },
    {
      "code": "close",
      "owner": "npc",
      "type": "talk",
      "text": [{ "value": "yourmod:dialogue-goodbye" }]
    }
  ]
}
```

The `trigger: "openquests"` line executes the Alegacy VS Quest action to show the quest menu.

---

## Step 4: Quest Definition

Create `config/quests/priest-quest.json` (or use a single-file `config/quest.json` / `config/quests.json` with one or multiple quest objects):

```json
{
  "id": "yourmod:priest-quest",
  "cooldown": 90,
  "perPlayer": true,
  "onAcceptedActions": [
    {
      "id": "randomkill",
      "args": [
        "2", "2", "7",
        "yourmod:kill-notify",
        "playsound 'sounds/effect/writing' 0.25",
        "playsound 'sounds/tutorialsuccess' 0.5",
        "drifter-normal", "wolf", "bear"
      ]
    },
    {
      "id": "addjournalentry",
      "args": ["yourmod:priest-quest", "yourmod:priest", "yourmod:quest-journal-title", "yourmod:quest-journal-text"]
    }
  ],
  "actionObjectives": [
    { "id": "randomkill", "args": ["yourmod:priest-quest", "0"] },
    { "id": "randomkill", "args": ["yourmod:priest-quest", "1"] }
  ],
  "actionRewards": [
    { "id": "notify", "args": ["yourmod:quest-complete"] },
    { "id": "showquestfinaldialog", "args": ["yourmod:final-title", "yourmod:final-text", "yourmod:final-button"] },
    { "id": "allowcharselonce", "args": [] }
  ]
}
```

**Quest properties:**
- `id` — Unique quest identifier
- `cooldown` — Days until quest is available again
- `perPlayer` — Each player has separate progress
- `onAcceptedActions` — Run when quest is accepted
- `actionObjectives` — Conditions to complete
- `actionRewards` — Run when quest is completed

---

## Step 5: Translations

Create `lang/en.json`:

```json
{
  "yourmod:priest-quest-title": "Priest's Task",
  "yourmod:priest-quest-desc": "The priest needs your help.",
  "yourmod:priest-quest-hover": "Quest giver: Priest.",
  
  "yourmod:dialogue-intro": "Peace be with you.",
  "yourmod:dialogue-quests": "Do you have work for me?",
  "yourmod:dialogue-leave": "Goodbye.",
  "yourmod:dialogue-opening": "Let me see what needs doing.",
  "yourmod:dialogue-goodbye": "Go in peace.",
  
  "yourmod:no-quest": "No tasks available.",
  "yourmod:quest-cooldown": "Return in {0} days.",
  
  "yourmod:kill-notify": "Kill {0}x {1}.",
  "yourmod:quest-complete": "Task complete!",
  "yourmod:quest-journal-title": "Priest",
  "yourmod:quest-journal-text": "The priest asked for help...",
  "yourmod:final-title": "Priest",
  "yourmod:final-text": "Thank you for your help.",
  "yourmod:final-button": "Understood."
}
```

**Hover tooltip for quest name in chat:**
- Optional `*-hover` key controls the tooltip when hovering the quest name in completion chat.
- If `*-hover` is missing, the first non-empty line of `*-desc` is used instead.

---

## How It Works

1. **Player talks to NPC** → Dialogue starts at `intro`
2. **Player selects "quests" option** → `openquests` component runs
3. **`trigger: "openquests"` executes** → Quest menu appears
4. **Player accepts quest** → `onAcceptedActions` run, objectives set
5. **Player completes objectives** → Quest becomes completable
6. **Player returns to NPC** → `actionRewards` run
