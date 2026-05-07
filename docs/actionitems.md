# Alegacy VS Quest Action Items

> **Documentation Version:** v3.0.0

---

## What are Action Items?

**Action Items** are special items that execute quest actions when the player right-clicks them. They are defined in `itemconfig.json` files and can customize the item's name, description, tooltip display, and behavior.

> [!IMPORTANT]
> Action Items are different from regular items:
> - **Regular Items** — Standard game items with vanilla behavior
> - **Action Items** — Items configured to trigger quest actions on use

---

## How Action Items Work

1. **Configuration** — Action Items are defined in `config/itemconfig.json` within a quest pack
2. **Registration** — On game load, `ItemSystem` scans all mods for `itemconfig.json` files and registers them to `ActionItemRegistry`
3. **Item Creation** — When an action item is given (via `questitem` action), the item receives special attributes including `alegacyvsquest:actions`
4. **Execution** — When the player right-clicks the item, the client sends a packet to the server, which executes all configured actions

---

## Configuration File

Action Items are defined in `config/itemconfig.json`:

```json
{
  "actionItems": [
    {
      "id": "unique-item-id",
      "itemCode": "game:item-code",
      "name": "Custom Display Name",
      "description": "Custom item description",
      "actions": [],
      "modes": [],
      "attributes": {},
      "showAttributes": [],
      "hideVanillaTooltips": []
    }
  ]
}
```

---

## ActionItem Properties

| Property | Type | Description |
|----------|------|-------------|
| `id` | string | Unique identifier for the action item (required) |
| `itemCode` | string | Base item/block code including domain (required) |
| `name` | string | Custom display name, supports rich text formatting |
| `description` | string | Custom description, supports rich text and `\n` |
| `actions` | array | List of actions to execute on right-click |
| `modes` | array | Optional tool modes that override `actions` based on selected mode |
| `sourceQuestId` | string | Optional quest id used as action execution context. If omitted, defaults to `item-action`. |
| `triggerOnInventoryAdd` | bool | If true, actions auto-trigger once when the item enters the player inventory. Manual right-click triggering is disabled. |
| `blockMove` | bool | If true, prevents moving the item outside the hotbar (hotbar + mouse cursor only). This blocks moving into backpacks, chests, etc. |
| `blockEquip` | bool | If true, prevents putting the item into character equipment slots (uses a Harmony patch on `ItemSlotCharacter`). |
| `blockDrop` | bool | If true, prevents manual dropping (DropItem). |
| `blockDeath` | bool | If true, prevents dropping on player death. |
| `blockGroundStorage` | bool | If true, prevents placing the item into ground storage via `Shift` + Right Click. |
| `attributes` | object | Custom float attributes (e.g., stats) |
| `showAttributes` | array | Which custom attributes to show in tooltip |
| `hideVanillaTooltips` | array | Which vanilla tooltip sections to hide |

> [!NOTE]
> `triggerOnInventoryAdd` auto-trigger is executed **once per action item id** and is tracked on the player. If `sourceQuestId` is set to a real quest id, the auto-trigger will only fire while that quest is active.

---

## Actions Array

Each action in the `actions` array follows this format:

```json
{
  "id": "actionId",
  "args": ["arg1", "arg2"]
}
```

Any action from the [Actions](actions.md) documentation can be used here.

---

## Modes (Tool Mode UI)

Action items can define **tool modes** that show the vanilla F-mode selector. If modes are present, the selected mode determines which actions are executed.

```json
"modes": [
  {
    "id": "track",
    "name": "Track",
    "icon": "search",
    "actions": [
      { "id": "trackboss", "args": [] }
    ]
  },
  {
    "id": "silent",
    "name": "Silent",
    "icon": "mute",
    "actions": [
      { "id": "notify", "args": ["albase:tracker-silent"] }
    ]
  }
]
```

- `icon` uses vanilla icon codes (same set as other tool mode UIs). If omitted, the first letter of `name` is used.
- Mode index is stored on the item stack and synced via vanilla tool mode UI.

---

## Available Attributes

Custom attributes that can be applied to items via the `attributes` object. These attributes add actual stat bonuses to the item.

| Attribute | Effect | Display Format |
|-----------|--------|----------------|
| `attackpower` | Adds bonus attack damage | +X hp |
| `warmth` | Adds warmth to wearable items | +X°C |
| `protection` | Flat damage reduction (armor) | +X dmg |
| `walkspeed` | Modifies walk speed | +X% |
| `hungerrate` | Modifies hunger rate | +X% |
| `healingeffectiveness` | Modifies healing received | +X% |
| `rangedaccuracy` | Modifies ranged weapon accuracy | +X% |
| `rangedchargspeed` | Modifies ranged weapon charge speed | +X% |
| `rangeddamagemult` | Modifies ranged weapon damage | +X% |
| `miningspeedmult` | Modifies mining speed | +X% |
| `falldamagemult` | Modifies fall damage taken | +X% |
| `temporaldrainmult` | Modifies temporal stability drain | +X% |
| `jumpheightmul` | Modifies jump height | +X% |
| `knockbackmult` | Modifies knockback dealt | +X% |
| `maxhealthflat` | Adds max health | +X hp |
| `maxoxygen` | Adds max oxygen (displayed as seconds) | +X |
| `stealth` | Reduces detection (AI aggro) | +X |
| `secondchancecharges` | Second chance (auto-revive) charges | X |
| `weightlimit` | Modifies weight limit | +X% |
| `viewdistance` | Modifies fog/view distance | +X% |
| `uraniummaskchargehours` | Uranium mask charge in hours | Xh |
| `healonkill` | Heals percentage of max HP on kill | +X% |

> [!NOTE]
> Percentage-based attributes use decimal values (e.g., `0.1` = 10%, `0.25` = 25%)

---

## Tooltip Customization

### showAttributes

List of custom attribute keys to display in the item tooltip. Only attributes listed here will be shown.

```json
"showAttributes": ["protection", "warmth"]
```

### hideVanillaTooltips

List of vanilla tooltip sections to suppress. Available values:

| Value | Hides |
|-------|-------|
| `durability` | Durability: X / Y |
| `miningspeed` | Tool tier and mining speeds |
| `storage` | Bag storage slots and contents |
| `nutrition` | Food satiety and nutrition info |
| `attackpower` | Vanilla attack power and tier |
| `combustible` | Burn temperature, smelting info |
| `grinding` | Grinding output |
| `crushing` | Pulverizer output |
| `temperature` | Current item temperature |
| `modsource` | Which mod the item is from |

---

## Example

```json
{
  "actionItems": [
    {
      "id": "memorial-scarf-2026",
      "itemCode": "game:clothes-shoulder-artisans-scarf",
      "name": "<font color=\"#8B5CF6\">Memorial Scarf 2026</font>",
      "description": "<font color=\"#60A5FA\">A warm scarf with special embroidery.\n</font>",
      "actions": [],
      "attributes": {
        "warmth": 2.0,
        "protection": 200.0,
        "attackpower": 200.0
      },
      "showAttributes": ["protection"],
      "hideVanillaTooltips": ["attackpower"]
    }
  ]
}
```

This creates a scarf item that:
- Has a purple colored name
- Shows only the `protection` attribute in tooltip
- Hides the vanilla `attackpower` tooltip section
- Has no actions (purely decorative item)

---

## Giving Action Items

Use the `questitem` action to give an action item to a player:

```json
{
  "id": "questitem",
  "args": ["memorial-scarf-2026"]
}
```

Or via dialogue string:
```
questitem memorial-scarf-2026
```
