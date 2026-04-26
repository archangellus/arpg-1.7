using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Text;

namespace PLAYERTWO.ARPGProject.ItemComparison
{
    /// <summary>
    /// Runtime component that augments the item inspector with comparison data.
    /// </summary>
    public class ItemComparisonExtension : MonoBehaviour
    {
        [Header("Comparison Text Override")]
        [Tooltip("Optional text object to display comparison information. If not set, a clone of the attributes text will be created automatically.")]
        public Text comparisonText;

        [Header("Custom Window Texts (Optional)")]
        [Tooltip("Optional title text for custom comparison windows. When title/attributes/additional attributes are all set, equipped details are shown here instead of in comparisonText.")]
        public Text comparisonTitleText;

        [Tooltip("Optional attributes text for custom comparison windows. Used with comparisonTitleText and comparisonAdditionalAttributesText.")]
        public Text comparisonAttributesText;

        [Tooltip("Optional additional attributes text for custom comparison windows. Used with comparisonTitleText and comparisonAttributesText.")]
        public Text comparisonAdditionalAttributesText;

        [Header("Comparison Tooltip Window")]
        [Tooltip("Optional RectTransform used as the comparison tooltip window. If not set, one is created automatically.")]
        public RectTransform comparisonTooltipRoot;

        [Tooltip("Offset applied to the auto-created comparison tooltip window.")]
        public Vector2 tooltipOffset = new(20f, 0f);

        [Tooltip("Background color used by the auto-created comparison tooltip window.")]
        public Color tooltipBackgroundColor = new(0f, 0f, 0f, 0.88f);

        [Header("Input")]
        [Tooltip("Optional input action to trigger comparison. If not set, Shift keys are used by default.")]
        public InputActionReference comparisonAction;

        [Header("Comparison Colors")]
        [Tooltip("Color used when the inspected item is better than the equipped one.")]
        public Color betterColor = GameColors.LightBlue;

        [Tooltip("Color used when the inspected item is worse than the equipped one.")]
        public Color worseColor = GameColors.LightRed;

        [Header("Formatting")]
        [Tooltip("Number of blank lines to insert above the 'Differences:' heading when equipped details are shown.")]
        [Min(0)]
        public int spacingAboveDifferences = 1;

        [Tooltip("Number of blank lines to insert below the equipped Attributes block when equipped details are shown.")]
        [Min(0)]
        public int spacingBelowAttributes = 0;

        [Tooltip("Optional heading shown above delta lines. Leave empty to hide the heading.")]
        public string differencesHeading = "Differences:";

        private GUIItemInspector m_inspector;
        private ItemInstance m_item;
        private GUIItem m_guiItem;
        private Text m_comparisonText;
        private RectTransform m_comparisonTooltipRoot;
        private bool m_autoCreatedTooltipRoot;
        private bool m_showComparison;
        private Entity m_entity;
        private bool m_actionAutoEnabled;

        private void OnEnable()
        {
            EnsureComparisonAction();
        }

        private void OnDisable()
        {
            if (m_actionAutoEnabled && comparisonAction != null && comparisonAction.action != null)
            {
                comparisonAction.action.Disable();
                m_actionAutoEnabled = false;
            }
        }

        public void Configure(GUIItemInspector inspector, ItemInstance item, GUIItem guiItem)
        {
            m_inspector = inspector;
            m_item = item;
            m_guiItem = guiItem;
            m_entity = Level.instance?.player;

            EnsureComparisonText();
            ResetComparison();
        }

        public void Refresh(ItemInstance item, GUIItem guiItem)
        {
            m_item = item;
            m_guiItem = guiItem;

            ResetComparison();
        }

        public void HandleHidden()
        {
            ResetComparison();
        }

        private void ResetComparison()
        {
            if (m_comparisonText != null)
            {
                m_comparisonText.text = string.Empty;
                m_comparisonText.gameObject.SetActive(false);
            }

            ClearCustomEquippedDetailsTexts();

            if (m_comparisonTooltipRoot != null)
                m_comparisonTooltipRoot.gameObject.SetActive(false);

            m_showComparison = false;
        }

