# Stackable Item Split Installation Manual

This guide explains how to install, configure, and verify the Shift-based stack splitting modification for the ARPG inventory UI.

## What This Modification Adds

The stack split modification lets a player split an existing stack by selecting a stacked item, holding **Shift**, and dropping it into another inventory cell, compatible item slot, or compatible existing stack.

When the player starts a split move:

1. A visual copy of the selected stack remains in the original inventory cell.
2. One item is placed at the target location.
3. A split menu opens with:
   - `-` decrease button
   - `+` increase button
   - `Confirm` button
   - TextMeshPro quantity text showing the amount being moved
4. The `+` and `-` buttons become unavailable at their safe min/max limits to prevent item duplication.

## Files Added or Modified

Install or verify the following files are present in your project:

### New Files

- `Assets/PLAYER TWO/ARPG Project/Core/GUI/GUIStackSplitMenu.cs`
- `Assets/PLAYER TWO/ARPG Project/Core/GUI/GUIStackSplitMenu.cs.meta`

### Modified Files

- `Assets/PLAYER TWO/ARPG Project/Core/GUI/GUI.cs`
- `Assets/PLAYER TWO/ARPG Project/Core/GUI/GUIInventory.cs`
- `Assets/PLAYER TWO/ARPG Project/Core/GUI/GUIItem.cs`
- `Assets/PLAYER TWO/ARPG Project/Core/GUI/GUIItemSlot.cs`
- `Assets/PLAYER TWO/ARPG Project/Core/Items/ItemAttributes.cs`
- `Assets/PLAYER TWO/ARPG Project/Core/Items/ItemInstance.cs`

## Prerequisites

Before installing, confirm the project has:

1. **Unity Input System** enabled, because Shift detection uses `Keyboard.current`.
2. **TextMeshPro Essentials** installed, because the split menu quantity field uses `TextMeshProUGUI` / `TMP_Text`.
3. Existing inventory UI prefabs that already use the ARPG project classes:
   - `GUI`
   - `GUIInventory`
   - `GUIItem`
   - `GUIItemSlot`
4. Stackable item definitions with:
   - `canStack` enabled
   - `stackCapacity` greater than `1`

## Installation Steps

### Step 1: Back Up Your Project

Before applying the modification, create a project backup or commit your current branch.

Recommended Git command:

```bash
git status
git add .
git commit -m "Backup before stack split installation"
```

Skip this step only if your project is already clean and backed up.

### Step 2: Add the Stack Split Menu Script

Copy the new stack split menu script into the GUI folder:

```text
Assets/PLAYER TWO/ARPG Project/Core/GUI/GUIStackSplitMenu.cs
```

This script creates the split menu at runtime, so no extra prefab is required.

The generated menu contains:

- a dark background panel
- a TextMeshPro quantity label
- a `-` button
- a `+` button
- a `Confirm` button

### Step 3: Update the Main GUI Controller

Update `GUI.cs` with the stack split workflow.

The main controller must include logic for:

1. Detecting when either Shift key is pressed.
2. Confirming the selected item is stackable and has a stack greater than `1`.
3. Creating a source-cell visual preview while Shift is held.
4. Placing one item into the destination before opening the split menu.
5. Moving additional quantities through the split menu only inside safe min/max limits.
6. Denying any split attempt that overlaps the original source cell.

Important methods added by this modification include:

- `IsStackSplitModifierPressed()`
- `CanSplitSelectedItem(GUIItem carried)`
- `TrySplitSelectedItemToInventory(GUIInventory inventory, GUIItem carried)`
- `TrySplitSelectedItemToSlot(GUIItemSlot slot, GUIItem carried)`
- `TrySplitSelectedItemToExistingStack(GUIItem destination, GUIItem carried)`
- `CompleteInitialSplitMove(...)`
- `HandleSplitPreview()`
- `DestroySplitPreview()`

### Step 4: Update Inventory Drop Handling

