# Alegacy VS Quest Objectives

> **Documentation Version:** v3.0.0

---

## What are Objectives?

**Objectives** are conditions that the player must complete to finish a quest. They track player progress and determine when a quest can be turned in.

> [!IMPORTANT]
> **Objectives** are different from **Actions**:
> - **Objectives** = Conditions the player must *complete* (kill X enemies, walk Y distance)
> - **Actions** = Things that *happen* (give item, play sound, spawn entity)

---

## When Objectives Are Checked

Objectives are defined in the `actionObjectives` array within a quest JSON. They are continuously checked while the quest is active.

```json
{
  "actionObjectives": [
    {
      "id": "objectiveId",
      "args": ["arg1", "arg2"]
    }
  ]
}
```

---

## Objective Format

```json
{
  "id": "objectiveId",
  "args": ["arg1", "arg2", "arg3"]
}
```

- `id` — The objective identifier (see list below)
- `args` — Array of string arguments passed to the objective

---

## All Available Objectives

Objectives are registered in `QuestObjectiveRegistry`.

### `walkdistance`

Requires the player to walk a certain distance in meters.

**Arguments:**
- `<questId>` — Quest ID for tracking (required)
- `<meters>` — Distance in meters to walk (required)
- `[slot]` — Objective slot for multiple walk objectives (optional)

> [!NOTE]
> Use `resetwalkdistance` action in `onAcceptedActions` to reset distance tracking when quest starts.

---

### `randomkill`

Completes when all random-kill slots rolled for the quest are finished.

This objective relies on the `randomkill` action to set up targets and store progress.

**Arguments:**
- `<questId>` — Quest ID for tracking (required)
- `<slot>` — Which random kill slot to check (required)

The `randomkill` action in `onAcceptedActions` generates the kill targets. Each slot corresponds to a randomly selected mob to hunt.

---

## Objective Helper Actions

These are quest **actions** that support objectives. They are often placed in `onAcceptedActions` or invoked by scripts/events.

### `randomkill` (action)

Rolls and initializes random kill targets for a quest and stores progress/completion callbacks.

**Arguments:**
- `<slotCount>` — How many random-kill slots to roll (required)
- `<killsPerSlot>` — Required kills per slot (required)
- `<maxMobDistance>` — Max distance in blocks for kill counting (required)
- `<notifyTemplateLangKey>` — Notification template language key, supports `{0}` = remaining kills, `{1}` = mob name (required)
- `[onProgressActionString]` — Action string executed on kill progress (optional)
- `[onCompletedActionString]` — Action string executed when all slots are complete (optional)
- `<entityCode1>` — First possible mob code (required)
- `[entityCode2...]` — Additional mob codes to pick from

---

### `resetwalkdistance` (action)

Resets the internal walk distance tracking for `walkdistance` objectives.

**Arguments:**
- `<questId>` — Quest ID used for tracking (required)
- `[slot]` — Objective slot for multiple walk objectives (optional)

---

### `checkobjective` (action)

Forces a re-check of quest objectives (useful if progress is driven by manual actions rather than events).

**Arguments:** None

---

### `markinteraction` (action)

Marks a coordinate string as interacted for objectives like `interactat`.

**Arguments:**
- `<x,y,z>` — Coordinate string to mark (required)

---

### `markentityinteraction` (action)

Marks an entity interaction for objectives like `interactwithentity`.

**Arguments:**
- `<entityId>` — Entity id as integer/long (required)

---

### `checkvariable`

Checks if a player attribute meets a condition. Supports the following comparison operators:
- `=` (equal to)
- `==` (equal to, alias for `=`)
- `>` (greater than)
- `>=` (greater than or equal to)
- `<` (less than)
- `<=` (less than or equal to)
- `!=` (not equal to)

**Arguments:**
- `<varName>` — Player attribute key to check (required)
- `<operator>` — Comparison operator (required)
- `<value>` — Value to compare against (required)
- `[actionsOnComplete]` — Action string to execute when condition is met (optional)

---

### `timeofday`

Requires a specific time of day to complete.

**Arguments:**
- `<mode>` — Time mode (required):
  - `day` — 06:00 to 18:00
  - `night` — 18:00 to 06:00
  - `startHour,endHour` — Custom range (e.g., `8,16`)