        private void EnsureComparisonText()
        {
            EnsureComparisonTooltipRoot();

            if (comparisonText != null)
            {
                m_comparisonText = comparisonText;
            }
            else
            {
                if (m_comparisonText != null || m_inspector == null || m_inspector.attributesText == null || m_comparisonTooltipRoot == null)
                    return;

                var template = m_inspector.attributesText.gameObject;
                var clone = Instantiate(template, m_comparisonTooltipRoot);
                clone.name = "ComparisonText";
                m_comparisonText = clone.GetComponent<Text>();

                if (m_comparisonText != null)
                {
                    var rect = m_comparisonText.rectTransform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = new Vector2(12f, 12f);
                    rect.offsetMax = new Vector2(-12f, -12f);
                }
            }

            if (m_comparisonText != null)
            {
                m_comparisonText.text = string.Empty;
                m_comparisonText.gameObject.SetActive(false);
            }

            ClearCustomEquippedDetailsTexts();
        }

        private void EnsureComparisonTooltipRoot()
        {
            if (comparisonTooltipRoot != null)
            {
                m_comparisonTooltipRoot = comparisonTooltipRoot;
                m_autoCreatedTooltipRoot = false;
                return;
            }

            if (m_comparisonTooltipRoot != null || m_inspector == null)
                return;

            var parent = m_inspector.transform.parent;
            if (parent == null)
                return;

            var root = new GameObject("ComparisonTooltip", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);

            m_comparisonTooltipRoot = root.GetComponent<RectTransform>();
            m_comparisonTooltipRoot.sizeDelta = new Vector2(320f, 200f);
            m_autoCreatedTooltipRoot = true;

            var background = root.GetComponent<Image>();
            background.color = tooltipBackgroundColor;

            m_comparisonTooltipRoot.gameObject.SetActive(false);
        }

        private void EnsureComparisonAction()
        {
            var action = comparisonAction?.action;
            if (action == null)
                return;

            if (!action.enabled)
            {
                action.Enable();
                m_actionAutoEnabled = true;
            }
        }

        private void Update()
        {
            if (m_comparisonText == null || m_item == null || m_inspector == null)
                return;

            bool pressed = IsComparisonPressed();

            if (pressed && !m_showComparison && m_inspector.gameObject.activeSelf && m_item.IsEquippable())
            {
                var equipped = GetEquippedItem(m_item);
                var useCustomEquippedDetails = HasCustomEquippedDetailsFields();
                var text = BuildComparisonText(equipped, !useCustomEquippedDetails);

                if (text.Length > 0)
                {
                    m_comparisonText.text = text;

                    if (useCustomEquippedDetails)
                        PopulateCustomEquippedDetailsTexts(equipped);

                    UpdateComparisonTooltipSize();
                    m_comparisonText.gameObject.SetActive(true);
                    if (m_comparisonTooltipRoot != null)
                        m_comparisonTooltipRoot.gameObject.SetActive(true);
                    UpdateComparisonTooltipPosition();
                    m_showComparison = true;
                }
            }
            else if (pressed && m_showComparison)
            {
                UpdateComparisonTooltipPosition();
            }
            else if (!pressed && m_showComparison)
            {
                ResetComparison();
            }
        }

        private void UpdateComparisonTooltipSize()
        {
            if (m_comparisonTooltipRoot == null || m_comparisonText == null)
                return;

            if (!m_autoCreatedTooltipRoot)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(m_comparisonText.rectTransform);

            var currentSize = m_comparisonTooltipRoot.sizeDelta;
            var contentHeight = m_comparisonText.preferredHeight + 24f;
            m_comparisonTooltipRoot.sizeDelta = new Vector2(currentSize.x, Mathf.Max(48f, contentHeight));
        }

        private bool HasCustomEquippedDetailsFields()
        {
            return comparisonTitleText != null &&
                   comparisonAttributesText != null &&
                   comparisonAdditionalAttributesText != null;
        }

        private void PopulateCustomEquippedDetailsTexts(ItemInstance equipped)
        {
            if (!HasCustomEquippedDetailsFields() || equipped == null)
                return;

            comparisonTitleText.text = equipped.data.name;
            comparisonAttributesText.text = equipped.Inspect(m_entity?.stats, m_inspector.attentionColor, m_inspector.invalidColor);
            comparisonAdditionalAttributesText.text = equipped.attributes?.Inspect() ?? string.Empty;
        }

