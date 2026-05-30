# Pet Inventory Installation Manual

This guide explains how to add the pet inventory to a scene and connect it to the existing ARPG Project UI/save flow.

## 1. Add the scene settings component

1. In the scene hierarchy, create an empty GameObject named **Pet Inventory Settings**.
2. Add **PLAYER TWO/ARPG Project/Inventory/Pet Inventory Settings** to that GameObject.
3. Configure **Rows** and **Columns** in the Inspector. These values control only the pet inventory grid and do not change the player inventory size.

## 2. Create the pet inventory UI window

1. Duplicate the existing player inventory window or create a new UI window using the same structure:
   - A `GUIWindow` component on the window root.
   - A grid container for inventory slots.
   - An items container for GUI items.
   - The same slot prefab used by the player inventory.
2. Replace or add the inventory component on that window root with **GUI Pet Inventory**.
3. Assign these inherited `GUIInventory` references in the Inspector:
   - **Inventory Slot**
   - **Grid Container**
   - **Items Container**
   - Optional **Auto Sort Button**
4. Leave **Money Text** empty, or assign it if you are reusing the player inventory layout. `GUI Pet Inventory` hides the money text at runtime because pet inventory does not store money.
5. Keep the pet inventory window inactive by default if you want it hidden at scene start.

## 3. Register the window with the GUI windows manager

1. Select the GameObject that has **GUI Windows Manager**.
2. Assign the pet inventory window's `GUIWindow` component to **Pet Inventory Window**.

## 4. Configure UI button toggling

To toggle from a UI Button:

1. Select the button.
2. Add an `OnClick` listener.
3. Drag the **GUI Windows Manager** object into the listener target field.
4. Select `GUIWindowsManager.TogglePetInventory()`.

## 5. Configure input toggling

The example input asset already contains a **Toggle Pet Inventory** action bound to **P**.

If your scene uses a custom GUI input asset:

1. Open the GUI input actions asset.
2. Add a button action named exactly **Toggle Pet Inventory**.
3. Bind it to the desired key/button.
4. Ensure the scene `GUI` component references this input action asset.

At runtime, `GUI` invokes its **On Toggle Pet Inventory** event. If that event has no persistent listeners, it falls back to `GUIWindowsManager.TogglePetInventory()` automatically.

## 6. Item movement and dropping

- Left-click/drag behavior matches the existing inventory interaction.
- Right-click an item inside the pet inventory to move it into the player inventory when space is available.
- Select an item from the pet inventory and use the normal drop action to drop it on the ground through the existing item drop system.

## 7. Saving and loading

Pet inventory items are saved with the current character through the existing save system. Pet inventory money is always serialized as `0`, and the pet inventory UI hides money by default.
