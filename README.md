# Kits

Oxide plugin for Rust. Item kits, autokits, kit cooldowns, and more.

Allow your players to redeem pre-made kits of items

![Screenshot_1](https://i.gyazo.com/78a0ebca7f9e80a52c8f7e3f63f742c1.jpg)
![Screenshot_2](https://i.gyazo.com/507889a73657508b080ea48f4d774511.jpg)

## Features

* Easy to use UI menu for creating and claiming kits
* Set redeem limitations with maximum uses, cooldowns and purchase costs
* Create VIP kits that require custom permissions
* Create admin kits that use Rusts auth-level system
* Give auto-kits on death by specifying a list of kits in the config and have the granted using a priority system
* Works with HumanNPC to have kits that are only available via NPC interaction
* Supports CopyPaste by allowing kits to paste down pre-created structures
* Customize the UI color scheme in the config

## Permissions

> This plugin uses the permission system. To assign a permission, use `oxide.grant <user or group> <name or steam id> <permission>`. To remove a permission, use `oxide.revoke <user or group> <name or steam id> <permission>`.

* `kits.admin` Players with this permission can use the admin command functionality

## Commands

> This plugin provides both chat and console commands using the same syntax. When using a command in chat, prefix it with a forward slash: `/`.

### Player Chat Commands

* `/kit` Opens the kit menu
* `/kit <name>` Used by player to claim the specified kit
* `/kit autokit` Allows the player to toggle whether they recieve kits when spawning. This option must be enabled in the config

### Admin Chat Commands

* `/kit` help Show the help menu with all available commands
* `/kit list` Show the full list of kit names
* `/kit add/kit new` Start creating a new kit
* `/kit edit <kitname>` Start editing a previously created kit
* `/kit remove <kitname>` | `/kit delete <name>` Delete the specified kit
* `/kit give <player name | player id> <kitname>` Give the specified player the specified kit
* `/kit givenpc <kitname>` Give the specified kit to the NPC you are currently looking at
* `/kit reset` Reset all player kit usage data

### Admin Console Commands

* `kit list` List all kits
* `kit remove <kitname>` | `kit delete <kitname>` Delete the specified kit
* `kit give <player name | player id> <kitname>` Give the specified player the specified kit
* `kit reset` Reset all player kit usage data

## How to create kits

Before starting, clear your inventory and use the admin menu to give yourself all the items you want in the kit.

Once you have done that either type "/kit new" in chat, or press the "Create New" button in the top right of the UI.

You will presented with the following screen

![Screenshot_3](https://i.gyazo.com/adf3ec15ed371e19c3a2269a1cb2fe54.jpg)

All the kit options are on the left hand side, and the item view is on the right.

Start by pressing the "Copy From Inventory" button in the bottom right. This will add all the items from your inventory in to the kit.

You can then proceed to fill out the information on the left side.

### Kit Details

* `Name` is the name of the kit
* `Description` is the kit description. This is not a required parameter
* `Icon URL` is the URL for the kit image show in the UI grid. This is not a required parameter

### Usage Authority

* `Permission` is used to restrict usage of this kit to only players with the specified permission. The permission should be prefixed with 'kits.' (example 'kits.awesomekit').
* `Auth Level` is used to restrict usage of this kit to players with at least the specified auth level (0 is player, 1 is moderator, 2 is admin)
* `Is Hidden` is a toggle field. When enabled this kit will not be visible in the kit grid to regular players

### Usage Details

* `Maximum Uses` is used to specify a maximum number of times a player can claim this kit per wipe cycle
* `Cooldown` is the amount of time in seconds the player must wait before being able to claim this kit again
* `Purchase Cost` is used to force players to pay to claim the kit. The currency type is defined in the config

### CopyPaste Support

`File Name` is the file name of the desired CopyPaste file. If provided, when the player claims this kit it will attempt to spawn the specified copypaste file
When you are done click the "Save Kit" button in the bottom left of the screen to save the kit

## Autokits

Autokits are kits that are given to a player when they respawn after dying.

What kits are included are to be specified in the config file under "Autokits ordered by priority" by using the kits name

The plugin will loop through each kit specified in the config until it can give one to the player.

These auto-kits adhere to the same redeem rules as claiming them manually so they will be subject to things like `Maximum Uses` and `Cooldown` time

Here is a example of the formatting used in the config

```json
"Autokits ordered by priority": [
  "example1",
  "example2",
  "example3"
],
```

## NPC Kits

The plugin support claiming kits through NPC vendors from the HumanNPC plugin.

Each NPC can be configured with different kits, and when the player interacts with the NPC the kit menu is presented to them with the kits specified in the config

The example in the config shows how to set this up.

You can specify which kits are available through the NPC, and a chat based response when interacting with them

The first parameter is the NPCs user ID

```json
"Kit menu items when opened via HumanNPC (NPC user ID | Items)": {
  "0": {
    "The list of kits that can be claimed from this NPC": [
      "ExampleKitName",
      "OtherKitName"
    ],
    "The NPC's response to opening their kit menu": "Welcome to this server! Here are some free kits you can claim"
  },
  "1111": {
    "The list of kits that can be claimed from this NPC": [
      "ExampleKitName",
      "OtherKitName"
    ],
    "The NPC's response to opening their kit menu": "Welcome to this server! Here are some free kits you can claim"
  }
},
```

## Kit Logging

There is a option in the config titled "Log kits given". When a player redeems a kit it is logged to /oxide/log/Kits/Kits_Received

## Configuration

> The settings and options can be configured in the `Kits` file under the `config` directory. The use of an editor and validator is recommended to avoid formatting issues and syntax errors.

```json
{
  "Kit chat command": "kit",
  "Currency used for purchase costs (Scrap, Economics, ServerRewards)": "Scrap",
  "Log kits given": false,
  "Wipe player data when the server is wiped": false,
  "Use the Kits UI menu": true,
  "Allow players to toggle auto-kits on spawn": false,
  "Show kits with permissions assigned to players without the permission": false,
  "Players with the admin permission ignore usage restrictions": false,
  "Autokits ordered by priority": [
    "ExampleKitName",
    "OtherKitName"
  ],
  "Post wipe cooldowns (kit name | seconds)": {
    "ExampleKitName": 3600,
    "OtherKitName": 600
  },
  "Parameters used when pasting a building via CopyPaste": [
    "deployables",
    "true",
    "inventories",
    "true"
  ],
  "UI Options": {
    "Panel Color": {
      "Hex": "#232323",
      "Alpha": 1.0
    },
    "Disabled Color": {
      "Hex": "#3e3e42",
      "Alpha": 1.0
    },
    "Color 1": {
      "Hex": "#007acc",
      "Alpha": 1.0
    },
    "Color 2": {
      "Hex": "#6a8b38",
      "Alpha": 1.0
    },
    "Color 3": {
      "Hex": "#d85540",
      "Alpha": 1.0
    },
    "Color 4": {
      "Hex": "#d08822",
      "Alpha": 1.0
    },
    "Default kit image URL": "<https://chaoscode.io/oxide/Images/kiticon.png>",
    "View kit icon URL": "<https://chaoscode.io/oxide/Images/magnifyingglass.png>"
  },
  "Kit menu items when opened via HumanNPC (NPC user ID | Items)": {
    "0": {
      "The list of kits that can be claimed from this NPC": [
        "ExampleKitName",
        "OtherKitName"
      ],
      "The NPC's response to opening their kit menu": "Welcome to this server! Here are some free kits you can claim"
    },
    "1111": {
      "The list of kits that can be claimed from this NPC": [
        "ExampleKitName",
        "OtherKitName"
      ],
      "The NPC's response to opening their kit menu": "Welcome to this server! Here are some free kits you can claim"
    }
  },
  "Version": {
    "Major": 4,
    "Minor": 0,
    "Patch": 15
  }
}
```

## For Developers

### Hooks

```cs
// Return any non-null value to abort redeeming of kit.
// Player notification as to why it was aborted should be handled
// from the plugin that is cancelling it
object CanRedeemKit( BasePlayer player )
// Called when a player claims a kit
void OnKitRedeemed(BasePlayer player, string kitName)
```

### API

```cs
// Give a kit to the target player.
// Returns a (string) message if unsuccessful, or (bool) true if successful
object GiveKit(BasePlayer player, string name)
// Returns true if the specified kit exists, otherwise returns false
bool IsKit(string name)
// Add all available kit names to the provided list
void GetKitNames(List<string> list)
// Returns the kit image URL if specified in the kit data
// else returns a empty string
string GetKitImage(string name)
// Returns the kit description if specified in the kit data
// else returns a empty string
string GetKitDescription(string name)
// Returns the kit maximum uses per player if specified in the kit data
// else returns 0
int GetKitMaxUses(string name)
// Returns the kit cooldown time (seconds) if specified in the kit data
// else returns 0
int GetKitCooldown(string name)
// Returns the number of time the specified player has used the specified kit
int GetPlayerKitUses(ulong playerId, string name)
// Allows you to set the specified players number of uses of the specified kit
void SetPlayerKitUses(ulong playerId, string name, int amount)
// Returns the remaining cooldown time (seconds) of the
// specified kit for the specified player
double GetPlayerKitCooldown(ulong playerId, string name)
// Allows you to set the remaining cooldown time of the specified kit
// for the specified player
void SetPlayerCooldown(ulong playerId, string name, double seconds)
// Returns a JObject of the kits data.
// Look at the implementation in the plugin to see the data structure
JObject GetKitObject(string name)
// Returns an enumerator that creates the items in the specified kit
IEnumerable<Item> CreateKitItems(string name)
```

## Credits

* **Reneb**, the original author of this plugin
