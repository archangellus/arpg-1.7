# ARPG Interaction System

A lightweight Press-E style interaction layer for ARPG Project.

## Setup

1. In the Unity Editor, select **Tools > ANSTUDDIO > Interactive System > Create Interaction Manager**.
2. Assign the player `Entity` if it was not found automatically, or choose the `Player Tag` from the existing-tags dropdown so the manager can find it.
3. Configure `Key To Interact`, `Interaction Distance`, detection mode, and universal prompt styling on the manager.
4. Add `ARPG Interactable` to objects that need custom prompt text, icons, press interactions, or hold interactions.

Existing ARPG `Interactive` objects also work without adding `ARPG Interactable`; the manager will show the default prompt and call `Interactive.Interact(player)`.

## Supported modes

- **Press**: press the interaction key once to trigger the event.
- **Hold**: hold the interaction key for the configured duration to trigger the event.
