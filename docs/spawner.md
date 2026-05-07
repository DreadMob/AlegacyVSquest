# Quest Spawner (Quest Mobs & Bosses)

> **Documentation Version:** v3.0.0

---

## Overview

Alegacy VS Quest provides a configurable block-based **Quest Spawner**:
- Block: `alegacyvsquest:questspawner`
- Block class: `BlockQuestSpawner`
- Block entity: `BlockEntityQuestSpawner`

This spawner can be used to spawn regular mobs or bosses. Spawned entities are tagged with the spawner position so the Quest Spawner can keep track of them.

---

## Placing & Default Settings

The block has default settings defined in:
- `resources/assets/alegacyvsquest/blocktypes/questspawner.json`

Default attributes:
- `maxAlive`: 3
- `spawnIntervalSeconds`: 10
- `spawnRadius`: 4
- `leashRange`: 12
- `yOffset`: 0

---

## Interactions

### Open config UI

- Right-click the block to open the config UI (`QuestSpawnerConfigGui`).

### Quick toggle (enable/disable)

- **Sneak + right-click** toggles enabled state without opening UI.
- Internally, the spawner is considered disabled if `maxAlive <= 0`.

### Quick add from entity spawner item

If you right-click the spawner while holding `alegacyvsquest:entityspawner` in the active hotbar slot (server-side):
- The spawner will append a spawn entry based on the item attribute `type`.
- You will get a chat notification: `Added spawn entry` / `Spawn entry already exists`.

---

## Config Fields

The spawner stores its config in block entity tree attributes:
- `vsquest:questspawner:maxAlive`
- `vsquest:questspawner:spawnIntervalSeconds`
- `vsquest:questspawner:spawnRadius`
- `vsquest:questspawner:leashRange`
- `vsquest:questspawner:yOffset`
- `vsquest:questspawner:entries`

### `maxAlive`

Maximum number of living entities spawned by this spawner that may exist at once.

### `spawnIntervalSeconds`

How often the spawner attempts to spawn (seconds). The spawner ticks on the server.

### `spawnRadius`

Random spawn radius around the spawner block (in blocks). Spawn position is computed as a random point in a circle.

### `leashRange`

Used for counting range fallback (and typically intended to match mob leash behavior).

### `yOffset`

Spawn Y is computed as:
- `RainMapHeightAt(x, z) + yOffset`

---

## Entries Format (Weighted Spawn Table)

The main configuration is `entries` (one per line):

- Full format: `entityCode|targetId|weight`
- Short format: `entityCode`

Where:
- `entityCode` — entity asset code (e.g. `game:wolf` or `yourmod:bossname`)
- `targetId` — optional id used for kill/objective tracking (see below)
- `weight` — integer weight for random selection

Notes:
- If `weight <= 0`, the entry is ignored.
- If `targetId` is omitted, it is not set on the entity (the entity may still have its own behavior-defined target id).

---

## Spawned Entity Tagging

When the spawner creates an entity, it tags it with the spawner block position using `EntityBehaviorQuestTarget.SetSpawnerAnchor(entity, Pos)`.

The spawner later counts and manages entities by scanning loaded entities for these watched attribute keys:
- `alegacyvsquest:spawner:dim`
- `alegacyvsquest:spawner:x`
- `alegacyvsquest:spawner:y`
- `alegacyvsquest:spawner:z`

This is why the spawner can reliably count its own spawned mobs even if they wander away.

---

## Kill/Objective Integration (targetId)

If `targetId` is present in an entry, the spawner sets:
- `alegacyvsquest:killaction:targetid = targetId`

This can be used by kill-based objectives/actions that look for a specific target id.

---

## Boss Respawn Safety

To avoid duplicate boss spawning when a boss has a respawn timer, the spawner will **block spawning** if it detects a dead spawned entity with:
- `alegacyvsquest:bossrespawnAtTotalHours` set

This prevents race conditions between the spawner and boss respawn logic.

---

## Admin Buttons (UI)

The config UI includes:
- **Enable/Disable** — toggles enabled state
- **Kill** — despawns all entities currently tagged with this spawner position
- **Save** — saves current config to the block entity
