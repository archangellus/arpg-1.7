// RuntimeTooltipSystem.cs
// Unity 6000.3+ | UI Toolkit tooltip renderer + uGUI and UI Toolkit bindings
// Drop this file anywhere under Assets/. Editor-only menu helpers are included at the bottom.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RuntimeTooltips
{
    [CreateAssetMenu(menuName = "UI/Runtime Tooltip Style", fileName = "RuntimeTooltipStyle")]
    public sealed class RuntimeTooltipStyle : ScriptableObject
    {
        [Header("Panel")]
        public Color backgroundColor = new(0.035f, 0.04f, 0.05f, 0.96f);
        public Color borderColor = new(1f, 1f, 1f, 0.16f);
        [Min(0f)] public float borderWidth = 1f;
        [Min(0f)] public float cornerRadius = 8f;
        [Min(0f)] public float padding = 10f;
        [Min(80f)] public float maxWidth = 360f;

        [Header("Text")]
        public Color titleColor = Color.white;
        public Color bodyColor = new(0.86f, 0.88f, 0.92f, 1f);
        [Min(1)] public int titleSize = 14;
        [Min(1)] public int bodySize = 12;
        public FontStyle titleStyle = FontStyle.Bold;

        [Header("Motion")]
        [Min(0f)] public float edgePadding = 8f;
        public Vector2 defaultOffset = new(16f, 18f);
    }

    [Serializable]
    public sealed class TooltipInfo
    {
        public string title;
        [TextArea(2, 8)] public string body;

        [Header("Behavior")]
        [Min(0f)] public float delay = 0.2f;
        public bool followPointer = true;
        public bool richText = true;
        public Vector2 offset = new(16f, 18f);
        [Min(80f)] public float maxWidth = 360f;

        public bool HasText => !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(body);

        public static TooltipInfo Create(string title, string body, float delay = 0.2f, bool followPointer = true)
        {
            return new TooltipInfo
            {
                title = title,
                body = body,
                delay = delay,
                followPointer = followPointer
            };
        }

        public TooltipInfo Copy(bool? followPointerOverride = null)
        {
            return new TooltipInfo
            {
                title = title,
                body = body,
                delay = delay,
                followPointer = followPointerOverride ?? followPointer,
                richText = richText,
                offset = offset,
                maxWidth = maxWidth
            };
        }
    }

    /// <summary>
    /// Add this to a UIDocument GameObject. It renders one reusable tooltip VisualElement over the whole screen.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class RuntimeTooltipManager : MonoBehaviour
    {
        public static RuntimeTooltipManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindFirstObjectByType<RuntimeTooltipManager>();
                return _instance;
            }
        }

        private static RuntimeTooltipManager _instance;

        [Header("Optional Style Asset")]
        [SerializeField] private RuntimeTooltipStyle style;

        [Header("Fallback Style When No Asset Is Assigned")]
        [SerializeField] private Color fallbackBackgroundColor = new(0.035f, 0.04f, 0.05f, 0.96f);
        [SerializeField] private Color fallbackBorderColor = new(1f, 1f, 1f, 0.16f);
        [SerializeField] private Color fallbackTitleColor = Color.white;
        [SerializeField] private Color fallbackBodyColor = new(0.86f, 0.88f, 0.92f, 1f);
        [SerializeField, Min(0f)] private float fallbackBorderWidth = 1f;
        [SerializeField, Min(0f)] private float fallbackCornerRadius = 8f;
        [SerializeField, Min(0f)] private float fallbackPadding = 10f;
        [SerializeField, Min(1)] private int fallbackTitleSize = 14;
        [SerializeField, Min(1)] private int fallbackBodySize = 12;
        [SerializeField, Min(0f)] private float fallbackEdgePadding = 8f;
        [SerializeField] private Vector2 fallbackOffset = new(16f, 18f);

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _tooltip;
        private Label _titleLabel;
        private Label _bodyLabel;

        private bool _hovering;
        private bool _visible;
        private float _showTime;
        private TooltipInfo _pendingInfo;
        private Vector2 _lastPanelPosition;

        private readonly Dictionary<VisualElement, RegisteredVisualElementTooltip> _registeredVisualElements = new();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"Multiple {nameof(RuntimeTooltipManager)} instances found. Keeping the first one: {_instance.name}", this);
                return;
            }

            _instance = this;
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            _document ??= GetComponent<UIDocument>();
            BuildTooltipIfNeeded();
            HideImmediate();
        }

        private void OnDisable()
        {
            HideImmediate();
        }

        private void Update()
        {
            if (!_hovering) return;

            if (_pendingInfo != null && _pendingInfo.followPointer)
                _lastPanelPosition = ScreenToPanel(GetCurrentPointerScreenPosition());

            if (!_visible && Time.unscaledTime >= _showTime)
                ShowNow();

            if (_visible)
                PositionTooltip(_lastPanelPosition);
        }

        /// <summary>Show a tooltip using a UI Toolkit panel-space point.</summary>
        public void BeginHover(TooltipInfo info, Vector2 panelPosition)
        {
            if (info == null || !info.HasText) return;

            BuildTooltipIfNeeded();
            _pendingInfo = info;
            _lastPanelPosition = panelPosition;
            _showTime = Time.unscaledTime + Mathf.Max(0f, info.delay);
            _hovering = true;
            _visible = false;

            if (_tooltip != null)
                _tooltip.style.display = DisplayStyle.None;
        }

        /// <summary>Show a tooltip using a regular screen-space point, such as PointerEventData.position.</summary>
        public void BeginHoverFromScreen(TooltipInfo info, Vector2 screenPosition)
        {
            BeginHover(info, ScreenToPanel(screenPosition));
        }

        /// <summary>Immediately show a tooltip at a screen-space point. Useful for API-triggered help popups.</summary>
        public void ShowAtScreen(string title, string body, Vector2 screenPosition)
        {
            var info = TooltipInfo.Create(title, body, 0f, false);
            BeginHoverFromScreen(info, screenPosition);
            ShowNow();
        }

        /// <summary>Immediately show a tooltip at the center of a RectTransform.</summary>
        public void ShowForRectTransform(RectTransform rectTransform, TooltipInfo info, Camera eventCamera = null, bool followPointer = false)
        {
            if (rectTransform == null || info == null) return;
            var worldCenter = rectTransform.TransformPoint(rectTransform.rect.center);
            BeginHoverFromScreen(info.Copy(followPointer), RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter));
        }

        public void UpdateHoverPosition(Vector2 panelPosition)
        {
            _lastPanelPosition = panelPosition;
            if (_visible)
                PositionTooltip(_lastPanelPosition);
        }

        public void UpdateHoverScreenPosition(Vector2 screenPosition)
        {
            UpdateHoverPosition(ScreenToPanel(screenPosition));
        }

        public void EndHover()
        {
            _hovering = false;
            _pendingInfo = null;
            HideImmediate();
        }

        public void Hide()
        {
            EndHover();
        }

        /// <summary>
        /// API for UI Toolkit: call this once for any VisualElement. Designers can do the same from TooltipUIDocumentBinder.
        /// </summary>
        public void RegisterVisualElement(VisualElement element, TooltipInfo info)
        {
            if (element == null || info == null) return;
            UnregisterVisualElement(element);

            var registered = new RegisteredVisualElementTooltip
            {
                info = info,
                enter = evt => BeginHover(info, evt.position),
                move = evt => UpdateHoverPosition(evt.position),
                leave = _ => EndHover(),
                focusIn = _ => BeginHover(info.Copy(false), element.worldBound.center),
                focusOut = _ => EndHover()
            };

            element.RegisterCallback(registered.enter);
            element.RegisterCallback(registered.move);
            element.RegisterCallback(registered.leave);
            element.RegisterCallback(registered.focusIn);
            element.RegisterCallback(registered.focusOut);

            _registeredVisualElements[element] = registered;
        }

        public void UnregisterVisualElement(VisualElement element)
        {
            if (element == null) return;
            if (!_registeredVisualElements.TryGetValue(element, out var registered)) return;

            element.UnregisterCallback(registered.enter);
            element.UnregisterCallback(registered.move);
            element.UnregisterCallback(registered.leave);
            element.UnregisterCallback(registered.focusIn);
            element.UnregisterCallback(registered.focusOut);
            _registeredVisualElements.Remove(element);
        }

        private void BuildTooltipIfNeeded()
        {
            if (_document == null) return;
            var root = _document.rootVisualElement;
            if (root == null) return;

            if (_root == root && _tooltip != null)
            {
                ApplyStyle();
                return;
            }

            _root = root;

            _tooltip = new VisualElement
            {
                name = "RuntimeTooltip",
                pickingMode = PickingMode.Ignore,
                usageHints = UsageHints.DynamicTransform
            };

            _titleLabel = new Label { name = "RuntimeTooltipTitle", pickingMode = PickingMode.Ignore };
            _bodyLabel = new Label { name = "RuntimeTooltipBody", pickingMode = PickingMode.Ignore };

            _tooltip.Add(_titleLabel);
            _tooltip.Add(_bodyLabel);
            _root.Add(_tooltip);
            _tooltip.BringToFront();

            ApplyStyle();
        }

        private void ApplyStyle()
        {
            if (_tooltip == null || _titleLabel == null || _bodyLabel == null) return;

            var padding = style != null ? style.padding : fallbackPadding;
            var borderWidth = style != null ? style.borderWidth : fallbackBorderWidth;
            var cornerRadius = style != null ? style.cornerRadius : fallbackCornerRadius;
            var maxWidth = style != null ? style.maxWidth : 360f;

            _tooltip.style.position = Position.Absolute;
            _tooltip.style.left = 0f;
            _tooltip.style.top = 0f;
            _tooltip.style.display = DisplayStyle.None;
            _tooltip.style.maxWidth = maxWidth;
            _tooltip.style.paddingLeft = padding;
            _tooltip.style.paddingRight = padding;
            _tooltip.style.paddingTop = padding;
            _tooltip.style.paddingBottom = padding;
            _tooltip.style.backgroundColor = style != null ? style.backgroundColor : fallbackBackgroundColor;
            _tooltip.style.borderTopColor = style != null ? style.borderColor : fallbackBorderColor;
            _tooltip.style.borderRightColor = style != null ? style.borderColor : fallbackBorderColor;
            _tooltip.style.borderBottomColor = style != null ? style.borderColor : fallbackBorderColor;
            _tooltip.style.borderLeftColor = style != null ? style.borderColor : fallbackBorderColor;
            _tooltip.style.borderTopWidth = borderWidth;
            _tooltip.style.borderRightWidth = borderWidth;
            _tooltip.style.borderBottomWidth = borderWidth;
            _tooltip.style.borderLeftWidth = borderWidth;
            _tooltip.style.borderTopLeftRadius = cornerRadius;
            _tooltip.style.borderTopRightRadius = cornerRadius;
            _tooltip.style.borderBottomRightRadius = cornerRadius;
            _tooltip.style.borderBottomLeftRadius = cornerRadius;

            _titleLabel.style.color = style != null ? style.titleColor : fallbackTitleColor;
            _titleLabel.style.fontSize = style != null ? style.titleSize : fallbackTitleSize;
            _titleLabel.style.unityFontStyleAndWeight = style != null ? style.titleStyle : FontStyle.Bold;
            _titleLabel.style.marginBottom = 4f;
            _titleLabel.style.whiteSpace = WhiteSpace.Normal;

            _bodyLabel.style.color = style != null ? style.bodyColor : fallbackBodyColor;
            _bodyLabel.style.fontSize = style != null ? style.bodySize : fallbackBodySize;
            _bodyLabel.style.whiteSpace = WhiteSpace.Normal;
        }

        private void ShowNow()
        {
            if (_tooltip == null || _pendingInfo == null || !_pendingInfo.HasText) return;

            _titleLabel.enableRichText = _pendingInfo.richText;
            _bodyLabel.enableRichText = _pendingInfo.richText;
            _titleLabel.text = _pendingInfo.title ?? string.Empty;
            _bodyLabel.text = _pendingInfo.body ?? string.Empty;

            _titleLabel.style.display = string.IsNullOrWhiteSpace(_pendingInfo.title) ? DisplayStyle.None : DisplayStyle.Flex;
            _bodyLabel.style.display = string.IsNullOrWhiteSpace(_pendingInfo.body) ? DisplayStyle.None : DisplayStyle.Flex;

            var maxWidth = _pendingInfo.maxWidth > 0f
                ? _pendingInfo.maxWidth
                : style != null ? style.maxWidth : 360f;

            _tooltip.style.maxWidth = maxWidth;
            _tooltip.style.display = DisplayStyle.Flex;
            _tooltip.BringToFront();
            _visible = true;

            PositionTooltip(_lastPanelPosition);
        }

        private void HideImmediate()
        {
            _visible = false;
            if (_tooltip != null)
                _tooltip.style.display = DisplayStyle.None;
        }

        private void PositionTooltip(Vector2 panelPosition)
        {
            if (_tooltip == null || _root == null) return;

            var infoOffset = _pendingInfo != null ? _pendingInfo.offset : Vector2.zero;
            if (infoOffset == Vector2.zero)
                infoOffset = style != null ? style.defaultOffset : fallbackOffset;

            var rootSize = _root.layout.size;
            if (rootSize.x <= 0f || rootSize.y <= 0f)
                return;

            var width = _tooltip.resolvedStyle.width;
            var height = _tooltip.resolvedStyle.height;

            if (float.IsNaN(width) || width <= 0f)
                width = _pendingInfo != null && _pendingInfo.maxWidth > 0f ? _pendingInfo.maxWidth : style != null ? style.maxWidth : 360f;
            if (float.IsNaN(height) || height <= 0f)
                height = 80f;

            var edgePadding = style != null ? style.edgePadding : fallbackEdgePadding;
            var x = panelPosition.x + infoOffset.x;
            var y = panelPosition.y + infoOffset.y;

            x = Mathf.Clamp(x, edgePadding, Mathf.Max(edgePadding, rootSize.x - width - edgePadding));
            y = Mathf.Clamp(y, edgePadding, Mathf.Max(edgePadding, rootSize.y - height - edgePadding));

            _tooltip.style.translate = new Translate(x, y);
        }

        private Vector2 ScreenToPanel(Vector2 screenPosition)
        {
            BuildTooltipIfNeeded();

            if (_root != null && _root.panel != null)
                return RuntimePanelUtils.ScreenToPanel(_root.panel, screenPosition);

            return new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        }

        private static Vector2 GetCurrentPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return Touchscreen.current.primaryTouch.position.ReadValue();
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#endif
        }

        private sealed class RegisteredVisualElementTooltip
        {
            public TooltipInfo info;
            public EventCallback<PointerEnterEvent> enter;
            public EventCallback<PointerMoveEvent> move;
            public EventCallback<PointerLeaveEvent> leave;
            public EventCallback<FocusInEvent> focusIn;
            public EventCallback<FocusOutEvent> focusOut;
        }
    }

    /// <summary>
    /// Designer-friendly uGUI target. Add to any Button/Image/TMPUGUI/etc. GameObject with a RectTransform.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private TooltipInfo tooltip = new();
        [SerializeField] private bool showOnPointer = true;
        [SerializeField] private bool showOnKeyboardOrGamepadFocus = true;

        public TooltipInfo Tooltip => tooltip;

        public void SetTooltip(string title, string body)
        {
            tooltip ??= new TooltipInfo();
            tooltip.title = title;
            tooltip.body = body;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!showOnPointer) return;
            RuntimeTooltipManager.Instance?.BeginHoverFromScreen(tooltip, eventData.position);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!showOnPointer) return;
            RuntimeTooltipManager.Instance?.UpdateHoverScreenPosition(eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            RuntimeTooltipManager.Instance?.EndHover();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!showOnKeyboardOrGamepadFocus) return;
            var manager = RuntimeTooltipManager.Instance;
            if (manager == null) return;

            var rectTransform = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = null;

            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCamera = canvas.worldCamera;

            manager.ShowForRectTransform(rectTransform, tooltip, eventCamera, false);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            RuntimeTooltipManager.Instance?.EndHover();
        }

        private void OnDisable()
        {
            RuntimeTooltipManager.Instance?.EndHover();
        }
    }

    [Serializable]
    public sealed class UITooltipBinding
    {
        [Tooltip("The UI Toolkit VisualElement name set in UI Builder, for example: InventoryButton.")]
        public string elementName;
        public TooltipInfo tooltip = new();
    }

    /// <summary>
    /// Designer-friendly UI Toolkit binder. Add to a UIDocument and map VisualElement names to TooltipInfo entries.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class TooltipUIDocumentBinder : MonoBehaviour
    {
        [SerializeField] private RuntimeTooltipManager manager;
        [SerializeField] private List<UITooltipBinding> bindings = new();

        private UIDocument _document;
        private readonly List<VisualElement> _boundElements = new();

        private void OnEnable()
        {
            Bind();
        }

        private void Start()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        [ContextMenu("Rebind Tooltip Elements")]
        public void Bind()
        {
            Unbind();

            _document ??= GetComponent<UIDocument>();
            manager ??= RuntimeTooltipManager.Instance;

            if (_document == null || _document.rootVisualElement == null || manager == null)
                return;

            foreach (var binding in bindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.elementName) || binding.tooltip == null || !binding.tooltip.HasText)
                    continue;

                var element = _document.rootVisualElement.Q<VisualElement>(binding.elementName);
                if (element == null)
                {
                    Debug.LogWarning($"Tooltip binding on {name} could not find VisualElement named '{binding.elementName}'.", this);
                    continue;
                }

                manager.RegisterVisualElement(element, binding.tooltip);
                _boundElements.Add(element);
            }
        }

        public void Unbind()
        {
            var activeManager = manager != null ? manager : RuntimeTooltipManager.Instance;
            if (activeManager != null)
            {
                foreach (var element in _boundElements)
                    activeManager.UnregisterVisualElement(element);
            }

            _boundElements.Clear();
        }
    }

