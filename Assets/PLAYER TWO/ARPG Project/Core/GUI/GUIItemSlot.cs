using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Item Slot")]
    public abstract class GUIItemSlot
        : GUISlot,
            IPointerEnterHandler,
            IPointerExitHandler,
            IPointerDownHandler,
            IDeselectHandler,
            IDragHandler,
            IEndDragHandler,
            IDropHandler
    {
        [Header("Color Settings")]
        [Tooltip("The color of the slot when hovering a valid GUI Item for this slot.")]
        public Color valid = GameColors.LightBlue;

        [Tooltip("The color of the slot when hovering a invalid GUI Item for this slot.")]
        public Color invalid = GameColors.LightRed;

        [Header("Slot Events")]
        public UnityEvent<GUIItem> onEquip;
        public UnityEvent<GUIItem> onUnequip;

        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when equipping an item.")]
        public AudioClip equipClip;

        [Tooltip("The Audio Clip that plays when unequipping an item.")]
        public AudioClip unequipClip;

        protected bool m_hovering;
        protected bool m_inspecting;
        protected float m_initializationTime;
        protected float m_lastClickTime;

        protected Image m_image;
        protected Color m_initialColor;
        protected GUIItem m_tempItem;

        protected const float k_doubleClickThreshold = 0.3f;

        /// <summary>
        /// Returns the current equipped GUI Item on this slot.
        /// </summary>
        public GUIItem item { get; protected set; }

        protected GameAudio m_audio => GameAudio.instance;

        /// <summary>
        /// Equips a GUI Item to this slot.
        /// </summary>
        /// <param name="item">The GUI Item you want to equip.</param>
        public virtual void Equip(GUIItem item)
        {
            this.item = item;
            this.item.interactable = false;
            this.item.transform.position = transform.position;
            this.item.transform.SetParent(transform);
            m_image.color = m_initialColor;
            PlayAudio(equipClip);
            onEquip.Invoke(item);
        }

        /// <summary>
        /// Unequips the current GUI Item from this slot.
        /// </summary>
        public virtual void Unequip()
        {
            m_tempItem = item;
            item.SetLastPosition(this);
            item = null;
            PlayAudio(unequipClip);
            onUnequip.Invoke(m_tempItem);
        }

        /// <summary>
        /// Returns true if this slot can stack a given GUI Item.
        /// </summary>
        /// <param name="other">The GUI Item you want to stack.</param>
        public virtual bool CanStack(GUIItem other) => item && item.CanStack(other);

        /// <summary>
        /// Tries to stack a given GUI Item to this slot.
        /// </summary>
        /// <param name="other">The GUI Item you want to stack.</param>
        /// <returns>Return true if the item was stacked.</returns>
        public virtual bool TryStack(GUIItem other) => item && item.TryStack(other);

        /// <summary>
        /// Returns true if it successfully equips or stacks the current selected GUI Item.
        /// </summary>
        public virtual bool TryEquipOrStackSelectedItem()
        {
            if (TryStack(GUI.instance.selected))
            {
                PlayAudio(unequipClip);
                onEquip.Invoke(item);
                GUI.instance.ClearSelection();
                return true;
            }
            else if (CanEquip(GUI.instance.selected))
            {
                var item = GUI.instance.selected;
                GUI.instance.Deselect();
                Equip(item);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if this slot can equip a given GUI Item.
        /// </summary>
        /// <param name="item">The GUI Item you want to equip.</param>
        public abstract bool CanEquip(GUIItem item);

        /// <summary>
        /// Returns true if this slot can unequip its current equipped GUI Item.
        /// </summary>
        public abstract bool CanUnequip();

        protected virtual void HandleLeftClick()
        {
            if (GUI.instance.selected)
            {
                if (!TryEquipOrStackSelectedItem())
                    GameAudio.instance.PlayDeniedSound();
            }
            else if (item && CanUnequip())
            {
                GUI.instance.Select(item);
                Unequip();
            }
        }

        protected virtual void PlayAudio(AudioClip audio)
        {
            if (m_audio && Time.time > m_initializationTime)
                m_audio.PlayUiEffect(audio);
        }

        protected virtual void HandleRightClick() { }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_hovering = true;
#if UNITY_STANDALONE || UNITY_WEBGL
            m_inspecting = true;
#endif
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_hovering = false;
#if UNITY_STANDALONE || UNITY_WEBGL
            m_inspecting = true;
            GUIItemInspector.instance.Hide();
#endif
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
#if UNITY_STANDALONE || UNITY_WEBGL
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    HandleLeftClick();
                    break;
                case PointerEventData.InputButton.Right:
                    HandleRightClick();
                    break;
            }
#else
            if (Time.time - m_lastClickTime < k_doubleClickThreshold)
            {
                HandleRightClick();
            }
            else
            {
                GUIItemInspector.instance.Hide();
                GUIItemInspector.instance.Show(item);
                GUIItemInspector.instance.SetPositionRelativeTo((RectTransform)transform);
                EventSystem.current.SetSelectedGameObject(gameObject);
            }

            m_inspecting = true;
            m_lastClickTime = Time.time;
#endif
        }

        public virtual void OnDeselect(BaseEventData _)
        {
            m_hovering = false;
            GUIItemInspector.instance.Hide();
        }

        public virtual void OnDrag(PointerEventData _)
        {
            if (!GUI.instance.selected && item && CanUnequip())
            {
                GUI.instance.Select(item);
                Unequip();
            }
        }

        public virtual void OnEndDrag(PointerEventData _) => GUI.instance.DropItem();

        public virtual void OnDrop(PointerEventData _)
        {
            if (m_hovering && GUI.instance.selected)
            {
                if (!TryEquipOrStackSelectedItem())
                    GameAudio.instance.PlayDeniedSound();
            }

            m_hovering = false;
        }

        protected override void Awake()
        {
            base.Awake();
            m_image = GetComponent<Image>();
            m_initialColor = m_image.color;
            m_initializationTime = Time.time;
        }

        protected virtual void Update()
        {
            if (!m_hovering)
            {
                if (m_image.color != m_initialColor)
                    m_image.color = m_initialColor;

                return;
            }
            if (GUI.instance.selected)
            {
                m_image.color = invalid;

                if (CanEquip(GUI.instance.selected) || CanStack(GUI.instance.selected))
                    m_image.color = valid;
            }
#if UNITY_STANDALONE || UNITY_WEBGL
            else if (item)
                GUIItemInspector.instance.Show(item);
            else
                GUIItemInspector.instance.Hide();
#endif
        }

        protected virtual void OnDisable()
        {
            if (m_inspecting)
            {
                m_inspecting = false;
#if UNITY_STANDALONE || UNITY_WEBGL
                m_hovering = false;
#endif
                GUIItemInspector.instance.Hide();
            }
        }
    }
}
