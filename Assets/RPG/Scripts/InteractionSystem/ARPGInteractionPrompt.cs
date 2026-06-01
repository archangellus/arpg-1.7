using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Small uGUI prompt presenter used by <see cref="ARPGInteractionManager"/>.
    /// Assign existing UI references or let the component build a simple screen-space prompt at runtime.
    /// </summary>
    [AddComponentMenu("PLAYER TWO/ARPG Project/Interaction/Interaction Prompt")]
    public class ARPGInteractionPrompt : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Optional root object to toggle when a prompt is shown or hidden.")]
        public GameObject root;

        [Tooltip("Text element used for the prompt message.")]
        public Text messageText;

        [Tooltip("Optional image element used for object- or manager-level prompt icons.")]
        public Image iconImage;

        [Header("Fallback UI")]
        [Tooltip("Create a simple prompt canvas automatically when no message text is assigned.")]
        public bool createFallbackUI = true;

        [Tooltip("Anchored position used by the generated fallback prompt.")]
        public Vector2 fallbackAnchoredPosition = new(0f, 160f);

        protected virtual void Awake()
        {
            if (!messageText && createFallbackUI)
                CreateFallbackUI();

            Hide();
        }

        public virtual void Show(string message, Sprite icon, Color textColor, int textSize, Vector2 iconSize)
        {
            if (!messageText && createFallbackUI)
                CreateFallbackUI();

            if (messageText)
            {
                messageText.text = message;
                messageText.color = textColor;
                messageText.fontSize = textSize;
            }

            if (iconImage)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;

                if (iconImage.rectTransform)
                    iconImage.rectTransform.sizeDelta = iconSize;
            }

            SetRootActive(true);
        }

        public virtual void Hide()
        {
            SetRootActive(false);
        }

        protected virtual void SetRootActive(bool value)
        {
            var target = root;

            if (!target && messageText)
                target = messageText.gameObject;

            if (!target)
                return;

            if (target.activeSelf != value)
                target.SetActive(value);
        }

        protected virtual void CreateFallbackUI()
        {
            var canvasObject = new GameObject("Interaction Prompt Canvas");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            var panelObject = new GameObject("Interaction Prompt");
            panelObject.transform.SetParent(canvasObject.transform, false);
            root = panelObject;

            var panel = panelObject.AddComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = fallbackAnchoredPosition;
            panel.sizeDelta = new Vector2(420f, 72f);

            var background = panelObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);

            var iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(panelObject.transform, false);
            iconImage = iconObject.AddComponent<Image>();
            iconImage.enabled = false;

            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(18f, 0f);
            iconRect.sizeDelta = new Vector2(40f, 40f);

            var textObject = new GameObject("Message");
            textObject.transform.SetParent(panelObject.transform, false);
            messageText = textObject.AddComponent<Text>();
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.raycastTarget = false;
            messageText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            messageText.supportRichText = true;

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-16f, 0f);
        }
    }
}
