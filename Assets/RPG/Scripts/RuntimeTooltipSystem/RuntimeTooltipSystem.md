TOOLTIP SYSTEM

No-programming setup
Put RuntimeTooltipSystem.cs anywhere under Assets/.
In Unity, go to Tools > Runtime Tooltips > Create Tooltip Manager.
For uGUI, select your UI GameObjects, then use
Tools > Runtime Tooltips > Add Tooltip Target To Selected uGUI Elements.
Fill in the Title and Body fields in the Inspector.
For UI Toolkit, select your UIDocument, add
TooltipUIDocumentBinder, then add bindings by VisualElement name.

uGUI support uses Unity’s EventSystem pointer interfaces like IPointerEnterHandler, IPointerExitHandler, and IPointerMoveHandler; Unity’s uGUI docs describe these as the supported pointer callbacks when a valid EventSystem is configured.

API examples
##
using RuntimeTooltips;
using UnityEngine;

RuntimeTooltipManager.Instance.ShowAtScreen(
    "Save",
    "Writes the current profile to disk.",
    Input.mousePosition
);
##
##
using RuntimeTooltips;
using UnityEngine.UIElements;

var info = TooltipInfo.Create(
    "Inventory Slot",
    "Drag an item here to equip it."
);
##

RuntimeTooltipManager.Instance.RegisterVisualElement(myButton, info);

The script includes editor menu helpers, a reusable RuntimeTooltipStyle asset, keyboard/gamepad focus support, rich text, edge clamping, follow-cursor behavior, and an Inspector-only workflow for designers.