Update `GUIInventory.cs` so inventory cells attempt a split before normal placement.

The split attempt must run in both:

- `OnPointerDown(PointerEventData eventData)`
- `OnDrop(PointerEventData eventData)`

This allows stack splitting by both click-placement and drag-drop flows.

Also verify `GUIInventory` includes:

- `CreateVisualCopy(GUIItem source, InventoryCell cell)`
- `SetItemPosition(GUIItem item, int rowId, int columnId)`

These helpers are used to show the original stack icon while the player is holding Shift.

### Step 5: Update GUI Item Drop Handling

Update `GUIItem.cs` so carried items attempt stack splitting before normal drop behavior.

The split attempt must be wired into:

- `TryDropOnItemSlot(GUIItemSlot slot)`
- `TryDropOnInventory(GUIInventory inventory)`
- `OnDrop(PointerEventData eventData)`

This supports splitting into:

- equipment/consumable/blacksmith-style item slots when compatible
- inventory cells
- compatible existing stacks

Also verify `GUIItem` exposes its last inventory location through:

- `lastInventory`
- `lastInventoryPosition`
- `hasLastInventory`

These properties let the split workflow return the original selected stack safely and draw the visual source copy in the correct place.

### Step 6: Update Item Slot Drop Handling

Update `GUIItemSlot.cs` so item slots attempt stack splitting before normal equip/stack behavior.

The split attempt must run inside:

- `OnDrop(PointerEventData eventData)`

Also update slot hover highlighting so a compatible split target is highlighted as valid when Shift is held.

### Step 7: Add Item Copy Helpers

Update `ItemInstance.cs` with:

```csharp
CopyWithStack(int stack)
```

This creates a separate item instance with the requested stack amount while preserving:

- item data
- durability
- rarity id
- prefix affixes
- suffix affixes
- item attributes

Update `ItemAttributes.cs` with a copy constructor:

```csharp
ItemAttributes(ItemAttributes other)
```

This prevents split copies from losing rolled attributes or sharing mutable attribute state unexpectedly.

### Step 8: Import TextMeshPro Essentials

If TextMeshPro Essentials are not already installed:

1. Open Unity.
2. Go to **Window > TextMeshPro > Import TMP Essential Resources**.
3. Import the package.

The project may compile without scene changes only after TextMeshPro is available, because `GUIStackSplitMenu.cs` uses the `TMPro` namespace.

### Step 9: Verify Input System Settings

In Unity:

1. Open **Edit > Project Settings > Player**.
2. Check **Active Input Handling**.
3. Use either:
   - `Input System Package (New)`, or
   - `Both`

The split modifier uses the new Input System keyboard API.

### Step 10: Verify Stackable Item Data

For each item that should support splitting:

1. Open the item asset in the Unity Inspector.
2. Enable stack support by setting `canStack` to true.
3. Set `stackCapacity` to a value greater than `1`.
4. Make sure the item can appear in an inventory with a stack count greater than `1`.

Non-stackable items and stacks of exactly `1` will continue to use the normal move/drop behavior.

## In-Editor Verification Checklist

After installing the modification, enter Play Mode and verify the following cases.

### Basic Inventory Split

1. Add a stackable item with stack count `2` or higher to the player inventory.
2. Select or drag the item.
3. Hold **Shift**.
4. Drop it into an empty inventory cell.
5. Confirm that:
   - a visual icon remains in the source cell while Shift is held
   - one item appears in the destination cell
   - the split menu opens
   - the displayed quantity starts at `1`
   - the `-` button is disabled at quantity `1`
   - the `+` button is disabled once the maximum safe quantity is reached
   - confirming leaves the source and destination stacks with the expected counts

### Split Into Existing Compatible Stack

1. Create two stacks of the same stackable item.
2. Select one stack.
3. Hold **Shift**.
4. Drop it onto the other compatible stack.
5. Confirm that the destination stack increases and the source stack decreases without exceeding `stackCapacity`.

