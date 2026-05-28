# Infinity Wardrobe Plug-in Manual

## Overview
The **Infinity Wardrobe** plug-in extends the ARPG Project's `EntityItemManager` with a third
Item System management path. Besides Character Pieces and Item Renderers, equipped armor can now
activate or deactivate the objects that belong to Infinity PBR "Wardrobe" groups without
editing any ARPG Project file. The system is implemented on top of the plug-in framework and
relies on the shared `EventBus`, so it stays update-safe and can be automated by other tools.

## Installation
### Requirements
This plugin has the following dependencies:
1.	ARPG Project
2.	Plugin System 
3.	Infinity PBR (at least one of the character packs)
4.	Items configured in the Infinity PBR’s * Prefab and Object Manager *

### Automatic Installation (recommended)
This is the normal workflow for plugins distributed as InfinityWardrobe.zip exported from the Plugin System.

1. 	Open the Plugin System Window
	- In Unity:Menu: Tools → ANSTUDIO → Plugin System → Plugin System Window
	- The left side lists all existing plugins. The bottom toolbar contains import/export buttons.
	
2. 	Import the InfinityWardrobe.zip archive
	- In the bottom toolbar click Import Plug-in.
	- In the file dialog select the InfinityWardrobe.zip.
	- Confirm.
	
3.	Finalize plugin setup
	After import and (optional) patch application:
	- Check the plugin folder under: Assets/ANSTUDIO/PluginSystem/plugins/InfinityWardrobe
	- Open any README / manual / provided by the plugin.
	
### Manual Installation
1. Copy the entire `Assets/ANSTUDIO/PluginSystem/plugins/InfinityWardrobe` folder into your
   project alongside the other plug-ins.
2. Ensure the `PluginManager` script (already included with the plug-in system) is part of
   your project so the new plug-in is discovered at play mode.
   
Keep the provided `Resources/InfinityWardrobeLibrary.asset` file in your project. The 
runtime service auto-loads it from `Resources/InfinityWardrobeLibrary` when no custom
library is assigned.
   
Enter play mode. A hidden `[Plugin] Infinity Wardrobe Service` object is created and 
persists across scenes while automatically wiring `EntityItemManager` instances.

## Default Behaviour
The bundled library ships with an example rule for `Leather Armor Chest`. When that armor is
worn the first entry inside `Wardrobe0` is activated, while the rest of the group is turned
off. Removing the armor disables the object again. Use this as a template for your own rules.

## Configuring Wardrobe Rules
1. Select `InfinityWardrobeLibrary.asset` and press **Add** in the list to create new rules.
   Use the search bar located above the rules list whenever you need to filter entries by item
   name or description—the inspector stretches with the window so you can comfortably edit long
   lists.
   
2. Fill the fields (each one includes inspector tooltips for easy info when needed):
   - **Item Armors**: list every `ItemArmor` asset that should execute the same rule. A single
     entry is still valid, but now you can drag multiple armor definitions into the list so each
     one enables or disables the same wardrobe objects.
	 
   - **Description**: optional helper text shown only in the inspector.
   
   - **Group Type**: matches the `groupType` column inside the Prefab And Object Manager (the
     default `Wardrobe` type is usually enough).
	 
   - **Group Name**: name of the group row (for example `Wardrobe0`). Leave it empty when you
     prefer to target by index.
	 
   - **Group Index**: zero-based index used when no name is provided. The index is calculated
     *per group type*, so `0` represents the first group of the chosen type(default -1 to autodetect).
	 
   - **Clear Group Before Apply**: disables every object inside the group before any custom
     action runs. Enable it whenever you want to guarantee that only one outfit piece remains
     visible at a time (it is disabled by default so you can opt in per rule).
	 
   - **Clear Group On Unequip**: forces the entire group to turn off when the armor is removed.
     Enable it when you do not want a fallback outfit to remain (also disabled by default).
	 
   - **Revert On Unequip**: mirrors each action when the item is removed. If disabled the
     plug-in will leave the last state untouched(default = true).
	 
  - **Actions**: list of object toggles that target the indices shown inside the Prefab And Object Manager
	(the small red `X`/green `✓` controls highlighted in the provided screenshots). 
	Each action stores one or more *Object Indices*, whether they should be enabled
    on equip, the state to apply on unequip, and an optional list of extra indices to disable
    whenever the action fires. Use this last field to immediately hide multiple conflicting meshes
    the moment a new object turns on. Pairing several indices inside a single action is perfect
    for outfits that need to activate multiple wardrobe entries simultaneously.
	
