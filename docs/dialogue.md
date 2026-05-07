# Alegacy VS Quest Dialogues

> **Documentation Version:** v3.0.0

---

## Overview

Dialogues in Vintage Story are part of the vanilla **EntityBehaviorConversable** system. NPCs with this behavior can have conversations with players through a dialogue UI. Alegacy VS Quest extends this system by intercepting the `trigger` property to execute quest actions.

This documentation covers the vanilla dialogue format with notes on Alegacy VS Quest-specific features.

---

## File Location

Dialogues are placed in `config/dialogue/<name>.json` within your mod's assets folder.

---

## Basic Structure

A dialogue file contains an array of **components** — individual steps in a conversation:

```json
{
  "components": [
    { "code": "intro", ... },
    { "code": "main", ... },
    { "code": "close", ... }
  ]
}
```

Each component has a unique `code` that other components can reference via `jumpTo`.

---

## Component Properties

| Property | Description |
|----------|-------------|
| `code` | Unique identifier |
| `owner` | `npc` or `player` |
| `type` | `talk` or `condition` |
| `text` | Array of text entries |
| `jumpTo` | Next component code |
| `trigger` | Action string to execute |
| `setVariables` | Variables to set |
| `sound` | Sound to play |

---

## Component Type: talk

### NPC Speech

When `owner` is `npc`, the component displays the NPC's lines and automatically jumps to the next component:

```json
{
  "code": "greeting",
  "owner": "npc",
  "type": "talk",
  "text": [
    { "value": "modid:dialogue-greeting" }
  ],
  "jumpTo": "main"
}
```

### Player Choices

When `owner` is `player`, the text entries become clickable options. Each option has its own `jumpTo`:

```json
{
  "code": "main",
  "owner": "player",
  "type": "talk",
  "text": [
    { "value": "modid:option-quests", "jumpTo": "openquests" },
    { "value": "modid:option-leave", "jumpTo": "close" }
  ]
}
```

---

## Component Type: condition

Checks a variable and branches:

```json
{
  "code": "checkfirstmeet",
  "owner": "npc",
  "type": "condition",
  "variable": "entity.hasmet",
  "isNotValue": "true",
  "thenJumpTo": "firstmeet",
  "elseJumpTo": "welcomeback"
}
```

---

## Variables

Variables are stored with different scopes:
- `entity.<name>` — On the NPC entity
- `player.<name>` — On the player entity
- `global.<name>` — Global scope

### Setting Variables

```json
{
  "setVariables": {
    "entity.hasmet": "true",
    "player.talkedtoinnkeeper": "true"
  }
}
```

### Checking Variables

Via condition component or text entry conditions.

---

## Conditional Text Options

Show/hide player choices based on conditions:

```json
{
  "value": "modid:special-option",
  "jumpTo": "special",
  "conditions": [
    { "variable": "entity.unlocked", "isValue": "true" }
  ]
}
```

---

## Alegacy VS Quest Trigger Integration

The `trigger` property executes Alegacy VS Quest action strings when the component is entered:

```json
{
  "code": "openquests",
  "owner": "npc",
  "type": "talk",
  "trigger": "openquests",
  "text": [
    { "value": "modid:dialogue-quests-opening" }
  ]
}
```

Multiple actions separated by `;`:

```json
{
  "trigger": "playsound 'sounds/effect/writing' 0.5;addjournalentry yourmod:questid yourmod:lorecode 'Title' 'Text'"
}
```

See [Actions](actions.md) for all available actions.
