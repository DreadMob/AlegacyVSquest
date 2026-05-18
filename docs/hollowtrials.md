# Hollow Trials

> **Documentation Version:** v3.1.0

---

## Overview

Hollow Trials is a solo boss challenge system with difficulty tiers, combat challenges, weekly modifiers, and its own currency. Unlike Boss Hunt, Hollow Trials are designed for solo play: if multiple players deal damage to a boss, the kill is voided for everyone.

---

## Core Concepts

### Tiers

Each trial boss has 3 difficulty tiers with escalating stats:

| Tier | Description | Base Reward |
|------|-------------|-------------|
| T1 | Base difficulty | 15 rep + 15 shards |
| T2 | Higher HP, damage, speed; shorter enrage | 30 rep + 30 shards |
| T3 | Maximum stats, shortest enrage timer | 50 rep + 50 shards |

Tier access is gated by reputation:
- T1: available to all
- T2: requires 100 reputation
- T3: requires 300 reputation

### Void Rift Anchor

A special block placed in the world by admins. Players summon the trial boss by right-clicking the anchor. Each anchor is assigned a boss during rotation.

### Solo Enforcement

If more than 1 player deals damage to a trial boss, the kill is voided for everyone. No rewards are granted.

---

## Rotation

- Rotation occurs every `RotationDays` (default 60 in-game days)
- At rotation, `ActiveTrialCount` (default 3) active bosses are selected
- Bosses are sorted by `trialKey` and selected deterministically
- A new weekly modifier is randomly chosen at each rotation

---

## Weekly Modifiers

One modifier is randomly selected at each rotation, affecting all active trial bosses:

### Negative (boss stronger)

| Modifier | Effect |
|----------|--------|
| BossHpUp | +25% boss HP |
| BossDamageUp | +20% boss damage |
| BossSpeedUp | +15% boss speed |
| EnrageSpeedup | Enrage timer 25% shorter |
| AbilityCooldownReduced | Ability cooldowns 20% shorter |
| NoVulnerability | No vulnerability windows |
| BossRegen | Boss regenerates 0.3% HP/sec always |
| HealingReduced | Player healing reduced by 50% |

### Positive (player benefit)

| Modifier | Effect |
|----------|--------|
| DoubleShards | ×2 Void Shard rewards |
| VulnerabilityExtended | Vulnerability windows 50% longer |
| ReputationBoost | +50% reputation from kills |

### Combo (negative + compensation)

| Modifier | Effect |
|----------|--------|
| Fortified | +40% boss HP, but ×2.5 shards |
| GlassCannon | +30% boss damage, but ×3 damage during vulnerability |
| Desperate | Enrage 40% faster, but +75% reputation |

---

## Challenges

Optional conditions that grant bonus reputation, shards, and improve reward quality chances when completed.

| Challenge | Condition |
|-----------|-----------|
| `speedkill` | Kill the boss within N minutes (configurable per tier) |
| `deathless` | Don't die during the fight |
| `nofood` | Don't eat during the fight |
| `nopotions` | Don't use potions during the fight |
| `lowgear` | Don't wear armor above the specified tier |
| `lowmaxhp` | Max HP must be ≤ 16 (poor nutrition) |
| `perfectdodge` | Don't get hit by a specific ability |

### Challenge Bonus Reputation

| Tier | Reputation per challenge completed |
|------|-----------------------------------|
| T1 | +10 |
| T2 | +15 |
| T3 | +25 |

---

## Progression

### Reputation

Reputation never decreases. It determines tier access and shop unlocks.

| Rank | Required Rep |
|------|-------------|
| Novice | 0 |
| Tested | 100 |
| Hardened | 300 |
| Void Master | 600 |
| Abyss Conqueror | 1000 |

### Void Shards

Currency spent in the Trial Shop. Earned alongside reputation on boss kills.

Sources:
- Base kill reward (15/30/50 by tier)
- First kill bonus (+30)
- Challenge bonuses
- Progressive difficulty bonus (+2 per 5 kills, max +20)
- Modifier multipliers

### Progressive Difficulty

Every 5 kills of the same boss:
- +5% boss HP and damage (max +50% at 50 kills)
- +2 bonus shards (max +20)

### Personal Best

The system tracks per player per boss/tier:
- Best kill time
- Total kill count
- Deathless kill count
- Best challenges completed in a single kill

---

## Trial Shop

The Trial Warden NPC offers a shop using Void Shards as currency.

### Item Types

1. **Fixed items** — fixed price, optionally with a fixed quality tier
2. **Cases** — virtual items that open into a random item from a pool with a quality roll

### Shop Item Configuration

```json
{
  "itemCode": "case:tier2",
  "nameKey": "yourmod:trial-shop-case-tier2",
  "cost": 50,
  "requiredReputation": 100,
  "maxPurchases": 0,
  "caseTier": 2,
  "casePool": ["sword-shadow", "armor-void-chest", "ring-abyss"],
  "fixedQuality": 0
}
```

| Field | Type | Description |
|-------|------|-------------|
| `itemCode` | string | Item code to give, or `case:tierN` for virtual cases |
| `nameKey` | string | Localization key for display name (optional) |
| `cost` | int | Cost in Void Shards |
| `requiredReputation` | int | Minimum reputation to see this item (0 = always visible) |
| `maxPurchases` | int | Max purchases per player (0 = unlimited) |
| `caseTier` | int | For cases: tier of the case (1, 2, or 3) |
| `casePool` | string[] | For cases: pool of base item codes |
| `fixedQuality` | int | Force a quality tier (1-4), 0 = no quality |

