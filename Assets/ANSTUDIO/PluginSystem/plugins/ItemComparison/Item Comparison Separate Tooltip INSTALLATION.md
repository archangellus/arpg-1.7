# Item Comparison (Separate Tooltip Window) — Installation Guide

This guide explains how to install and configure the **ItemComparison** plugin update that renders comparison data in a **separate tooltip window** instead of appending to the main item inspector text block.

---

## What changed

The `ItemComparisonExtension` now supports:

- A dedicated comparison tooltip container (`comparisonTooltipRoot`), and
- Auto-creation of that container when none is assigned.

When the comparison key is held (default **Left Shift / Right Shift**), the plugin opens a second tooltip panel next to the normal item inspector. The panel shows the **currently equipped item details first**, then a **Differences** section underneath.
If there are no stat deltas, the comparison tooltip is not shown.

---

## Files included in this modification

- `Assets/ANSTUDIO/PluginSystem/plugins/ItemComparison/ItemComparisonExtension.cs`
- `Assets/ANSTUDIO/PluginSystem/plugins/ItemComparison/Item Comparison Separate Tooltip INSTALLATION.md`

---

## Installation options

## Option A (Recommended): Use auto-created tooltip window

No prefab editing required.

1. Open the project in Unity.
2. Ensure the **ItemComparison** plugin is enabled in your Plugin System workflow.
3. Enter Play Mode.
4. Hover an equippable item so the item inspector appears.
5. Hold **Shift** (or your configured input action).
6. Confirm the comparison appears in a **new tooltip panel** beside the standard inspector.

If no custom tooltip root is assigned, the extension creates `ComparisonTooltip` automatically under the same UI parent as the inspector.
In auto-created mode, the tooltip background height expands/contracts to match the text content.

---

## Option B: Use your own custom tooltip object (for custom visuals)

Use this if you want full control over styling/animation.

1. In your UI Canvas, create a new GameObject for the comparison tooltip.
   - Add a `RectTransform` (required).
   - Add an `Image` (optional but recommended for background).
2. Create a child `Text` object for the comparison lines.
3. On the object that has `ItemComparisonExtension`, assign:
   - `Comparison Tooltip Root` → your tooltip root `RectTransform`.
   - `Comparison Text` → your child `Text`.
4. Keep the tooltip root disabled by default (optional but recommended).
5. Enter Play Mode and validate behavior.

---

## Inspector field reference (ItemComparisonExtension)

### Comparison Tooltip Window

- **Comparison Tooltip Root** (`RectTransform`)
  - Optional custom tooltip window root.
  - Leave empty for auto-creation.

- **Tooltip Offset** (`Vector2`, default `20, 0`)
  - Horizontal/vertical offset from the inspected item tooltip.
  - Increase X if windows are too close.

- **Tooltip Background Color** (`Color`, default black with high alpha)
  - Used by the auto-created tooltip only.

### Comparison Text Override

- **Comparison Text** (`Text`)
  - Optional custom text component used to render deltas.
  - If empty, a clone of the inspector attributes text is generated inside the comparison tooltip root.

### Custom Window Texts (Optional)

- **Comparison Title Text** (`Text`)
- **Comparison Attributes Text** (`Text`)
- **Comparison Additional Attributes Text** (`Text`)
  - If **all three** are assigned, equipped-item details are rendered into these fields and removed from the comparison delta block.
  - In this mode, the main comparison text shows only the **Differences** section.

### Input

- **Comparison Action** (`InputActionReference`)
  - Optional action for displaying comparison.
  - If not assigned, Shift keys are used as fallback.

### Comparison Colors

- **Better Color** / **Worse Color**
  - Controls positive/negative delta coloring.

### Formatting

- **Spacing Above Differences** (`int`, default `1`)
  - Controls how many blank lines are inserted above the `Differences:` heading when equipped details are displayed in the same text block.
- **Spacing Below Attributes** (`int`, default `0`)
  - Adds extra blank lines below the equipped attributes block.
  - This value is additive with **Spacing Above Differences**.
- **Differences Heading** (`string`, default `Differences:`)
  - Optional heading shown above delta lines.
  - Leave empty to remove the heading entirely.

---

## Validation checklist

After installation, verify:

- Main item tooltip still opens normally.
- Holding comparison key opens a **second** tooltip window.
- The second window starts with the equipped item information.
- A **Differences** heading appears with delta lines underneath.
- If there are no stat changes, no comparison tooltip is shown.
- Releasing the key hides only the comparison tooltip.
- Comparison tooltip follows tooltip side logic (left/right of inspector as needed).
- Comparison tooltip stays clamped within its parent UI bounds and should not jump to a corner.
- Equipped-item comparisons still compute for weapons, armor, shields, and additional attributes.

---

## Troubleshooting

### Comparison tooltip does not show

- Confirm item is equippable and has a valid equipped counterpart.
- Confirm comparison key is pressed (or bound action is active).
- Confirm `ItemComparison` plugin is enabled.

### Tooltip appears without background

- If auto-created root is used, ensure Unity UI `Image` is available and not overridden by theme code.
- If custom root is used, add/configure an `Image` on your root object.

### Wrong placement

- Adjust `Tooltip Offset` in `ItemComparisonExtension`.
- If using a custom root, verify anchors/pivot are not being overwritten by another layout script.

---

## Notes for teams / source control

- Commit both script and this guide so designers can configure custom tooltips without reading code.
- If your project uses prefab variants for inspector UI, document which prefab hosts `ItemComparisonExtension` and where custom references are assigned.
