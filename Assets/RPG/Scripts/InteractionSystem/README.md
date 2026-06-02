# ARPG Interaction System

A lightweight Press-E style interaction layer for ARPG Project.

## Setup

1. In the Unity Editor, select **Tools > ANSTUDDIO > Interactive System > Create Interaction Manager**.
2. Assign the player `Entity` if it was not found automatically, or choose the `Player Tag` from the existing-tags dropdown so the manager can find it.
3. Configure `Key To Interact`, `Interaction Distance`, detection mode, player-facing behavior, and universal prompt styling on the manager.
4. Add `ARPG Interactable` to objects that need custom prompt text, icons, press interactions, or hold interactions.

By default, only objects with `ARPGInteractable` are selected. Existing ARPG `Interactive` objects can still be called by assigning them to `Linked Interactive` on an `ARPGInteractable`, or by enabling `Include Legacy Interactive` on the manager.

## Supported modes

- **Press**: press the interaction key once to trigger the event.
- **Hold**: hold the interaction key for the configured duration to trigger the event.

## Player facing

Enable **Rotate Player To Face Interactable** on the `ARPGInteractionManager` to make the player face the selected object while the interaction prompt is active. For a top-down Diablo-like camera, keep **Use Flat Player Facing** enabled so the character rotates only around the Y axis.

- Assign **Player Rotation Transform** when only a child model should turn instead of the player root.
- Enable **Snap Player Rotation To Interactable** for instant facing. Leave it off to rotate smoothly using **Player Rotation Speed**.