        private void ClearCustomEquippedDetailsTexts()
        {
            if (comparisonTitleText != null)
                comparisonTitleText.text = string.Empty;

            if (comparisonAttributesText != null)
                comparisonAttributesText.text = string.Empty;

            if (comparisonAdditionalAttributesText != null)
                comparisonAdditionalAttributesText.text = string.Empty;
        }

private void UpdateComparisonTooltipPosition()
{
    if (m_comparisonTooltipRoot == null || m_inspector == null)
        return;

    var inspectorRect = m_inspector.GetComponent<RectTransform>();
    var parentRect = m_comparisonTooltipRoot.parent as RectTransform;
    if (inspectorRect == null || parentRect == null)
        return;

    var worldCorners = new Vector3[4];
    inspectorRect.GetWorldCorners(worldCorners);

    var leftEdge = (Vector2)parentRect.InverseTransformPoint(worldCorners[0]);
    var rightEdge = (Vector2)parentRect.InverseTransformPoint(worldCorners[3]);
    var inspectorPivotPosition = (Vector2)parentRect.InverseTransformPoint(inspectorRect.position);

    var inspectorCenterX = (leftEdge.x + rightEdge.x) * 0.5f;
    var inspectorOnRightSide = inspectorCenterX >= parentRect.rect.center.x;
    var pivotY = inspectorRect.pivot.y;
    var spacing = Mathf.Abs(tooltipOffset.x);
    var verticalOffset = tooltipOffset.y;

    if (inspectorOnRightSide)
    {
        m_comparisonTooltipRoot.pivot = new Vector2(1f, pivotY);
        m_comparisonTooltipRoot.localPosition = new Vector3(
            leftEdge.x - spacing,
            inspectorPivotPosition.y + verticalOffset,
            m_comparisonTooltipRoot.localPosition.z);
    }
    else
    {
        m_comparisonTooltipRoot.pivot = new Vector2(0f, pivotY);
        m_comparisonTooltipRoot.localPosition = new Vector3(
            rightEdge.x + spacing,
            inspectorPivotPosition.y + verticalOffset,
            m_comparisonTooltipRoot.localPosition.z);
    }

    ClampTooltipToParentBounds();
}

        private void ClampTooltipToParentBounds()
        {
            if (m_comparisonTooltipRoot == null)
                return;

            var parentRect = m_comparisonTooltipRoot.parent as RectTransform;
            if (parentRect == null)
                return;

            var localPoint = (Vector2)parentRect.InverseTransformPoint(m_comparisonTooltipRoot.position);
            var parentBounds = parentRect.rect;
            var tooltipRect = m_comparisonTooltipRoot.rect;
            var pivot = m_comparisonTooltipRoot.pivot;

            var minX = parentBounds.xMin + tooltipRect.width * pivot.x;
            var maxX = parentBounds.xMax - tooltipRect.width * (1f - pivot.x);
            var minY = parentBounds.yMin + tooltipRect.height * pivot.y;
            var maxY = parentBounds.yMax - tooltipRect.height * (1f - pivot.y);

            if (minX > maxX || minY > maxY)
            {
                //Debug.Log($"[ItemComparison] Clamp skipped (tooltip larger than parent). minX={minX}, maxX={maxX}, minY={minY}, maxY={maxY}");
                return;
            }

            var before = m_comparisonTooltipRoot.localPosition;
            var clampedX = Mathf.Clamp(localPoint.x, minX, maxX);
            var clampedY = Mathf.Clamp(localPoint.y, minY, maxY);
            m_comparisonTooltipRoot.localPosition = new Vector3(clampedX, clampedY, m_comparisonTooltipRoot.localPosition.z);
            //Debug.Log($"[ItemComparison] Clamp applied localPosition {before} -> {m_comparisonTooltipRoot.localPosition} | boundsX[{minX},{maxX}] boundsY[{minY},{maxY}]");
        }

