# Boss Hunt System

> **Documentation Version:** v3.0.0

## Overview
The boss hunt system spawns a single active boss at a time, rotates it on a long schedule, and provides a questline with a tracker item. The active boss is chosen from configs in `quests/albase/assets/albase/config/bosshunt/*.json`.

## Core concepts
- **Boss config**: JSON file describing one boss (bossKey, entity code, quest id, points, timers).
- **Active boss**: Only one boss is considered active at a time. Rotates every `rotationDays` (currently 360 in-game days) per config.
- **Anchors/points**: Bosses can spawn at configured points, or at anchor points set in-world (Boss Hunt Anchor block). Anchors can be named and ordered, and override `points` when present.

## Rotation logic
- The system keeps `activeBossKey` and `nextBossRotationTotalHours` in world state.
- On tick, it selects an active config if none is set or the rotation time has passed.
- The active boss key is rotated deterministically (sorted by bossKey, then next in list).

## Spawn/relocation
- Boss spawns when a player is near the active boss point (`activationRange`).
- Boss relocates on an interval (`relocateIntervalHours`) unless recently damaged.
- Respawn is delayed by `respawnInGameHours` after death.
- Bosses respect `playerLockRange` for tracking and relocation rules.

## Tracker item
- Single action item: `albase:bosshunt-tracker`.
- Uses the `trackboss` action with no args; the active boss key is resolved automatically.
- Tracking only works when the player has the **active boss quest**.
- Cooldown is 5 minutes and costs 2 HP per use.

## Commands
- `/avq bosshunt status`: Shows the current active boss key and time until rotation.
- `/avq bosshunt skip`: Forces rotation to the next boss entry.
- Both commands require `give` privilege.

## Questgiver behavior
- Boss hunter NPC uses `bosshuntactiveonly: true` to offer only the active boss quest.
- Intro quest is always offered once and grants the tracker item.

## Config fields
Each boss config supports:
- `bossKey` (string)
- `questId` (string)
- `points` (list of `x,y,z,dim` strings)
- `rotationDays`
- `relocateIntervalHours`
- `respawnInGameHours`
- `noRelocateAfterDamageMinutes`
- `activationRange`
- `playerLockRange`
