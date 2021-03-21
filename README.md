# Rusty Cuffs - Handcuffs
<img align="right" src="https://i.imgur.com/bWEPIhZ.png" width="200"/>

> A Rust plugin that adds Hand Cuffs to your [uMod](https://umod.org/games/rust) server.

###### What is this?
**RustyCuffs** adds handcuff mechanics to rust by allowing you to restrain and escort other players. For any questions or help, you can join our discord [here](https://discord.gg/5BqtyY4pvU)
## Video
Check out the demo video [here](https://www.youtube.com/watch?v=G7dhc0IKczk)

## Features
* Restrain / unrestrain players
* Restrain AI (optional)
* Escort / unescort
* Execute (kill)
* Create handcuffs and key items
* Anti wall clipping (construction only)
* Chat commands
* Permissions
* Changeable chat prefix and icon
* Configurable

## How to use
Equip the cuffs item with `/cuffs <player>` (requires `rustycuffs.admin` to be set) or by finding it. Look at a player while holding the cuffs item and hold the `RELOAD` key, this will restrain the player. If using the `rustycuffs.unlimited` permission, you can use the cuffs again to interact with the restrained player. If not, you can use the cuffs key that is created to interact with the restrained player using the reload key. The cuffs key must belong to the restrained player for it to work (they key item will be renamed to their name).

## Permissions
* `rustycuffs.admin` - Allows the use of chat commands
* `rustycuffs.use` - Allows players to use rusty cuffs
* `rustycuffs.unlimited` - Allows infinte usage of a single cuffs item
* `rustycuffs.escort` - Allows player to escort a target
* `rustycuffs.viewinventory` - Allows player to view the inventory of a target
* `rustycuffs.execute` - Allows player to kill the target from the select menu
* `rustycuffs.createkey` - Allows player to create a key from the select menu
* `rustycuffs.unrestrain` - Allows player to unrestrain target

## Chat Commands
Chat commands require the `rustycuffs.admin` permission to be set
* `/restrain <player>` - Restrain target
* `/unrestrain <player>` - Unrestrain target
* `/cuffsmenu <player>` - Open the select menu on target (must be restrained)
* `/cuffs <player>` - Gives target the cuffs item
* `/cuffskey <player>` - Gives player a key for the targets cuffs
* `/cuffsbot` - Spawn a bot (for testing)

## Frequently Asked Questions
**Question:** Why can't I unrestrain people in moving vehicles or elevators?  
**Answer:** Rust connects players to the moving volume that you're standing in. Due to this behavior a pretty bad bug occurs when trying to unrestrain while in this volume that causes the client to crash. I disable users input by forcing them to spectate themself and spectating while in this volume causes an array of issues. It's something I'm aware of and will approach this behavior differently in a future patch.

**Question:** Why do people hover when unrestrained at a height?  
**Answer:** When I disable a users input I force them to spectate themselves, this disables physics for the player. This too will be addressed in a future patch.

**Question:** Is this the same as the paid handcuff plugins?  
**Answer:** No, I took a different approach to this plugin, I did not want the restrained target to constantly be following the player, I found this unrealistic and mechanically too rewarding for the player. By forcing the restrained target in front of the player, their the line of sight is restricted and makes combat impossible without killing the restrained target thus making the decision to escort somebody penalizing.

**Question:** If I'm escorting somebody can't I just clip them through a wall and loot what's behind it?  
**Answer:** No. When a target enters or is pushed through construction (base etc), the target becomes automatically unescorted and moved away from the object.

## Configuration
```json
{
  "Chat Prefix": "[+16][#00ffff]Rusty Cuffs[/#][/+]: ",
  "Chat Icon": 76561199105408156,
  "Return Cuffs": true,
  "Restrain Time": 1.0,
  "Restrain Distance": 2.0,
  "Escort Distance": 0.9,
  "Restrain NPCs": true
}
```

## Developers
# API
Check to see if a player is restrained
```csharp
private bool API_IsRestrained(BasePlayer player)
```

Create handcuffs item
```csharp
private Item API_CreateCuffs(int amount)
```

Create key item for specified player
```csharp
private Item API_CreateCuffsKey(BasePlayer player)
```

Restrain  player
```csharp
private bool API_Restrain(BasePlayer target, BasePlayer player)
```

Unrestrain player
```csharp
private bool API_Unrestrain(BasePlayer target, BasePlayer player)
```

# Hooks
Can player start the restraining process on a target
```csharp
private object CanCuffsPlayerStartRestrain(BasePlayer target, BasePlayer player){
	Puts("CanCuffsPlayerStartRestrain works!");

	return null;
}
```

Can player restrain the target
```csharp
private object CanCuffsPlayerRestrain(BasePlayer target, BasePlayer player){
	Puts("CanCuffsPlayerRestrain works!");

  return null;
}
```

Can target be unrestrained
```csharp
private object CanCuffsPlayerUnrestrain(BasePlayer target){
	Puts("CanCuffsPlayerUnrestrain works!");

	return null;
}
```

Can player select the target (bring up menu)
```csharp
private object CanCuffsPlayerSelect(BasePlayer target, BasePlayer player){
	Puts("CanCuffsPlayerSelect works!");

	return null;
}
```

Can player start escorting the target
```csharp
private object CanCuffsPlayerEscort(BasePlayer target, BasePlayer player){
	Puts("CanCuffsPlayerEscort works!");

	return null;
}
```

Can player stop escorting the target
```csharp
private object CanCuffsPlayerEscortStop(BasePlayer target, BasePlayer player){
	Puts("CanCuffsPlayerEscortStop works!");

	return null;
}
```

Can player view the targets inventory
```csharp
private object CanCuffsPlayerViewInventory(BasePlayer target, BasePlayer player){
	Puts("CanCuffsPlayerViewInventory works!");

	return null;
}
```

Can player kill the target (stab damage type)
```csharp
private object CanCuffsPlayerExecute(BasePlayer target, BasePlayer player){
	Puts("CanCuffsPlayerExecute works!");

	return null;
}
```

Can player use keys on the target
```csharp
private object CanCuffsPlayerUseKey(BasePlayer target, BasePlayer player){
	Puts("CanCuffsPlayerUseKey works!");

	return null;
}
```

Can player use cuffs on the target
```csharp
private object CanCuffsPlayerUseCuffs(BasePlayer target, BasePlayer player){
	Puts("CanCuffsPlayerUseCuffs works!");

	return null;
}
```

Called when a player starts being restrained
```csharp
private void OnCuffsPlayerStartRestrain(BasePlayer target, BasePlayer player){
	Puts("OnCuffsPlayerStartRestrain works!");
}
```

Called when a player is restrained
```csharp
private void OnCuffsPlayerRestrain(BasePlayer target, BasePlayer player){
	Puts("OnCuffsPlayerRestrain works!");
}
```

Called when a player is unrestrained
```csharp
private void OnCuffsPlayerUnrestrain(BasePlayer target){
	Puts("OnCuffsPlayerUnrestrain works!");
}
```

Called when a player is selected
```csharp
private void OnCuffsPlayerSelect(BasePlayer target, BasePlayer player){
	Puts("OnCuffsPlayerSelect works!");
}
```

Called when a player starts to escort a target
```csharp
private void OnCuffsPlayerEscort(BasePlayer target, BasePlayer player){
	Puts("OnCuffsPlayerEscort works!");
}
```

Called when a player stops escorting a target
```csharp
private void OnCuffsPlayerEscortStop(BasePlayer target, BasePlayer player){
	Puts("OnCuffsPlayerEscortStop works!");
}
```

Called when a player views a targets inventory
```csharp
private void OnCuffsPlayerViewInventory(BasePlayer target, BasePlayer player){
	Puts("OnCuffsPlayerViewInventory works!");
}
```

Called when a player kills a target
```csharp
private void OnCuffsPlayerExecute(BasePlayer target, BasePlayer player){
	Puts("OnCuffsPlayerExecute works!");
}
```

Called when a player uses a key
```csharp
private void OnCuffsPlayerUseKey(BasePlayer target, BasePlayer player){
	Puts("OnCuffsPlayerUseKey works!");
}
```

Called when a player uses cuffs
```csharp
private void OnCuffsPlayerUseCuffs(BasePlayer target, BasePlayer player){
	Puts("OnCuffsPlayerUseCuffs works!");
}
```

## Todo
* Add ability to move players into vehicles
* Add lockpicks

## Known bugs
* Users float when unescorted in the air