#if UNITY_EDITOR
    public static class RuntimeTooltipEditorMenu
    {
        [MenuItem("Tools/Runtime Tooltips/Create Tooltip Manager", priority = 0)]
        public static void CreateTooltipManager()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<RuntimeTooltipManager>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            var panelSettingsPath = AssetDatabase.GenerateUniqueAssetPath("Assets/RuntimeTooltipPanelSettings.asset");
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(panelSettings, panelSettingsPath);
            AssetDatabase.SaveAssets();

            var go = new GameObject("Runtime Tooltip Manager");
            Undo.RegisterCreatedObjectUndo(go, "Create Runtime Tooltip Manager");

            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = short.MaxValue;
            go.AddComponent<RuntimeTooltipManager>();

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        [MenuItem("Tools/Runtime Tooltips/Add Tooltip Target To Selected uGUI Elements", priority = 10)]
        public static void AddTooltipTargetToSelectedUGUIElements()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go == null || go.GetComponent<RectTransform>() == null)
                    continue;

                if (go.GetComponent<TooltipTarget>() == null)
                    Undo.AddComponent<TooltipTarget>(go);
            }
        }

        [MenuItem("Tools/Runtime Tooltips/Add Tooltip Target To Selected uGUI Elements", true)]
        public static bool AddTooltipTargetToSelectedUGUIElementsValidate()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go != null && go.GetComponent<RectTransform>() != null)
                    return true;
            }

            return false;
        }

        [MenuItem("Tools/Runtime Tooltips/Add UI Toolkit Binder To Selected UIDocument", priority = 11)]
        public static void AddUIToolkitBinderToSelectedUIDocument()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go == null || go.GetComponent<UIDocument>() == null)
                    continue;

                if (go.GetComponent<TooltipUIDocumentBinder>() == null)
                    Undo.AddComponent<TooltipUIDocumentBinder>(go);
            }
        }

        [MenuItem("Tools/Runtime Tooltips/Add UI Toolkit Binder To Selected UIDocument", true)]
        public static bool AddUIToolkitBinderToSelectedUIDocumentValidate()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go != null && go.GetComponent<UIDocument>() != null)
                    return true;
            }

            return false;
        }
    }
#endif
}
