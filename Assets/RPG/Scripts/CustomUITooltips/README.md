# Custom UI Tooltips for Unity 6000.3+

A no-programming runtime tooltip system for Unity UI, built around a UI Toolkit overlay and compatible with both uGUI and UI Toolkit interfaces.

## What it gives you

- Runtime tooltips with title, body, icon, delay, placement, and styling profile.
- Designer workflow: use Unity menus and Inspector fields; no gameplay code is required.
- uGUI support: add `TooltipTrigger` to any `RectTransform` UI object.
- UI Toolkit support: add `TooltipUITKBinder` to a `UIDocument` and map selectors like `#PlayButton` or `.has-tooltip`.
- Optional UI Toolkit custom element: `TooltipAnchor` can be placed in UXML/UI Builder and configured with attributes.
- API support: call `TooltipService.Show(...)`, `TooltipService.Hide(...)`, or `TooltipService.Bind(VisualElement, TooltipContent)`.

## Install

Copy the `Assets/CustomUITooltips` folder into your Unity project.

## One-time scene setup

1. In Unity, select **Tools > Custom Tooltips > Create Runtime Tooltip System**.
2. This creates a `Runtime Tooltip System` GameObject with:
   - `UIDocument`
   - `TooltipManager`
   - a generated `PanelSettings` asset
   - a generated `Tooltip Profile` asset
3. Tweak colors, widths, padding, and delay on `Assets/CustomUITooltips/Generated/Default Tooltip Profile.asset`.

## Add tooltips without programming

### uGUI / Canvas UI

1. Select one or more UI GameObjects that have `RectTransform`.
2. Use **Tools > Custom Tooltips > Add Tooltip Trigger To Selected uGUI Elements**.
3. Fill `Tooltip > Title`, `Body`, `Icon`, `Placement`, and delay options in the Inspector.

### UI Toolkit / UIDocument

1. In UI Builder, give elements a name such as `PlayButton`, or a class such as `has-tooltip`.
2. Select the GameObject with the `UIDocument`.
3. Use **Tools > Custom Tooltips > Add UI Toolkit Binder To Selected UIDocument**.
4. Add rows to `Bindings`:
   - `#PlayButton` targets a named element.
   - `.has-tooltip` targets every element with that USS class.
   - `PlayButton` also works as a raw name.
5. Fill tooltip content in each row.

### Optional UXML custom element

Use `TooltipAnchor` when you want a UI Builder/UXML element that carries tooltip text itself.

```xml
<CustomUITooltips.TooltipAnchor tooltip-title="Inventory" tooltip-body="Open your items and equipment." tooltip-placement="Below" />
```

## API examples

```csharp
using CustomUITooltips;
using UnityEngine;

TooltipService.Show("Gold", "Used to buy upgrades.", Input.mousePosition, this);
TooltipService.Hide(this);
```

```csharp
using CustomUITooltips;
using UnityEngine.UIElements;

IDisposable binding = TooltipService.Bind(myVisualElement, TooltipContent.Text("Settings", "Open game options."));
// binding.Dispose(); when you no longer need it.
```

## Notes

- Keep the tooltip overlay `UIDocument` on a Panel Settings asset with a high sorting order if you have multiple runtime panels.
- This package does not use Unity's built-in UI Toolkit TooltipEvent because that event is Editor-only in Unity's documentation.
- For Input System projects, you can replace the generated `StandaloneInputModule` with `InputSystemUIInputModule` on the EventSystem.
