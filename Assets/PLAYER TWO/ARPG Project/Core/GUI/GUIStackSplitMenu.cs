using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Stack Split Menu")]
    public class GUIStackSplitMenu : MonoBehaviour
    {
        [Header("Stack Split Controls")]
        public Button decreaseButton;
        public Button increaseButton;
        public Button confirmButton;
        public TMP_Text amountText;

        protected ItemInstance m_source;
        protected ItemInstance m_destination;
        protected int m_movedAmount;
        protected int m_minMovedAmount = 1;
        protected int m_maxMovedAmount = 1;

        public static GUIStackSplitMenu Create(Transform parent)
        {
            var root = new GameObject("Stack Split Menu", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);

            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(260f, 120f);
            rect.anchoredPosition = Vector2.zero;

            var background = root.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.85f);

            var menu = root.AddComponent<GUIStackSplitMenu>();
            menu.BuildControls(rect);
            root.SetActive(false);
            return menu;
        }

        public virtual void Show(
            ItemInstance source,
            ItemInstance destination,
            int movedAmount,
            int destinationInitialStack
        )
        {
            m_source = source;
            m_destination = destination;
            m_movedAmount = Mathf.Max(1, movedAmount);

            var originalSourceStack = source.stack + m_movedAmount;
            var availableDestinationSpace = destination.data.stackCapacity - destinationInitialStack;
            m_maxMovedAmount = Mathf.Max(
                m_minMovedAmount,
                Mathf.Min(originalSourceStack - 1, availableDestinationSpace)
            );

            gameObject.SetActive(true);
            UpdateControls();
        }

        public virtual void Increase()
        {
            if (!CanIncrease())
                return;

            m_source.stack -= 1;
            m_destination.stack += 1;
            m_movedAmount += 1;
            UpdateControls();
        }

        public virtual void Decrease()
        {
            if (!CanDecrease())
                return;

            m_source.stack += 1;
            m_destination.stack -= 1;
            m_movedAmount -= 1;
            UpdateControls();
        }

        public virtual void Confirm()
        {
            gameObject.SetActive(false);
            m_source = null;
            m_destination = null;
        }

        protected virtual bool CanIncrease()
        {
            return m_source != null
                && m_destination != null
                && m_movedAmount < m_maxMovedAmount
                && m_source.stack > 1
                && m_destination.stack < m_destination.data.stackCapacity;
        }

        protected virtual bool CanDecrease()
        {
            return m_source != null && m_destination != null && m_movedAmount > m_minMovedAmount;
        }

        protected virtual void UpdateControls()
        {
            if (amountText)
                amountText.text = m_movedAmount.ToString();

            if (increaseButton)
                increaseButton.interactable = CanIncrease();

            if (decreaseButton)
                decreaseButton.interactable = CanDecrease();

            if (confirmButton)
                confirmButton.interactable = m_source != null && m_destination != null;
        }

        protected virtual void BuildControls(RectTransform root)
        {
            amountText = CreateText("Amount", root, new Vector2(0f, 25f), "1", 32f);
            decreaseButton = CreateButton("Decrease", root, new Vector2(-80f, -20f), "-");
            increaseButton = CreateButton("Increase", root, new Vector2(0f, -20f), "+");
            confirmButton = CreateButton("Confirm", root, new Vector2(90f, -20f), "Confirm");

            decreaseButton.onClick.AddListener(Decrease);
            increaseButton.onClick.AddListener(Increase);
            confirmButton.onClick.AddListener(Confirm);
        }

        protected virtual Button CreateButton(
            string name,
            RectTransform parent,
            Vector2 anchoredPosition,
            string label
        )
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = (RectTransform)buttonObject.transform;
            rect.sizeDelta = label.Length > 2 ? new Vector2(86f, 36f) : new Vector2(64f, 36f);
            rect.anchoredPosition = anchoredPosition;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.75f);
            button.colors = colors;

            CreateText("Label", rect, Vector2.zero, label, 22f);
            return button;
        }

        protected virtual TMP_Text CreateText(
            string name,
            RectTransform parent,
            Vector2 anchoredPosition,
            string text,
            float fontSize
        )
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            var rect = (RectTransform)textObject.transform;
            rect.sizeDelta = new Vector2(120f, 34f);
            rect.anchoredPosition = anchoredPosition;

            var tmp = textObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }
    }
}
