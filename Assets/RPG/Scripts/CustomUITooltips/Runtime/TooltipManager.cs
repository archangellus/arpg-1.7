using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace CustomUITooltips
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class TooltipManager : MonoBehaviour
    {
        private static TooltipManager instance;
        private static bool warnedMissingManager;

        [Header("Required")]
        [Tooltip("UIDocument used as the runtime tooltip overlay. Use Tools/Custom Tooltips/Create Runtime Tooltip System to create one.")]
        public UIDocument tooltipDocument;

        [Header("Defaults")]
        public TooltipProfile defaultProfile;
        public bool dontDestroyOnLoad;

        private VisualElement root;
        private VisualElement layer;
        private VisualElement card;
        private Image icon;
        private Label titleLabel;
        private Label bodyLabel;

        private Coroutine showRoutine;
        private TooltipContent currentContent;
        private TooltipProfile currentProfile;
        private object currentOwner;
        private Vector2 lastPanelPosition;
        private Rect currentTargetPanelRect;
        private bool hasTargetPanelRect;
        private bool visible;

        public static TooltipManager Instance
        {
            get
            {
                TryGetInstance(out TooltipManager manager, true);
                return manager;
            }
        }

        public static bool TryGetInstance(out TooltipManager manager, bool logIfMissing = true)
        {
            if (instance != null && instance.isActiveAndEnabled && instance.HasDocumentReference())
            {
                manager = instance;
                return true;
            }

            instance = FindBestInstance();
            manager = instance;

            if (manager != null)
                return true;

            if (logIfMissing && !warnedMissingManager)
            {
                warnedMissingManager = true;
                Debug.LogWarning("No active TooltipManager with a UIDocument and Panel Settings was found. Use Tools > Custom Tooltips > Create Runtime Tooltip System in this scene.");
            }

            return false;
        }

        private static TooltipManager FindBestInstance()
        {
            TooltipManager[] managers = FindObjectsByType<TooltipManager>(FindObjectsSortMode.None);
            TooltipManager firstActive = null;

            foreach (TooltipManager manager in managers)
            {
                if (manager == null || !manager.isActiveAndEnabled)
                    continue;

                if (firstActive == null)
                    firstActive = manager;

                if (manager.HasDocumentReference())
                    return manager;
            }

            return firstActive;
        }

        private void Awake()
        {
            if (tooltipDocument == null)
                tooltipDocument = GetComponent<UIDocument>();

            if (defaultProfile == null)
                defaultProfile = TooltipProfile.CreateRuntimeDefault();

            if (instance != null && instance != this)
            {
                TooltipManager existing = instance;
                bool thisConfigured = HasDocumentReference();
                bool existingConfigured = existing != null && existing.HasDocumentReference();

                // Prefer the configured scene manager over an old/unconfigured manager reference.
                if (thisConfigured && !existingConfigured)
                {
                    if (existing != null)
                        Destroy(existing.gameObject);
                }
                else
                {
                    Destroy(gameObject);
                    return;
                }
            }

            instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (instance == null || !instance.isActiveAndEnabled || !instance.HasDocumentReference())
                instance = this;

            EnsureVisualTree();
        }

        private void OnDisable()
        {
            StopShowing();

            if (instance == this)
                instance = null;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public void Show(TooltipContent content, Vector2 screenPosition, Rect? targetScreenRect = null, object owner = null)
        {
            if (!PrepareShow(content, owner))
                return;

            lastPanelPosition = ScreenToPanel(screenPosition);
            hasTargetPanelRect = targetScreenRect.HasValue;
            currentTargetPanelRect = targetScreenRect.HasValue ? ScreenRectToPanelRect(targetScreenRect.Value) : default;
            BeginDelayedShow();
        }

        public void ShowAtPanelPosition(TooltipContent content, Vector2 panelPosition, Rect? targetPanelRect = null, object owner = null)
        {
            if (!PrepareShow(content, owner))
                return;

            lastPanelPosition = panelPosition;
            hasTargetPanelRect = targetPanelRect.HasValue;
            currentTargetPanelRect = targetPanelRect ?? default;
            BeginDelayedShow();
        }

        public void Move(Vector2 screenPosition, object owner = null)
        {
            if (!OwnsTooltip(owner))
                return;

            lastPanelPosition = ScreenToPanel(screenPosition);
            if (visible && currentContent != null && currentContent.followPointer)
                PositionCard();
        }

        public void MoveAtPanelPosition(Vector2 panelPosition, object owner = null)
        {
            if (!OwnsTooltip(owner))
                return;

            lastPanelPosition = panelPosition;
            if (visible && currentContent != null && currentContent.followPointer)
                PositionCard();
        }

        public void Hide(object owner = null)
        {
            if (!OwnsTooltip(owner))
                return;

            StopShowing();

            if (card != null)
                card.style.display = DisplayStyle.None;
        }

        private void StopShowing()
        {
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }

            visible = false;
            currentOwner = null;
        }

        private bool PrepareShow(TooltipContent content, object owner)
        {
            if (content == null || content.IsEmpty)
                return false;

            if (!EnsureVisualTree())
            {
                string documentName = tooltipDocument != null ? tooltipDocument.name : "null";
                string panelSettingsName = tooltipDocument != null && tooltipDocument.panelSettings != null ? tooltipDocument.panelSettings.name : "null";
                Debug.LogWarning($"TooltipManager needs an active UIDocument with Panel Settings before it can display runtime tooltips. Manager='{name}', UIDocument='{documentName}', PanelSettings='{panelSettingsName}'.", this);
                return false;
            }

            currentContent = content;
            currentProfile = content.overrideProfile != null ? content.overrideProfile : defaultProfile;
            currentOwner = owner ?? this;
            ApplyContent(content, currentProfile);
            return true;
        }

        private void BeginDelayedShow()
        {
            if (showRoutine != null)
                StopCoroutine(showRoutine);

            card.style.display = DisplayStyle.None;
            visible = false;
            float delay = currentContent.showDelay >= 0f ? currentContent.showDelay : currentProfile.defaultShowDelay;
            showRoutine = StartCoroutine(ShowAfterDelay(delay));
        }

        private IEnumerator ShowAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                if (currentProfile.useUnscaledTime)
                    yield return new WaitForSecondsRealtime(delay);
                else
                    yield return new WaitForSeconds(delay);
            }

            if (!EnsureVisualTree())
            {
                showRoutine = null;
                yield break;
            }

            card.style.display = DisplayStyle.Flex;
            visible = true;

            // Give UI Toolkit one layout pass so resolvedStyle has useful dimensions.
            yield return null;
            PositionCard();
            showRoutine = null;
        }

        private bool HasDocumentReference()
        {
            if (tooltipDocument == null)
                tooltipDocument = GetComponent<UIDocument>();

            return tooltipDocument != null && tooltipDocument.panelSettings != null;
        }

        private bool EnsureVisualTree()
        {
            if (!HasDocumentReference())
                return false;

            VisualElement documentRoot = tooltipDocument.rootVisualElement;
            if (documentRoot == null || documentRoot.panel == null)
                return false;

            if (layer != null && layer.parent == documentRoot)
                return true;

            root = documentRoot;
            BuildVisualTree();
            return true;
        }

        private static void SetPickingModeRecursive(VisualElement element, PickingMode pickingMode)
        {
            if (element == null)
                return;

            element.pickingMode = pickingMode;

            foreach (VisualElement child in element.Children())
                SetPickingModeRecursive(child, pickingMode);
        }

        private void BuildVisualTree()
        {
            SetPickingModeRecursive(root, PickingMode.Ignore);

            layer = new VisualElement { name = "custom-tooltip-layer", pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0f;
            layer.style.top = 0f;
            layer.style.right = 0f;
            layer.style.bottom = 0f;

            card = new VisualElement { name = "custom-tooltip-card", pickingMode = PickingMode.Ignore };
            card.style.position = Position.Absolute;
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.FlexStart;
            card.style.display = DisplayStyle.None;

            icon = new Image { name = "custom-tooltip-icon", pickingMode = PickingMode.Ignore };
            icon.scaleMode = ScaleMode.ScaleToFit;

            VisualElement textColumn = new VisualElement { name = "custom-tooltip-text", pickingMode = PickingMode.Ignore };
            textColumn.style.flexDirection = FlexDirection.Column;
            textColumn.style.flexGrow = 1f;

            titleLabel = new Label { name = "custom-tooltip-title", pickingMode = PickingMode.Ignore };
            bodyLabel = new Label { name = "custom-tooltip-body", pickingMode = PickingMode.Ignore };
            titleLabel.style.whiteSpace = WhiteSpace.Normal;
            bodyLabel.style.whiteSpace = WhiteSpace.Normal;

            textColumn.Add(titleLabel);
            textColumn.Add(bodyLabel);
            card.Add(icon);
            card.Add(textColumn);
            layer.Add(card);
            root.Add(layer);
        }

        private void ApplyContent(TooltipContent content, TooltipProfile profile)
        {
            card.style.minWidth = profile.minWidth;
            card.style.maxWidth = profile.maxWidth;
            card.style.paddingLeft = profile.padding;
            card.style.paddingRight = profile.padding;
            card.style.paddingTop = profile.padding;
            card.style.paddingBottom = profile.padding;
            card.style.backgroundColor = profile.backgroundColor;
            card.style.borderTopColor = profile.borderColor;
            card.style.borderRightColor = profile.borderColor;
            card.style.borderBottomColor = profile.borderColor;
            card.style.borderLeftColor = profile.borderColor;
            card.style.borderTopWidth = profile.borderWidth;
            card.style.borderRightWidth = profile.borderWidth;
            card.style.borderBottomWidth = profile.borderWidth;
            card.style.borderLeftWidth = profile.borderWidth;
            card.style.borderTopLeftRadius = profile.cornerRadius;
            card.style.borderTopRightRadius = profile.cornerRadius;
            card.style.borderBottomLeftRadius = profile.cornerRadius;
            card.style.borderBottomRightRadius = profile.cornerRadius;

            bool hasIcon = content.icon != null;
            icon.style.display = hasIcon ? DisplayStyle.Flex : DisplayStyle.None;
            icon.style.width = profile.iconSize;
            icon.style.height = profile.iconSize;
            icon.style.marginRight = hasIcon ? profile.gap : 0f;
            icon.image = hasIcon ? content.icon.texture : null;

            titleLabel.text = content.title ?? string.Empty;
            bodyLabel.text = content.body ?? string.Empty;
            titleLabel.style.display = string.IsNullOrWhiteSpace(content.title) ? DisplayStyle.None : DisplayStyle.Flex;
            bodyLabel.style.display = string.IsNullOrWhiteSpace(content.body) ? DisplayStyle.None : DisplayStyle.Flex;
            titleLabel.style.color = profile.titleColor;
            bodyLabel.style.color = profile.bodyColor;
            titleLabel.style.fontSize = profile.titleFontSize;
            bodyLabel.style.fontSize = profile.bodyFontSize;
            titleLabel.enableRichText = profile.enableRichText;
            bodyLabel.enableRichText = profile.enableRichText;
            bodyLabel.style.marginTop = string.IsNullOrWhiteSpace(content.title) ? 0f : 4f;
        }

        private void PositionCard()
        {
            if (root == null || card == null || currentContent == null || currentProfile == null)
                return;

            float rootWidth = root.resolvedStyle.width > 0f ? root.resolvedStyle.width : Screen.width;
            float rootHeight = root.resolvedStyle.height > 0f ? root.resolvedStyle.height : Screen.height;
            float cardWidth = card.resolvedStyle.width > 0f ? card.resolvedStyle.width : currentProfile.maxWidth;
            float cardHeight = card.resolvedStyle.height > 0f ? card.resolvedStyle.height : 60f;
            Vector2 pos = CalculatePreferredPosition(cardWidth, cardHeight);

            float margin = currentProfile.screenMargin;
            pos.x = Mathf.Clamp(pos.x, margin, Mathf.Max(margin, rootWidth - cardWidth - margin));
            pos.y = Mathf.Clamp(pos.y, margin, Mathf.Max(margin, rootHeight - cardHeight - margin));

            card.style.left = pos.x;
            card.style.top = pos.y;
        }

        private Vector2 CalculatePreferredPosition(float cardWidth, float cardHeight)
        {
            Vector2 offset = currentContent.offset;

            if (!hasTargetPanelRect || currentContent.placement == TooltipPlacement.FollowPointer)
                return lastPanelPosition + offset;

            Rect target = currentTargetPanelRect;
            switch (currentContent.placement)
            {
                case TooltipPlacement.Above:
                    return new Vector2(target.center.x - cardWidth * 0.5f, target.yMin - cardHeight - Mathf.Abs(offset.y));
                case TooltipPlacement.Below:
                    return new Vector2(target.center.x - cardWidth * 0.5f, target.yMax + Mathf.Abs(offset.y));
                case TooltipPlacement.Left:
                    return new Vector2(target.xMin - cardWidth - Mathf.Abs(offset.x), target.center.y - cardHeight * 0.5f);
                case TooltipPlacement.Right:
                    return new Vector2(target.xMax + Mathf.Abs(offset.x), target.center.y - cardHeight * 0.5f);
                case TooltipPlacement.TopLeft:
                    return new Vector2(target.xMin + offset.x, target.yMin - cardHeight - Mathf.Abs(offset.y));
                case TooltipPlacement.TopRight:
                    return new Vector2(target.xMax - cardWidth + offset.x, target.yMin - cardHeight - Mathf.Abs(offset.y));
                case TooltipPlacement.BottomLeft:
                    return new Vector2(target.xMin + offset.x, target.yMax + Mathf.Abs(offset.y));
                case TooltipPlacement.BottomRight:
                    return new Vector2(target.xMax - cardWidth + offset.x, target.yMax + Mathf.Abs(offset.y));
                default:
                    return lastPanelPosition + offset;
            }
        }

        private Vector2 ScreenToPanel(Vector2 screenPosition)
        {
            // uGUI pointer/screen positions use a bottom-left origin.
            // UI Toolkit panel positions use a top-left origin.
            Vector2 topLeftScreenPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);

            if (root != null && root.panel != null)
                return RuntimePanelUtils.ScreenToPanel(root.panel, topLeftScreenPosition);

            return topLeftScreenPosition;
        }

        private Rect ScreenRectToPanelRect(Rect screenRect)
        {
            Vector2 a = ScreenToPanel(new Vector2(screenRect.xMin, screenRect.yMin));
            Vector2 b = ScreenToPanel(new Vector2(screenRect.xMax, screenRect.yMax));
            float xMin = Mathf.Min(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            float xMax = Mathf.Max(a.x, b.x);
            float yMax = Mathf.Max(a.y, b.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private bool OwnsTooltip(object owner)
        {
            return owner == null || currentOwner == null || ReferenceEquals(owner, currentOwner);
        }
    }
}
