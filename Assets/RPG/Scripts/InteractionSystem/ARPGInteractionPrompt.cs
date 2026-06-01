using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Small uGUI prompt presenter used by <see cref="ARPGInteractionManager"/>.
    /// Assign existing UI references or let the component build a simple screen-space prompt at runtime.
    /// All visual controls are applied while the prompt is shown so Play Mode inspector changes update live.
    /// </summary>
    [AddComponentMenu("ANSTUDIO/Interaction System/Interaction Prompt")]
    public class ARPGInteractionPrompt : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Optional root object to toggle when a prompt is shown or hidden. Assign the top object of your custom prompt UI here.")]
        public GameObject root;

        [Tooltip("Optional RectTransform to move and resize when tracking an interactable. Assign this for custom UI prefabs where Root is not the movable panel.")]
        public RectTransform promptRect;

        [Tooltip("Optional background image to recolor at runtime. For fallback UI this is created automatically.")]
        public Image backgroundImage;

        [Tooltip("TextMeshPro element used for the prompt message. Preferred for new UI.")]
        public TMP_Text tmpMessageText;

        [Tooltip("Legacy uGUI Text element used for the prompt message. Used when TextMeshPro is not assigned.")]
        public Text messageText;

        [Tooltip("Optional image element used for object- or manager-level prompt icons.")]
        public Image iconImage;

        [Tooltip("When true, missing Root, Prompt Rect, Background, Icon, and text references are searched from Root and from this object during Awake and while showing.")]
        public bool autoFindUIReferences = true;

        [Header("Runtime Content Updates")]
        [Tooltip("Update the assigned text component's text value when the prompt is shown.")]
        public bool updateMessage = true;

        [Tooltip("Update the assigned text component's color when the prompt is shown.")]
        public bool updateTextColor = true;

        [Tooltip("Update the assigned text component's font size when the prompt is shown.")]
        public bool updateTextSize = true;

        [Tooltip("Update the assigned icon image and size when the prompt is shown.")]
        public bool updateIcon = true;

        [Header("Runtime Layout Updates")]
        [Tooltip("Apply Prompt Panel Size to Prompt Rect every time the prompt is shown. Disable if a custom UI uses its own layout system.")]
        public bool updatePromptRectSize = true;

        [Tooltip("Apply Text Padding to the text RectTransform every time the prompt is shown.")]
        public bool updateTextPadding = true;

        [Tooltip("Apply Background Color to Background Image every time the prompt is shown.")]
        public bool updateBackgroundColor = true;

        [Tooltip("Apply fallback/custom text controls every time the prompt is shown.")]
        public bool updateTextControls = true;

        [Header("Fallback / Runtime Panel")]
        [Tooltip("Create a simple prompt canvas automatically when no prompt text is assigned.")]
        public bool createFallbackUI = true;

        [Tooltip("Use TextMeshPro for the generated fallback message.")]
        public bool useTextMeshProFallback = true;

        [Tooltip("Anchored position used by the generated fallback prompt when no world target is supplied.")]
        public Vector2 fallbackAnchoredPosition = new(0f, 160f);

        [Tooltip("Runtime size of the prompt panel. Applied to fallback UI and to custom Prompt Rect when Update Prompt Rect Size is enabled.")]
        public Vector2 fallbackPanelSize = new(420f, 72f);

        [Tooltip("Runtime padding inside the prompt message: left, bottom, right, top.")]
        public Vector4 fallbackTextPadding = new(72f, 0f, 16f, 0f);

        [Tooltip("Runtime background color used by the prompt background image.")]
        public Color fallbackBackgroundColor = new(0f, 0f, 0f, 0.55f);

        [Tooltip("Optional font for legacy uGUI text. If empty on generated fallback UI, LegacyRuntime.ttf is used.")]
        public Font fallbackLegacyFont;

        [Tooltip("Optional font asset for TextMeshPro text. If empty on generated fallback UI, TextMeshPro's default font asset is used.")]
        public TMP_FontAsset fallbackTMPFont;

        [Header("Legacy Text Controls")]
        public TextAnchor fallbackLegacyAlignment = TextAnchor.MiddleCenter;
        public FontStyle fallbackLegacyFontStyle = FontStyle.Normal;
        public HorizontalWrapMode fallbackLegacyHorizontalOverflow = HorizontalWrapMode.Wrap;
        public VerticalWrapMode fallbackLegacyVerticalOverflow = VerticalWrapMode.Truncate;
        public bool fallbackLegacyBestFit;

        [Min(1)]
        public int fallbackLegacyBestFitMinSize = 14;

        [Min(1)]
        public int fallbackLegacyBestFitMaxSize = 28;

        [Header("TextMeshPro Controls")]
        public TextAlignmentOptions fallbackTMPAlignment = TextAlignmentOptions.Center;
        public FontStyles fallbackTMPFontStyle = FontStyles.Normal;
        public bool fallbackTMPWordWrapping = true;
        public TextOverflowModes fallbackTMPOverflowMode = TextOverflowModes.Truncate;
        public bool fallbackTMPAutoSize;

        [Min(1)]
        public int fallbackTMPAutoSizeMin = 14;

        [Min(1)]
        public int fallbackTMPAutoSizeMax = 28;

        [Header("World Tracking")]
        [Tooltip("Hide the prompt when the selected interactable is behind the camera.")]
        public bool hideWhenTargetBehindCamera = true;

        protected Canvas m_canvas;
        protected bool m_createdFallbackUI;

        protected virtual void Awake()
        {
            ResolveReferences();

            if (!HasTextTarget() && createFallbackUI)
                CreateFallbackUI();

            ResolveReferences();
            ApplyRuntimeVisualControls(Vector2.zero);
            Hide();
        }

        protected virtual void OnValidate()
        {
            fallbackPanelSize.x = Mathf.Max(1f, fallbackPanelSize.x);
            fallbackPanelSize.y = Mathf.Max(1f, fallbackPanelSize.y);
            fallbackLegacyBestFitMaxSize = Mathf.Max(fallbackLegacyBestFitMaxSize, fallbackLegacyBestFitMinSize);
            fallbackTMPAutoSizeMax = Mathf.Max(fallbackTMPAutoSizeMax, fallbackTMPAutoSizeMin);
        }

        public virtual void Show(string message, Sprite icon, Color textColor, int textSize, Vector2 iconSize)
        {
            Show(message, icon, textColor, textSize, iconSize, null, 0f, null);
        }

        public virtual void Show(
            string message,
            Sprite icon,
            Color textColor,
            int textSize,
            Vector2 iconSize,
            Transform worldTarget,
            float worldYOffset,
            Camera worldCamera
        )
        {
            if (!worldTarget)
            {
                ShowAtFallbackPosition(message, icon, textColor, textSize, iconSize);
                return;
            }

            ShowAtWorldPosition(
                message,
                icon,
                textColor,
                textSize,
                iconSize,
                worldTarget.position + Vector3.up * worldYOffset,
                worldCamera
            );
        }

        public virtual void ShowAtWorldPosition(
            string message,
            Sprite icon,
            Color textColor,
            int textSize,
            Vector2 iconSize,
            Vector3 worldPosition,
            Camera worldCamera
        )
        {
            EnsureReady();
            ApplyPromptContent(message, icon, textColor, textSize, iconSize);
            SetRootActive(true);
            UpdateWorldPosition(worldPosition, worldCamera);
        }

        public virtual void ShowAtFallbackPosition(
            string message,
            Sprite icon,
            Color textColor,
            int textSize,
            Vector2 iconSize
        )
        {
            EnsureReady();
            ApplyPromptContent(message, icon, textColor, textSize, iconSize);
            SetRootActive(true);

            var rect = GetPromptRect();

            if (rect)
                rect.anchoredPosition = fallbackAnchoredPosition;
        }

        public virtual void Hide()
        {
            SetRootActive(false);
        }

        protected virtual void EnsureReady()
        {
            ResolveReferences();

            if (!HasTextTarget() && createFallbackUI)
            {
                CreateFallbackUI();
                ResolveReferences();
            }
        }

        protected virtual void ResolveReferences()
        {
            if (autoFindUIReferences)
            {
                if (!tmpMessageText)
                    tmpMessageText = FindInRootOrSelf<TMP_Text>();

                if (!messageText)
                    messageText = FindInRootOrSelf<Text>();

                if (!iconImage)
                    iconImage = FindLikelyIconImage();

                if (!backgroundImage)
                    backgroundImage = FindLikelyBackgroundImage();
            }

            if (!promptRect)
            {
                if (root && !root.TryGetComponent<Canvas>(out _) && root.TryGetComponent(out RectTransform rootRect))
                    promptRect = rootRect;
                else if (backgroundImage && backgroundImage.transform is RectTransform backgroundRect && !backgroundImage.TryGetComponent<Canvas>(out _))
                    promptRect = backgroundRect;
                else if (tmpMessageText && tmpMessageText.transform.parent is RectTransform tmpParentRect)
                    promptRect = tmpParentRect;
                else if (messageText && messageText.transform.parent is RectTransform textParentRect)
                    promptRect = textParentRect;
                else if (tmpMessageText)
                    promptRect = tmpMessageText.rectTransform;
                else if (messageText)
                    promptRect = messageText.rectTransform;
                else if (iconImage)
                    promptRect = iconImage.rectTransform;
            }

            if (!root)
            {
                if (promptRect)
                    root = promptRect.gameObject;
                else if (backgroundImage)
                    root = backgroundImage.gameObject;
                else if (tmpMessageText)
                    root = tmpMessageText.gameObject;
                else if (messageText)
                    root = messageText.gameObject;
                else if (iconImage)
                    root = iconImage.gameObject;
            }

            if (!m_canvas)
            {
                var rect = GetPromptRect();

                if (rect)
                    m_canvas = rect.GetComponentInParent<Canvas>(true);
            }
        }

        protected virtual T FindInRootOrSelf<T>() where T : Component
        {
            if (root)
            {
                var rootResult = root.GetComponentInChildren<T>(true);

                if (rootResult)
                    return rootResult;
            }

            return GetComponentInChildren<T>(true);
        }

        protected virtual Image FindLikelyIconImage()
        {
            var images = GetSearchImages();

            for (int i = 0; i < images.Length; i++)
            {
                var imageName = images[i].name.ToLowerInvariant();

                if (imageName.Contains("icon") || imageName.Contains("button") || imageName.Contains("key"))
                    return images[i];
            }

            return null;
        }

        protected virtual Image FindLikelyBackgroundImage()
        {
            var images = GetSearchImages();

            for (int i = 0; i < images.Length; i++)
            {
                var image = images[i];

                if (!image || image == iconImage)
                    continue;

                var imageName = image.name.ToLowerInvariant();

                if (imageName.Contains("background") || imageName.Contains("bg") || imageName.Contains("panel"))
                    return image;
            }

            var rect = GetPromptRect();

            if (rect && rect.TryGetComponent(out Image panelImage) && panelImage != iconImage)
                return panelImage;

            return null;
        }

        protected virtual Image[] GetSearchImages()
        {
            if (root)
                return root.GetComponentsInChildren<Image>(true);

            return GetComponentsInChildren<Image>(true);
        }

        protected virtual bool HasTextTarget() => tmpMessageText || messageText;

        protected virtual void ApplyPromptContent(
            string message,
            Sprite icon,
            Color textColor,
            int textSize,
            Vector2 iconSize
        )
        {
            ApplyRuntimeVisualControls(iconSize);

            if (tmpMessageText)
            {
                if (updateMessage)
                    tmpMessageText.text = message;

                if (updateTextColor)
                    tmpMessageText.color = textColor;

                if (updateTextSize && !tmpMessageText.enableAutoSizing)
                    tmpMessageText.fontSize = textSize;
            }
            else if (messageText)
            {
                if (updateMessage)
                    messageText.text = message;

                if (updateTextColor)
                    messageText.color = textColor;

                if (updateTextSize && !messageText.resizeTextForBestFit)
                    messageText.fontSize = textSize;
            }

            if (iconImage && updateIcon)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;

                if (iconImage.rectTransform)
                    iconImage.rectTransform.sizeDelta = iconSize;
            }
        }

        protected virtual void ApplyRuntimeVisualControls(Vector2 iconSize)
        {
            if (updatePromptRectSize)
            {
                var rect = GetPromptRect();

                if (rect)
                    rect.sizeDelta = fallbackPanelSize;
            }

            if (backgroundImage && updateBackgroundColor)
                backgroundImage.color = fallbackBackgroundColor;

            if (updateTextPadding)
                ApplyTextPadding();

            if (updateTextControls)
                ApplyTextControls();

            if (iconImage && updateIcon && iconImage.rectTransform && iconSize != Vector2.zero)
                iconImage.rectTransform.sizeDelta = iconSize;
        }

        protected virtual void ApplyTextPadding()
        {
            RectTransform textRect = null;

            if (tmpMessageText)
                textRect = tmpMessageText.rectTransform;
            else if (messageText)
                textRect = messageText.rectTransform;

            if (!textRect)
                return;

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(fallbackTextPadding.x, fallbackTextPadding.y);
            textRect.offsetMax = new Vector2(-fallbackTextPadding.z, -fallbackTextPadding.w);
        }

        protected virtual void ApplyTextControls()
        {
            if (tmpMessageText)
            {
                tmpMessageText.alignment = fallbackTMPAlignment;
                tmpMessageText.fontStyle = fallbackTMPFontStyle;
                tmpMessageText.enableWordWrapping = fallbackTMPWordWrapping;
                tmpMessageText.overflowMode = fallbackTMPOverflowMode;
                tmpMessageText.enableAutoSizing = fallbackTMPAutoSize;
                tmpMessageText.fontSizeMin = fallbackTMPAutoSizeMin;
                tmpMessageText.fontSizeMax = fallbackTMPAutoSizeMax;
                tmpMessageText.raycastTarget = false;

                if (fallbackTMPFont)
                    tmpMessageText.font = fallbackTMPFont;
                else if (m_createdFallbackUI && TMP_Settings.defaultFontAsset)
                    tmpMessageText.font = TMP_Settings.defaultFontAsset;
            }

            if (messageText)
            {
                messageText.alignment = fallbackLegacyAlignment;
                messageText.fontStyle = fallbackLegacyFontStyle;
                messageText.horizontalOverflow = fallbackLegacyHorizontalOverflow;
                messageText.verticalOverflow = fallbackLegacyVerticalOverflow;
                messageText.resizeTextForBestFit = fallbackLegacyBestFit;
                messageText.resizeTextMinSize = fallbackLegacyBestFitMinSize;
                messageText.resizeTextMaxSize = fallbackLegacyBestFitMaxSize;
                messageText.raycastTarget = false;
                messageText.supportRichText = true;

                if (fallbackLegacyFont)
                    messageText.font = fallbackLegacyFont;
                else if (m_createdFallbackUI && !messageText.font)
                    messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        protected virtual void SetRootActive(bool value)
        {
            var target = root;

            if (!target && promptRect)
                target = promptRect.gameObject;

            if (!target && backgroundImage)
                target = backgroundImage.gameObject;

            if (!target && tmpMessageText)
                target = tmpMessageText.gameObject;

            if (!target && messageText)
                target = messageText.gameObject;

            if (!target)
                return;

            if (target.activeSelf != value)
                target.SetActive(value);
        }

        protected virtual RectTransform GetPromptRect()
        {
            if (promptRect)
                return promptRect;

            if (root && root.TryGetComponent(out RectTransform rootRect))
                return rootRect;

            if (backgroundImage)
                return backgroundImage.rectTransform;

            if (tmpMessageText)
                return tmpMessageText.rectTransform;

            return messageText ? messageText.rectTransform : null;
        }

        protected virtual void UpdateWorldPosition(Vector3 worldPosition, Camera worldCamera)
        {
            var rect = GetPromptRect();

            if (!rect)
                return;

            if (!m_canvas)
                m_canvas = rect.GetComponentInParent<Canvas>(true);

            if (!m_canvas || !(m_canvas.transform is RectTransform canvasRect))
                return;

            var cam = worldCamera ? worldCamera : Camera.main;

            if (!cam)
                return;

            var screenPosition = cam.WorldToScreenPoint(worldPosition);

            if (hideWhenTargetBehindCamera && screenPosition.z < 0f)
            {
                SetRootActive(false);
                return;
            }

            var uiCamera = m_canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : m_canvas.worldCamera ? m_canvas.worldCamera : cam;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPosition,
                    uiCamera,
                    out var localPoint
                ))
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = localPoint;
        }

        protected virtual void CreateFallbackUI()
        {
            if (m_createdFallbackUI)
                return;

            var canvasObject = new GameObject("Interaction Prompt Canvas");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            m_canvas = canvas;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            var panelObject = new GameObject("Interaction Prompt");
            panelObject.transform.SetParent(canvasObject.transform, false);
            root = panelObject;

            var panel = panelObject.AddComponent<RectTransform>();
            promptRect = panel;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = fallbackAnchoredPosition;
            panel.sizeDelta = fallbackPanelSize;

            backgroundImage = panelObject.AddComponent<Image>();
            backgroundImage.color = fallbackBackgroundColor;
            backgroundImage.raycastTarget = false;

            var iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(panelObject.transform, false);
            iconImage = iconObject.AddComponent<Image>();
            iconImage.enabled = false;
            iconImage.raycastTarget = false;

            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(18f, 0f);
            iconRect.sizeDelta = new Vector2(40f, 40f);

            var textObject = new GameObject("Message");
            textObject.transform.SetParent(panelObject.transform, false);

            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(fallbackTextPadding.x, fallbackTextPadding.y);
            textRect.offsetMax = new Vector2(-fallbackTextPadding.z, -fallbackTextPadding.w);

            m_createdFallbackUI = true;

            if (useTextMeshProFallback)
                CreateTMPFallbackText(textObject);
            else
                CreateLegacyFallbackText(textObject);

            ApplyRuntimeVisualControls(Vector2.zero);
        }

        protected virtual void CreateTMPFallbackText(GameObject textObject)
        {
            tmpMessageText = textObject.AddComponent<TextMeshProUGUI>();
            ApplyTextControls();
        }

        protected virtual void CreateLegacyFallbackText(GameObject textObject)
        {
            messageText = textObject.AddComponent<Text>();
            messageText.font = fallbackLegacyFont
                ? fallbackLegacyFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ApplyTextControls();
        }
    }
}