        private bool IsComparisonPressed()
        {
            var action = comparisonAction?.action;
            if (action != null)
            {
                EnsureComparisonAction();
                if (action.enabled && action.IsPressed())
                    return true;
            }

            var keyboard = Keyboard.current;
            return keyboard != null &&
                (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        private ItemInstance GetEquippedItem(ItemInstance item)
        {
            var items = m_entity?.items;
            if (items == null || item == null)
                return null;

            if (item.IsArmor())
            {
                switch (item.GetArmor().slot)
                {
                    case ItemSlots.Helm:
                        return items.GetHelm();
                    case ItemSlots.Chest:
                        return items.GetChest();
                    case ItemSlots.Pants:
                        return items.GetPants();
                    case ItemSlots.Gloves:
                        return items.GetGloves();
                    case ItemSlots.Boots:
                        return items.GetBoots();
                }
            }
            else if (item.IsShield())
            {
                return items.GetLeftHand();
            }
            else if (item.IsBow())
            {
                return items.GetRightHand();
            }
            else if (item.IsWeapon())
            {
                var right = items.GetRightHand();
                if (right != null && right.IsWeapon())
                    return right;

                var left = items.GetLeftHand();
                if (left != null && left.IsWeapon())
                    return left;
            }

            return null;
        }

        private string BuildComparisonText(ItemInstance equipped, bool includeEquippedDetails)
        {
            if (equipped == null || equipped == m_item)
                return string.Empty;

            var deltaLines = new StringBuilder();

            if (m_item.IsWeapon() && equipped.IsWeapon())
            {
                var min = m_item.GetWeapon().minDamage - equipped.GetWeapon().minDamage;
                var max = m_item.GetWeapon().maxDamage - equipped.GetWeapon().maxDamage;
                if (min != 0 || max != 0)
                    AppendDeltaLine(deltaLines, $"Damage: {FormatDelta(min)} ~ {FormatDelta(max)}");
                var atk = m_item.GetWeapon().attackSpeed - equipped.GetWeapon().attackSpeed;
                if (atk != 0)
                    AppendDeltaLine(deltaLines, $"Attack Speed: {FormatDelta(atk)}");
            }
            else if ((m_item.IsArmor() || m_item.IsShield()) && (equipped.IsArmor() || equipped.IsShield()))
            {
                int def = 0;
                if (m_item.IsArmor())
                    def += m_item.GetArmor().defense;
                if (m_item.IsShield())
                    def += m_item.GetShield().defense;

                int eqDef = 0;
                if (equipped.IsArmor())
                    eqDef += equipped.GetArmor().defense;
                if (equipped.IsShield())
                    eqDef += equipped.GetShield().defense;

                var diff = def - eqDef;
                if (diff != 0)
                    AppendDeltaLine(deltaLines, $"Defense: {FormatDelta(diff)}");
            }

            int health = m_item.GetAdditionalHealth() - equipped.GetAdditionalHealth();
            int mana = m_item.GetAdditionalMana() - equipped.GetAdditionalMana();
            int damage = m_item.GetAdditionalDamage() - equipped.GetAdditionalDamage();
            int speed = m_item.GetAttackSpeed() - equipped.GetAttackSpeed();
            int defense = m_item.GetAdditionalDefense() - equipped.GetAdditionalDefense();

            if (damage != 0)
                AppendDeltaLine(deltaLines, $"Additional Damage: {FormatDelta(damage)}");
            if (speed != 0)
                AppendDeltaLine(deltaLines, $"Additional Attack Speed: {FormatDelta(speed)}");
            if (defense != 0)
                AppendDeltaLine(deltaLines, $"Additional Defense: {FormatDelta(defense)}");
            if (mana != 0)
                AppendDeltaLine(deltaLines, $"Additional Mana: {FormatDelta(mana)}");
            if (health != 0)
                AppendDeltaLine(deltaLines, $"Additional Health: {FormatDelta(health)}");

            if (deltaLines.Length == 0)
                return string.Empty;

            if (!includeEquippedDetails)
            {
                var differencesOnly = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(differencesHeading))
                    differencesOnly.AppendLine(differencesHeading);
                differencesOnly.Append(deltaLines.Length > 0 ? deltaLines.ToString() : "No stat differences.");
                return differencesOnly.ToString();
            }

            var equippedInspect = equipped
                .Inspect(m_entity?.stats, m_inspector.attentionColor, m_inspector.invalidColor)
                .TrimEnd();
            var withEquippedDetails = new StringBuilder();
            withEquippedDetails.AppendLine($"Equipped: {equipped.data.name}");
            withEquippedDetails.Append(equippedInspect);
            withEquippedDetails.Append('\n');

            var lines = Mathf.Max(0, spacingAboveDifferences + spacingBelowAttributes);
            for (var i = 0; i < lines; i++)
                withEquippedDetails.Append('\n');

            if (!string.IsNullOrWhiteSpace(differencesHeading))
                withEquippedDetails.AppendLine(differencesHeading);
            withEquippedDetails.Append(deltaLines.ToString());
            return withEquippedDetails.ToString();
        }

        private void AppendDeltaLine(StringBuilder builder, string line)
        {
            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(line);
        }

        private string FormatDelta(float value)
        {
            if (Mathf.Approximately(value, 0))
                return "0";

            var color = value > 0 ? betterColor : worseColor;
            var sign = value > 0 ? "+" : string.Empty;
            return StringWithColor($"{sign}{value}", color);
        }

        private string StringWithColor(string value, Color color)
        {
            var hex = ColorUtility.ToHtmlStringRGBA(color);
            return $"<color=#{hex}>{value}</color>";
        }
    }
}