3. Repeat the process for every armor piece. No scene or prefab editing is required.

### Player Entity Item Manager Configuration
To avoid conflicts with the existing default item systems you need to clear the following fields and entries in the Entity Item Manager:
* Item Renderers *
* Character pieces *
* Initial Visible Pieces *

### Tips for New Items
- When creating a new armor item, duplicate an existing rule and add the new `ItemArmor` to the
  **Item Armors** list (or create a dedicated rule). The plug-in reacts to any armor listed in a
  rule, so variants that share visuals can reuse the same configuration.
- Enable `Clear Group Before Apply` to automatically disable conflicting wardrobe pieces the
  moment a new one becomes active; leave it off when you want multiple objects from the same
  group to remain visible.
  
- Use the `Description` field to record which meshes are being toggled. This helps the next
  person who edits the library.
  
- Multiple armor items can now keep their wardrobe objects enabled at the same time. The binder
  tracks which entries are currently needed and ignores revert operations for those objects, so a
  belt and a cloak (for example) can share the same group without disabling each other unless you
  explicitly clear the group or add extra disable indices.

## EventBus Hooks
The plug-in exposes two new events for automation:

```csharp
EventBus.RaiseInfinityWardrobeRefresh(Entity targetEntity); // null refreshes every binder
EventBus.RaiseInfinityWardrobeApplied(Entity affectedEntity);
```

- `InfinityWardrobeRefreshRequested`: published whenever a refresh is requested (either via the
  helper method above or by other systems).
- `InfinityWardrobeApplied`: published after the binder processes the currently equipped items.

You can subscribe to `EventBus.InfinityWardrobeRefreshEvent` and
`EventBus.InfinityWardrobeAppliedEvent` if you prefer the strongly-typed delegates.

## Usage Flow
1. Equip or remove an armor item using the inventory.
2. The `EntityItemManager` fires `onChanged`, which the binder listens to.
3. Infinity Wardrobe compares the new inventory state with the configured rules and toggles the
   requested wardrobe objects. When the plug-in needs to spawn a wardrobe prefab it automatically
   retargets every `SkinnedMeshRenderer` to the character's live skeleton so the mesh follows the
   correct bones and the Infinity PBR "checkmark" UI mirrors the current state.
4. `EventBus.RaiseInfinityWardrobeApplied` is called so other systems (e.g. gameplay buffs or
   analytics) can react to the visual change.

## Troubleshooting
- **Nothing happens at runtime**: ensure the character prefab actually contains an Infinity PBR
  `PrefabAndObjectManager` component. The service only attaches binders when this component is
  present, so characters without it remain untouched (and the console stays clean).
- **Wrong object toggled**: double-check every entry in the action's object-index list against the
  Prefab And Object Manager inspector. Indices are zero-based and follow the same order used in the
  UI.
- **Need to refresh manually**: call `EventBus.RaiseInfinityWardrobeRefresh(targetEntity)`
  whenever you change wardrobe data via code.
- **Multiple outfits stacking**: enable `Clear Group Before Apply` and, if necessary,
  `Clear Group On Unequip` for the involved rules.

With these tools you can extend the wardrobe pipeline without touching Player Two's source
files, and future updates of the ARPG Project remain safe.
