# Alegacy VS Quest Chat Commands

> **Documentation Version:** v3.0.0

All commands require **`give` privilege** and are accessed via the `/avq` command.

---

## Command Reference

### Core

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/avq reload` | `[radiusBlocks]` | Reloads mod configs (`questconfig.json`, `alegacy-vsquest-config.json`). Does not reload assets. Optional `radiusBlocks` (default 512) for reloading bosshunt anchors. |
| `/avq fixplayer` | `[playerName]` | Clears common boss debuffs and stuck watched attributes on an online player. |

### Action Items

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/avq actionitems` | — | Lists all registered action items from `itemconfig.json` |
| `/avq getactionitem` | `<itemId> [amount]` | Gives an action item to yourself. Default amount is 1 |

---

### Entity Management

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/avq entities spawned` | — | Lists all currently loaded Quest Giver NPCs (entity ID, code, position) |
| `/avq entities all` | — | Lists all entity types from a quest pack domain (`assets/<domain>/entities`) |

---

### Quest Management

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/avq qlist` | — | Lists all registered quest IDs and their titles |
| `/avq qcheck` | `<playerName>` | Shows active/completed quests and progress for a player |
| `/avq qcomplete` | `<questId> <playerName>` | Force-completes a specific active quest for a player |
| `/avq qcompleteactive` | `[playerName]` | Force-completes the player's currently active quest |
| `/avq qca` | `[playerName]` | Alias for `/avq qcompleteactive` |
| `/avq qstart` | `<questId> <playerName>` | Starts a quest for a player |
| `/avq qforgive` | `<modeOrQuestId> [playerName]` | Resets quests or notes for a player: `all`, `notes`, `active`, or a specific quest id |
| `/avq qfa` | `[playerName]` | Alias for `/avq qforgive active` |
| `/avq qfall` | `[playerName]` | Alias for `/avq qforgive all` |
| `/avq nforgive` | `[playerName]` | Removes all note entries from the journal for a player |
| `/avq exec` | `[playerName] <actionString>` | Executes an action string on a player. If no player is given, uses the caller |

---

### Player Attributes

Player attributes are persistent flags used for quest progress tracking.

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/avq attr list` | `<playerName>` | Lists all watched quest attributes for an online player |
| `/avq attr set` | `<playerName> <key> <value>` | Sets a string attribute on an online player |
| `/avq attr remove` | `<playerName> <key>` | Removes an attribute from an online player |

---

### Boss Hunt

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/avq bosshunt status` | — | Shows the current active boss key and time until rotation |
| `/avq bosshunt skip` | — | Forces rotation to the next boss entry |

### Action Item Durability

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/avq ai repair` | — | Repair held item to max durability |
| `/avq ai destruct` | `<amount>` | Damage held item by a value |

### WatchedAttributes

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/avq wattr setint` | `[playerName] <key> <value>` | Sets an int WatchedAttribute |
| `/avq wattr setfloat` | `[playerName] <key> <value>` | Sets a float WatchedAttribute |
| `/avq wattr addint` | `[playerName] <key> <delta>` | Adds delta to an int WatchedAttribute |
| `/avq wattr addfloat` | `[playerName] <key> <delta>` | Adds delta to a float WatchedAttribute |
| `/avq wattr setbool` | `[playerName] <key> <value>` | Sets a bool WatchedAttribute |
| `/avq wattr setstring` | `[playerName] <key> <value...>` | Sets a string WatchedAttribute |
| `/avq wattr remove` | `[playerName] <key>` | Removes a WatchedAttribute key |

---

## Examples

```
# List all available quests
/avq qlist

# Check player's quest progress
/avq qcheck PlayerName

# Give action item
/avq getactionitem example_sword 1

# Reset a specific quest for a player
/avq qforgive innkeeper-firstimpression PlayerName

# Reset all quest progress for a player
/avq qforgive all PlayerName

# Remove all note entries for a player
/avq nforgive PlayerName

# Forget the active quest for a player
/avq qforgive active PlayerName

# Set a custom attribute
/avq attr set PlayerName talked_to_innkeeper true

# View player's attributes
/avq attr list PlayerName
```

---

## Notes

- All `<playerName>` arguments require the player to be **online**
- Quest IDs are the filename without `.json` extension (e.g., `innkeeper-firstimpression`)
- Action item IDs are defined in the quest pack's `itemconfig.json`