---

### `interactat`

Requires the player to interact with blocks at specific coordinates.

**Arguments:**
- `<coord1>` — First coordinate string (required)
- `[coord2...]` — Additional coordinates, all must be interacted with

Use `markinteraction` action to mark a coordinate as completed.

---

### `interactcount`

Counts interactions at multiple coordinates. Similar to `interactat` but shows progress.

**Arguments:**
- `<coord1>` — First coordinate string (required)
- `[coord2...]` — Additional coordinates
- `<displayKey>` — Language key for display text (last argument)

---

### `plantflowers`

Completes when the player has at least N flower blocks nearby.

**Arguments:**
- `<count>` — Minimum flowers required within 15 blocks (required)

---

### `hasattribute`

Checks if the player has a specific attribute with a specific value.

**Arguments:**
- `<key>` — Attribute key (required)
- `<value>` — Expected string value (required)

---

### `reachwaypoint`

Completes when the player is within range of a coordinate.

**Arguments:**
- `<x,y,z>` — Target coordinate (required)
- `[radius]` — Radius in blocks, default `2` (optional)

---

### `hasitem`

Completes when the player has at least N items matching the given code across their inventories.

**Arguments:**
- `<itemCode>` — Full code, supports `*` suffix wildcard (required)
- `<need>` — Required amount (required)

---

### `wearing`

Completes when the player is wearing an item matching the given code in the character inventory.

**Arguments:**
- `<itemCode>` — Full code, supports `*` suffix wildcard (required)
- `[slotIndex]` — Character inventory slot index to check (optional). If omitted, checks all slots.

---

### `interactwithentity`

Counts interactions with a specific entity.

**Arguments:**
- `<questId>` — Quest ID used for tracking (required)
- `<entityId>` — Entity id as integer/long (required)
- `<need>` — Required interaction count (required)

---

### `inland`

Completes when the player is currently inside a land claim with the given name.

**Arguments:**
- `<claimName>` — Land claim name to match (required)

---

### `landgate`

Progress gate: allows quest progress only while the player is inside a land claim with the given name.

This is meant to be used as a *gate* (similar to `timeofday`) and can be applied to the whole quest or a specific objective.

**Arguments:**
- `<claimName>` — Land claim name required for progress (required)
- `[objectiveId]` — If set, gate only applies when progressing the specified `actionObjective.objectiveId` (optional). If omitted, applies to all quest progress.
- `[prefix]` — Optional text prefix to display before progress lines (optional)
- `[hidePrefix]` — `true`/`1` to disable showing `prefix` (optional)

---

### `killnear`

Counts kills near a coordinate, optionally filtered by mob code.

**Arguments:**
- `<questId>` — Quest ID used for tracking (required)
- `<objectiveId>` — Must match `actionObjective.objectiveId` (required)
- `<x,y,z>` — Center coordinate (required)
- `<radius>` — Radius (required)
- `[mobCode]` — Entity code filter, default `*` (optional)
- `<need>` — Required kill count (required)

---

### `killactiontarget`

Counts kills of entities that have a matching `questtarget` behavior target id.

This is intended for “kill the marked target” style quests where the spawned entity is tagged via `EntityBehaviorQuestTarget`.

**Arguments:**
- `<questId>` — Quest ID used for tracking (required)
- `<objectiveId>` — Must match `actionObjective.objectiveId` (required)
- `<targetId>` — Required target id from `questtarget` behavior (required)
- `<need>` — Required kill count (required)

---

### `sequence`

Completes a list of other action objectives in a required order.

**Arguments:**
- `<questId>` — Quest ID (required)
- `<sequenceId>` — Sequence storage id (required)
- `<objectiveId1>` — Objective id to complete first (required)
- `<objectiveId2...>` — Next objective ids in order (required)

---

### `temporalstorm`

Counts temporal storms survived. A storm is counted when it transitions from active to inactive.

**Arguments:**
- `<questId>` — Quest ID used for tracking (required)
- `<objectiveId>` — Must match `actionObjective.objectiveId` (required)
- `<needStorms>` — Required storms survived (required)
