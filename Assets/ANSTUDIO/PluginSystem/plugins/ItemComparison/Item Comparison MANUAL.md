# Item Comparison Plugin Manual

## Overview
**Item Comparison** 
When the inventory is Open, holding **Shift** shows stat differences between the highlighted item and what your character currently has equipped.

## Installation
1. Import the `ItemComparison` package.
2. Apply the manifest changes to the GUIItemInspector.cs
3. Ensure the Plugin System is installed and enabled (see the project Plugin System manual).
4. In the plugin folder there is an example prefab that also contains an Item Comparison text object that needs to be copied to the Item Inspector as a child and copy and paste as new the `ItemComparisonExtension` script found in the `Item Inspector Example` prefab. or follow the steps bellow to do this completely manually.
	a. Add the `ItemComparisonExtension` script to the Item Inspector found in __CANVAS__
	b. Assign the compareAction/ShiftToCompare Input System action to the **Comparison Action** field on the `ItemComparisonExtension` component (attached to `GUI Item Inspector`). The action will be enabled automatically when the inspector is active.
	c. Assign the Item Comparison text object 
	d. Choose your colors or leave as default
4. Launch the game. The plugin registers itself automatically through the Plugin System loader.

## Usage
1. Open the inventory and hover/select an item to show the inspector.
2. Hold **Left Shift** or **Right Shift** (Input System keyboard) or press your configured **Comparison Action** to display the comparison text for equippable items (weapons, armor, shields, bows).
3. Release **Shift** to hide the comparison overlay.

## Notes
- The plugin listens to `EventBus` inspector events, so no scene changes are required.
- Comparison text is cloned from the inspector attribute text for consistent styling. If you want to drive an existing `Text` component instead, assign it to the `Comparison Text Override` field on the `ItemComparisonExtension` (attached to `GUI Item Inspector`).
- Customize the comparison highlight colors via the `Comparison Colors` fields on the same component; the defaults match the project `GameColors` blue/red pairing.
