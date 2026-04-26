using UnityEngine;
using UnityEditor;
using System.Linq;
namespace ProjectDoors
{

    [InitializeOnLoad]
    public static class DoorHierarchyIcons
    {
        // Preload your icons
        private static Texture2D pivotIcon;
        private static Texture2D knobIcon;

        static DoorHierarchyIcons()
        {
            // Listen to the Hierarchy drawing event
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;

            // Load the icons from your specified Assets folder
            // Make sure the paths & filenames match exactly
            pivotIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ANSTUDIO/Doors Project/Scripts/Hierarchy/Ui/pivot-hierarchy.png"
            );
            knobIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ANSTUDIO/Doors Project/Scripts/Hierarchy/Ui/knob-hierarchy.png"
            );
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            // Convert this row's instanceID -> an Object
            Object obj = EditorUtility.InstanceIDToObject(instanceID);
            if (obj is GameObject go)
            {
                // If the GameObject has a Door component
                Door door = go.GetComponent<Door>();
                if (door != null)
                {
                    // 1) "Pivot Adjustment Mode" is just 'door.IsPivotAdjustmentEnabled'
                    bool pivotActive = door.IsPivotAdjustmentEnabled;

                    // 2) "Knob is in edit position" means: 
                    //    ANY attached knob has isLocked == false
                    bool knobInEditPosition = false;
                    if (door.attachedObjects != null && door.attachedObjects.Count > 0)
                    {
                        knobInEditPosition = door.attachedObjects.Any(k => k != null && !k.isLocked);
                    }

                    // If neither pivot nor knob is active, do nothing
                    if (!pivotActive && !knobInEditPosition) return;

                    // We'll draw each icon+text pair in a ~55px slice
                    // If both are true, that's 2 slices = 110px total
                    int activeCount = 0;
                    if (pivotActive) activeCount++;
                    if (knobInEditPosition) activeCount++;

                    float totalWidth = activeCount * 55f;

                    // We'll start drawing at the right edge minus totalWidth
                    Rect pairRect = new Rect(selectionRect.xMax - totalWidth, selectionRect.y, 55, selectionRect.height);

                    // A simple style for the text
                    GUIStyle textStyle = new GUIStyle(EditorStyles.label)
                    {
                        normal = { textColor = Color.white },
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleLeft
                    };

                    // 1) If pivot is active, draw pivot icon + "PAM"
                    if (pivotActive)
                    {
                        DrawIconAndText(pairRect, pivotIcon, "PAM", textStyle);
                        pairRect.x += 55; // move over for next pair
                    }

                    // 2) If knob is in edit position, draw knob icon + "KPE"
                    if (knobInEditPosition)
                    {
                        DrawIconAndText(pairRect, knobIcon, "KPE", textStyle);
                        pairRect.x += 55;
                    }
                }
            }
        }

        private static void DrawIconAndText(Rect pairRect, Texture2D icon, string label, GUIStyle style)
        {
            // A 16×16 icon placed at the center-left within pairRect
            Rect iconRect = new Rect(
                pairRect.x,
                pairRect.y + (pairRect.height - 16) * 0.5f,
                16, 16
            );

            // The text "container" (full width)
            Rect textRect = new Rect(
                iconRect.xMax + 2,
                pairRect.y,
                pairRect.width - 18, // ~ 55 total minus icon/spacing
                pairRect.height
            );

            // 1) Draw the icon
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            // 2) Draw the text with a colored background
            if (!string.IsNullOrEmpty(label))
            {
                // First, draw a background rectangle over the full textRect
                EditorGUI.DrawRect(
                    textRect,
                    new Color(15f / 255f, 53f / 255f, 25f / 255f, 0.7f)
                );

                // 3) Add some "padding" inside textRect so the text
                //    has the same space at the beginning and the end.
                const float sidePadding = 4f;
                Rect paddedTextRect = new Rect(
                    textRect.x + sidePadding,
                    textRect.y,
                    textRect.width - sidePadding * 2f,
                    textRect.height
                );

                // 4) Draw the label on top of that padded area
                GUI.Label(paddedTextRect, label, style);
            }
        }

    }
}