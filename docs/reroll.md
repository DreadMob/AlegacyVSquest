# Alegacy VS Quest Reroll System

> **Documentation Version:** v3.1.0

---

## What is Reroll?

The **Reroll System** (also called "Озарение" / Revelation) allows players to exchange multiple boss hunt reward items from the same category for a randomly selected item from that category.

---

## How It Works

1. **Collect Items** — Defeat boss hunt targets to receive reward items
2. **Accumulate** — Collect enough items from the same boss category (typically 2)
3. **Open Dialog** — Talk to the reroll NPC to open the reroll dialog
4. **Select Category** — Choose which category to reroll
5. **Animation** — A spinning animation shows items cycling with sound effects
6. **Auto-Claim** — After 3 seconds, the item is automatically claimed and the dialog closes

---

## Configuration File

Reroll groups are defined in `config/rerollconfig.json`:

```json
{
  "rerollGroups": [
    {
      "id": "ossuarywarden",
      "name": "Страж Оссуария",
      "itemsRequired": 2,
      "applyQuality": false,
      "rewardItems": [
        "albase:bosshunt-reward-miner-belt",
        "albase:bosshunt-reward-bloodthirsty",
        "albase:bosshunt-reward-ossuary-mask"
      ]
    }
  ],
  "questToGroupMapping": {
    "albase:bosshunt-ossuarywarden": "ossuarywarden"
  }
}
```

---

## RerollGroup Properties

| Property | Type | Description |
|----------|------|-------------|
| `id` | string | Unique identifier for the group (required) |
| `name` | string | Display name shown in the dialog |
| `itemsRequired` | int | Number of items needed to perform a reroll |
| `applyQuality` | boolean | Whether to apply random quality to the result item |
| `rewardItems` | array | List of action item IDs that can be obtained |

---

## Quest Mapping

The `questToGroupMapping` connects quest IDs to reroll groups. When a quest is completed, its reward items become available for rerolling in the mapped group.

---

## User Interface

### Reroll Dialog
- Shows all available categories
- Displays item count per category
- Shows icon of the first reward item
- Button "Озарение" (Revelation) to initiate reroll

### Animation
- Spinning item icon in the center
- Sound effects that slow down with the animation
- No text labels (only item icon)
- Auto-closes after 3 seconds with automatic item claiming

---

## Server-Side Logic

- `RerollService.cs` — Core service managing groups, counting items, executing rerolls
- `PendingRerollReward` — Stores reward until animation completes
- `ClaimReward()` — Gives the item to player after animation

## Client-Side Logic

- `RerollDialogGui.cs` — Main dialog showing categories
- `RerollAnimationGui.cs` — Animation with spinning icons
- `SimpleSpinAnimation.cs` — Animation implementation with decelerating spin