### Split Into Incompatible Target

1. Select a stackable item.
2. Hold **Shift**.
3. Try to drop it into an occupied incompatible cell or incompatible item slot.
4. Confirm that the move is denied and no items are duplicated or lost.

### Split Onto Source Cell

1. Select a stackable item.
2. Hold **Shift**.
3. Try to drop it back onto the original occupied source area.
4. Confirm that the move is denied.

### Stack of One

1. Select an item with stack count `1`.
2. Hold **Shift** and move it.
3. Confirm that normal movement behavior is used and the split menu does not open.

## Troubleshooting

### The Project Does Not Compile Because `TMPro` Cannot Be Found

Install TextMeshPro Essentials:

```text
Window > TextMeshPro > Import TMP Essential Resources
```

### Holding Shift Does Nothing

Check that:

1. The selected item is stackable.
2. The selected item stack count is greater than `1`.
3. The project uses the new Unity Input System or `Both` input handling.
4. The item was selected from an inventory, not from a merchant-only listing.

### The Split Menu Does Not Appear

Check that:

1. The destination placement is valid.
2. The target cell does not overlap the source cell.
3. The destination stack has free capacity if dropping onto an existing stack.
4. The `GUI` singleton exists in the active UI scene.

### Buttons Do Not Change State

The split menu updates button availability after every `+` or `-` click. If a button appears stuck:

1. Confirm the source and destination `ItemInstance` references are valid.
2. Confirm the destination item has a valid `stackCapacity`.
3. Confirm the destination stack is not already full.

## Custom UI Option

The split system can use either the generated default menu or a custom prefab.

### Use the Generated Default Menu

Leave the following fields empty on the scene `GUI` component:

- `Stack Split Menu Prefab`
- `Stack Split Menu Container`

When no prefab is assigned, `GUIStackSplitMenu.Create(...)` builds the default runtime UI automatically.

### Use a Custom Split Menu Prefab

To provide a custom UI layout:

1. Create a UI prefab in your project.
2. Add the `GUIStackSplitMenu` component to the prefab root.
3. Add and style the child controls however you want.
4. Assign these references on the prefab's `GUIStackSplitMenu` component:
   - `Confirm Button` for the confirmation control
   - `Amount Text` for the TextMeshPro quantity display
   - either button controls or the custom slider control:
     - assign `Decrease Button` and `Increase Button` for the classic `-` / `+` controls, or
     - assign `Amount Slider` for a single-slider quantity selector
5. To make a slider-only custom UI, leave `Decrease Button` and `Increase Button` unassigned and assign only `Amount Slider`.
6. Select the scene object with the main `GUI` component.
7. Assign your prefab to `Stack Split Menu Prefab`.
8. Optionally assign `Stack Split Menu Container` if the menu should appear under a specific canvas/container instead of directly under the `GUI` transform.

The custom prefab still uses the same split logic, button state updates, slider min/max values, and quantity limits, so the duplication safeguards remain active.

### Custom Slider Behavior

The `Amount Slider` option is intended for custom UI prefabs only. The generated default menu continues to use the two-button layout.

When `Amount Slider` is assigned:

- the slider is forced to whole numbers
- the minimum value is set to `1`
- the maximum value is set to the largest safe split amount
- dragging the slider immediately moves quantity between the source and destination stacks
- the slider becomes non-interactable if there is no adjustable range

You can still assign both buttons and a slider if you want both interaction styles, but for a single-slider UI leave the `Decrease Button` and `Increase Button` fields empty.

### Generated Menu Styling

If you do not want to create a prefab, you can still customize the generated default menu by editing:

```text
Assets/PLAYER TWO/ARPG Project/Core/GUI/GUIStackSplitMenu.cs
```

Recommended customization points:

- panel size and color in `CreateDefault(...)`
- button positions and labels in `BuildControls(...)`
- button colors in `CreateButton(...)`
- quantity text size/color in `CreateText(...)`
