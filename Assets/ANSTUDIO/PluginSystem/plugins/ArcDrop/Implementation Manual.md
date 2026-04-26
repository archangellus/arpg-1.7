ArcDrop — Implementation Manual
================================

Overview
--------
ArcDrop is a plug-in that handles GUI item drops through the EventBus instead of core edits. When a player drops an item in the GUI, ArcDrop animates the collectible along an adjustable arc toward the hit point the player clicked/tapped on the ground.

What ArcDrop listens for
------------------------
- **EventBus.ArcDropRequested** — published by the GUI before any core drop logic runs. The payload contains:
  - `GUI gui` — the active GUI instance
  - `GUIItem guiItem` — the selected GUI item
  - `Entity entity` — the player entity
  - `Action onDropCompleted` — invoke to remove the GUI item and mark the drop time
  - `Action onDropFailed` — invoke to safely deselect on mobile when no ground hit is found
  - `Action<bool> markHandled` — call with `true` once the plugin takes ownership so core logic skips its fallback drop

Runtime behavior
----------------
1. Subscribes to `ArcDropRequested` in `ArcDropRuntime.Awake()`.
2. Raycasts using `Entity.inputs.MouseRaycast` against the configured **dropGroundLayer**. If not set, it uses `Level.instance.dropGroundLayer` (or `Physics.DefaultRaycastLayers` as a last resort).
3. Instantiates the collectible (preferring the runtime **droppedItemPrefab** override, otherwise `Game.instance.collectibleItemPrefab`) at the player’s position and applies the item from the GUI.
4. Calls `markHandled(true)` followed by `onDropCompleted()` so GUI state is updated.
5. Animates the collectible to the impact point along an arc using **dropSpeed**, **arcHeight**, and **arcCurve**, with optional per-axis rotation (**rotateOnX**, **rotateOnY**, **rotateOnZ**, **rotationSpeed**). Drop distance is clamped by **dropRange**.

Configuration
-------------
- Add `ArcDropPlugin` to your plugin states or leave it enabled by default (shipped as enabled).
- Select the manifest edit and use the Apply Selected Edit button.
- Drag the [ArcDropPlugin] prefab in the scene(found in the plugin folder)
- If your ground layer is not found automatically, select it from the list
- Play

- Auto-defaults applied on startup:
  - **dropGroundLayer** — set to the active Terrain’s layer when unset.
  - **droppedItemPrefab** — auto-assigned to the first found object named **“Collectible Item”**.
  - **arcCurve** — rebuilt to a five-key arc if missing or trivial.
  - **dropSpeed** defaults to **2**; **rotationSpeed** defaults to **250**.
- You can still tweak `ArcDropRuntime` fields on the `[ArcDropPlugin]` GameObject after play starts, or create a prefab override and replace it in the scene if desired:
  - **dropGroundLayer** — set a mask if you want a custom ground filter.
  - **droppedItemPrefab** — assign a custom collectible prefab (falls back to `Game.collectibleItemPrefab`).
  - **dropSpeed**, **arcHeight**, **arcCurve** — control arc trajectory and timing.
  - **rotateOnX/Y/Z**, **rotationSpeed** — adjust rotation during flight.
  - **dropRange** — limit how far from the player items can land.

Mobile note
-----------
If the raycast fails on mobile, ArcDrop invokes the provided `onDropFailed` callback so the GUI can safely deselect and return the item.

Extension tips
--------------
- Subscribe earlier to `ArcDropRequested` with `LoadOrder` < 200 if you need to override ArcDrop (e.g., to block drops in restricted areas). Remember to call `markHandled(true)` when you fully handle the request.
- To add sounds or VFX, extend `ArcDropRuntime` and play effects right after `Instantiate` or inside `AnimateDropItem`.
