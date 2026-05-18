# Promo Codes

> **Documentation Version:** v3.1.0

---

## Overview

The promo code system allows administrators to create codes that players can redeem for rewards. Codes can be defined in mod assets (read-only) or created at runtime via admin commands.

---

## Code Types

| Type | Description |
|------|-------------|
| `single` | One use total on the server (first come, first served) |
| `personal` | One use per player |
| `multi` | Limited total uses (see `maxUses`) |
| `unlimited` | No usage limits |

---

## Reward Types

| Type | Description | Fields |
|------|-------------|--------|
| `actionItem` | Gives an action item by ID from `itemconfig.json` | `itemId`, `amount`, `applyQuality`, `forceQuality` |
| `item` | Gives a vanilla/modded item by asset code | `itemCode` or `itemId`, `amount` |
| `quest` | Starts a quest | `questId` |
| `reputation` | Adds reputation | `reputationId`, `reputationAmount`, `reputationType` ("npc" or "faction") |

---

## Conditions

Promo codes can have redemption conditions:

| Field | Type | Description |
|-------|------|-------------|
| `validFrom` | string | Code is only valid after this date (UTC ISO format) |
| `validUntil` | string | Code is only valid until this date (UTC ISO format) |
| `requiredQuests` | string[] | Player must have completed these quests |

---

## Security

- **Rate limiting**: max `maxAttemptsPerMinute` (default 5) failed attempts per minute
- **Lockout**: after `lockoutThreshold` (default 15) failed attempts, player is locked out for `lockoutMinutes` (default 10) minutes
- **Case insensitive**: codes are case-insensitive by default
- **Security by obscurity**: invalid codes return the same error regardless of whether the code exists

---

## Configuration

### Asset-based (`config/promocodes.json`)

Defined in mod assets, read-only:

```json
{
  "settings": {
    "maxAttemptsPerMinute": 5,
    "lockoutMinutes": 10,
    "lockoutThreshold": 15,
    "caseInsensitive": true
  },
  "codes": [
    {
      "code": "WELCOME2025",
      "type": "personal",
      "maxUses": 0,
      "enabled": true,
      "message": "yourmod:promo-welcome-success",
      "rewards": [
        {
          "type": "actionItem",
          "itemId": "starter-sword",
          "amount": 1,
          "applyQuality": true
        }
      ],
      "conditions": {
        "validFrom": "2025-01-01T00:00:00Z",
        "validUntil": "2025-12-31T23:59:59Z",
        "requiredQuests": []
      }
    }
  ]
}
```

### Runtime (created via commands)

Stored in `ModConfig/alegacyvsquest/promocodes-runtime.json`. Created and managed via `/avq promo` commands.

---

## Commands

### Admin Commands (`/avq promo`)

Require `give` privilege.

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/avq promo create` | `<code> [type] [maxUses]` | Create a new promo code |
| `/avq promo delete` | `<code>` | Delete a promo code |
| `/avq promo list` | — | List all promo codes |
| `/avq promo info` | `<code>` | Show info about a promo code |
| `/avq promo addreward` | `<code> <rewardType> <itemId> [amount]` | Add a reward to an existing code |
| `/avq promo reload` | — | Reload promo code configs |
| `/avq promo reset` | `<playerName> <code>` | Reset a player's usage of a code |

### Player Command

| Command | Privilege | Description |
|---------|-----------|-------------|
| `/promo` | `chat` | Opens the promo code redemption dialog |

---

## Database Sync

When a code is redeemed, the data is synced to an external database (if configured):
- Fields: `player_uid`, `player_name`, `code`, `rewards_json`
- Endpoint: `POST /vsquest/promo-redemptions`

---

## Examples

### Creating a promo code via command

```
/avq promo create NEWYEAR personal
/avq promo addreward NEWYEAR actionItem firework-launcher 3
```

### Limited-use code

```
/avq promo create RAFFLE50 multi 50
/avq promo addreward RAFFLE50 item game:gear-rusty 1
```

### Resetting player usage

```
/avq promo reset PlayerName WELCOME2025
```