---

## Quality System

4 quality tiers for trial rewards:

| Quality | Multiplier | Color |
|---------|-----------|-------|
| Dim | 1.0x | #9CA3AF (gray) |
| Shimmering | 1.2x | #60A5FA (blue) |
| Radiant | 1.45x | #A78BFA (purple) |
| Abyssal | 1.75x | #F59E0B (gold) |

### Base Chances

| Quality | Base % |
|---------|--------|
| Dim | 50% |
| Shimmering | 30% |
| Radiant | 15% |
| Abyssal | 5% |

### Chance Modifiers

- **Tier 2**: +5% Shimmering, +3% Radiant
- **Tier 3**: +10% Shimmering, +5% Radiant, +2% Abyssal
- **1 challenge completed**: +5% Shimmering
- **2+ challenges completed**: +10% Shimmering
- **Deathless + Speedkill combo**: +3% Abyssal

---

## Combat Tracking

The system tracks in real-time during a fight:
- Damage dealt by each player (for solo enforcement)
- Player deaths
- Maximum armor tier worn (for `lowgear` challenge)
- Saturation changes (for `nofood` challenge)
- Potion usage (for `nopotions` challenge)
- Ability hits taken (for `perfectdodge` challenge)
- Maximum HP (for `lowmaxhp` challenge)

### Soft Reset

If a boss receives no damage for `SoftResetIdleHours` (default 2 hours), it despawns and can be summoned again at full HP.

---

## Configuration

### Boss Config (`config/hollowtrials/*.json`)

```json
{
  "trialKey": "yourmod:trial:boss-name",
  "entityCode": "yourmod:boss-name",
  "respawnInGameHours": 168,
  "activationRange": 120,
  "softResetIdleHours": 2.0,
  "tiers": {
    "1": {
      "questId": "yourmod:trial-boss-name-t1",
      "maxHealth": 300,
      "damageMult": 1.0,
      "speedMult": 1.0,
      "enrageTimerSeconds": 240,
      "challenges": [
        { "type": "speedkill", "thresholdMinutes": 3.0 },
        { "type": "deathless" },
        { "type": "lowgear", "maxArmorTier": 2 }
      ]
    },
    "2": {
      "questId": "yourmod:trial-boss-name-t2",
      "maxHealth": 600,
      "damageMult": 1.3,
      "speedMult": 1.1,
      "enrageTimerSeconds": 200,
      "challenges": [...]
    },
    "3": {
      "questId": "yourmod:trial-boss-name-t3",
      "maxHealth": 1000,
      "damageMult": 1.6,
      "speedMult": 1.2,
      "enrageTimerSeconds": 160,
      "challenges": [...]
    }
  }
}
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `trialKey` | string | — | Unique identifier (e.g. `yourmod:trial:boss-name`) |
| `entityCode` | string | derived from trialKey | Entity code to spawn |
| `respawnInGameHours` | double | 168 | Hours after death before respawn |
| `activationRange` | float | 120 | Distance in blocks for player detection |
| `softResetIdleHours` | double | 2.0 | Hours without damage before soft reset |
| `tiers` | dict | — | Per-tier configuration (keys: 1, 2, 3) |

### Tier Data

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `questId` | string | — | Quest ID for this tier (required) |
| `maxHealth` | float | 300 | Max HP |
| `damageMult` | float | 1.0 | Damage multiplier |
| `speedMult` | float | 1.0 | Speed multiplier |
| `enrageTimerSeconds` | int | 240 | Enrage timer in seconds |
| `challenges` | array | [] | Challenge definitions |

### Challenge Definition

| Field | Type | Description |
|-------|------|-------------|
| `type` | string | Challenge type (see Challenges section) |
| `thresholdMinutes` | double | For `speedkill`: time limit in minutes |
| `maxArmorTier` | int | For `lowgear`: max allowed armor tier |
| `abilityCode` | string | For `perfectdodge`: ability code to dodge |

### Core Config (in `alegacy-vsquest-config.json`)

```json
{
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

---

## Commands

All require `give` privilege unless noted.

| Command | Description |
|---------|-------------|
| `/avq trials status` | Shows all anchors, bosses, tiers, cooldowns, and active modifier |
| `/avq trials skip` | Kills all alive trial bosses, force-rotates, reassigns all anchors |
| `/avq trials respawn <anchorId>` | Force-respawns a trial boss at a specific anchor |
| `/avq trials reload` | Reloads trial configs from disk (requires `controlserver`) |
| `/avq trials clear` | Clears all anchor registrations and despawns all bosses (requires `controlserver`) |

---

## Quest Actions

| Action | Description |
|--------|-------------|
| `trialchallengebonuses` | Evaluates challenges and grants rewards (used in quest `actionRewards`) |
| `opentrialshop` | Opens the Trial Shop GUI |
| `resettrialtrackers` | Resets combat trackers |
| `tracktrialboss` | Shows distance to the active trial boss |

---

## Objectives

| Objective | Description |
|-----------|-------------|
| `killactivetrial` | Completes when the player kills the active trial boss |
