# Alegacy VS Quest Item Quality System

> **Documentation Version:** v3.1.0

---

## What is Item Quality?

The **Item Quality System** allows action items to have randomized quality tiers when obtained as rewards. Quality affects item attributes and is visually indicated in the item name.

---

## Quality Tiers

| Tier | Name | Color | Attribute Multiplier |
|------|------|-------|---------------------|
| 1 | Common | White | 1.0x |
| 2 | Uncommon | Green | 1.15x |
| 3 | Rare | Blue | 1.3x |
| 4 | Epic | Purple | 1.5x |
| 5 | Legendary | Gold | 1.75x |

---

## How Quality Affects Attributes

When an item with quality is created, all numerical attributes are multiplied by the quality multiplier:

- **Positive attributes** (bonuses) are multiplied up
- **Negative attributes** (penalties) are multiplied down (made less severe)

Example:
```
Base item: attackpower = 2.5
Rare quality (1.3x): attackpower = 3.25
Epic quality (1.5x): attackpower = 3.75
```

---

## Configuration

### Per-Item Quality Settings

In `itemconfig.json`, individual items can have quality settings:

```json
{
  "id": "albase:bosshunt-reward-example",
  "itemCode": "game:item-code",
  "name": "Example Item",
  "quality": {
    "enabled": true,
    "minTier": 1,
    "maxTier": 5
  }
}
```

### Reroll Group Quality Settings

In `rerollconfig.json`, entire groups can have quality applied:

```json
{
  "id": "ossuarywarden",
  "name": "Страж Оссуария",
  "itemsRequired": 2,
  "applyQuality": true
}
```

When `applyQuality` is `true`, all items from this reroll group will receive random quality.

---

## Attribute System

Items in `itemconfig.json` can define custom attributes that modify player stats:

```json
{
  "attributes": {
    "attackpower": 2.5,
    "maxhealthflat": -3.0,
    "hungerrate": 0.4,
    "walkspeed": -0.05,
    "knockbackmult": 0.6
  }
}
```

### Common Attributes

| Attribute | Description |
|-----------|-------------|
| `attackpower` | Melee damage bonus |
| `maxhealthflat` | Flat health modifier |
| `hungerrate` | Hunger consumption modifier |
| `walkspeed` | Movement speed modifier |
| `knockbackmult` | Knockback resistance |
| `rangedaccuracy` | Ranged accuracy bonus |
| `rangeddamagemult` | Ranged damage multiplier |
| `rangedchargspeed` | Ranged charge speed |
| `maxoxygen` | Oxygen capacity |
| `temporaldrainmult` | Temporal drain modifier |
| `viewdistance` | View distance modifier |

---

## Display Configuration

Control which attributes are shown in tooltips:

```json
{
  "showAttributes": [
    "attackpower",
    "maxhealthflat",
    "hungerrate"
  ],
  "hideVanillaTooltips": [
    "armor",
    "attackpower",
    "protection"
  ]
}
```

| Property | Description |
|----------|-------------|
| `showAttributes` | List of custom attributes to display |
| `hideVanillaTooltips` | List of vanilla tooltip sections to hide |
