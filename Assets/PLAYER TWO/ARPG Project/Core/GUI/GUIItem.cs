using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(CanvasGroup))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Item")]
    public class GUIItem
        : MonoBehaviour,
            IPointerEnterHandler,
            IPointerExitHandler,
            IPointerDownHandler,
            IDragHandler,
            IEndDragHandler,
            IDropHandler,
            IDeselectHandler
    {
        [Tooltip("A reference to the Text component that represents the stack size.")]
        public Text stackText;

        protected Image m_image;
        protected CanvasGroup m_group;
        protected GUIItemSlot m_lastSlot;
        protected GUIInventory m_lastInventory;

        protected bool m_hovering;
        protected bool m_selected;
        protected InventoryCell m_lastInventoryPosition;

        protected float m_lastClickTime;

        protected const float k_doubleClickThreshold = 0.3f;

        /// <summary>
        /// Returns the GUI Merchant associated to this GUI Item.
        /// </summary>
        public GUIMerchant merchant { get; set; }

        /// <summary>
        /// Returns the Item Instance that this GUI Item represents.
        /// </summary>
        public ItemInstance item { get; protected set; }

        /// <summary>
        /// Returns the Image component of this GUI Item.
        /// </summary>
        public Image image
        {
            get
            {
                if (!m_image)
                    m_image = GetComponent<Image>();

                return m_image;
            }
        }

        /// <summary>
        /// Returns the Canvas Group of this GUI Item.
        /// </summary>
        public CanvasGroup group
        {
            get
            {
                if (!m_group)
                    m_group = GetComponent<CanvasGroup>();

                return m_group;
            }
        }

        /// <summary>
        /// Returns true if this GUI Item is interactable.
        /// </summary>
        public bool interactable
        {
            get { return group.blocksRaycasts; }
            set { group.blocksRaycasts = value; }
        }

        /// <summary>
        /// Returns true if this item on a Merchant.
        /// </summary>
        public bool onMerchant => merchant;

        protected Entity player => Level.instance.player;

        /// <summary>
        /// Returns the current size of the GUI Item transform.
        /// </summary>
        public Vector2 size => ((RectTransform)transform).sizeDelta;

        protected GUIWindowsManager windowsManager => GUIWindowsManager.instance;
        protected GUIBlacksmith m_blacksmith => windowsManager.blacksmith;
        protected GUIWindow m_stash => windowsManager.stashWindow;
        protected GUIWindow m_merchant => windowsManager.merchantWindow;
        protected GUIInventory m_inventory => windowsManager.GetInventory();

        /// <summary>
        /// Selects this GUI Item.
        /// </summary>
        public virtual void Select()
        {
            group.blocksRaycasts = false;
            ((RectTransform)transform).SetAsLastSibling();
        }

        /// <summary>
        /// Deselects this GUI Item.
        /// </summary>
        public virtual void Deselect() => group.blocksRaycasts = true;

        /// <summary>
        /// Returns true if its possible to stack a given item on this one.
        /// </summary>
        /// <param name="other">The item you want to stack.</param>
        public virtual bool CanStack(GUIItem other) => item.CanStack(other.item);

        /// <summary>
        /// Tries to stack a given item on this one.
        /// </summary>
        /// <param name="other">The item you want to stack.</param>
        /// <returns>Returns true if the item was stacked.</returns>
        public virtual bool TryStack(GUIItem other) => item.TryStack(other.item);

        protected virtual void HandleLeftClick()
        {
            if (onMerchant)
                HandleBuy();
            else if (!GUI.instance.selected)
                GUI.instance.Select(this);
            else if (TryStack(GUI.instance.selected))
                GUI.instance.ClearSelection();
            else
                GameAudio.instance.PlayDeniedSound();
        }

        protected virtual void HandleRightClick()
        {
            if (onMerchant)
            {
#if UNITY_ANDROID || UNITY_IOS
                HandleBuy();
#endif
                return;
            }

            if (m_blacksmith.isOpen)
                HandleBlacksmithEquip();
            else if (m_stash.isOpen)
                HandleMoveToStash();
            else if (TryMoveFromPetToPlayer())
                return;
            else if (m_merchant.isOpen)
                HandleSell();
            else
                HandleEquip();
        }

        protected virtual void HandleBuy()
        {
            if (merchant.TrySell(this))
                merchant = null;
        }

        protected virtual void HandleSell()
        {
            var merchant = m_merchant.GetComponent<GUIMerchant>();

            if (merchant.TryBuy(this))
                this.merchant = merchant;
        }

        protected virtual void HandleBlacksmithEquip()
        {
            if (!m_blacksmith.slot.CanEquip(this))
                return;

            if (m_inventory.TryRemove(this))
                m_blacksmith.slot.Equip(this);
        }

        protected virtual void HandleEquip()
        {
            if (item.IsEquippable())
            {
                if (
                    m_inventory
                    && m_inventory.equipments.TryAutoEquip(this)
                    && m_inventory.TryRemove(this)
                )
                    return;
            }
            else if (item.IsConsumable())
            {
                var hud = GUIEntity.instance;

                if (hud && hud.TryEquipConsumable(this) && m_inventory)
                    m_inventory.TryRemove(this);
            }
            else if (item.IsSkill())
            {
                if (player.skills.TryLearnSkill(item.GetSkill()) && m_inventory.TryRemove(this))
                    Destroy(gameObject);
            }
        }

        protected virtual bool TryMoveFromPetToPlayer()
        {
            var source = GetComponentInParent<GUIPetInventory>();
            return source && source.TryMoveToPlayerInventory(this);
        }

        protected virtual void HandleMoveToStash()
        {
            var source = GetComponentInParent<GUIInventory>();

            if (source is GUIStash)
            {
                if (m_inventory.TryAutoInsert(this))
                    source.TryRemove(this);
            }
            else if (m_stash.GetComponentInChildren<GUIInventory>().TryAutoInsert(this))
                source.TryRemove(this);
        }

        /// <summary>
        /// Updates the stack size text.
        /// </summary>
        public virtual void UpdateStackText()
        {
            if (!stackText || item == null)
                return;

            stackText.enabled = item.IsStackable() && item.stack > 1;

            if (stackText.enabled)
                stackText.text = item.stack.ToString();
        }

        /// <summary>
        /// Sets the last position of this GUI Item from a given GUI Inventory.
        /// </summary>
        /// <param name="inventory">The inventory you want to set as last one.</param>
        /// <param name="position">The row and column you want to set as last one.</param>
        public virtual void SetLastPosition(GUIInventory inventory, InventoryCell position)
        {
            m_lastInventory = inventory;
            m_lastInventoryPosition = position;
            m_lastSlot = null;
        }

        public virtual bool WasRemovedFrom(GUIInventory inventory) =>
            inventory && m_lastInventory == inventory;

        /// <summary>
        /// Sets the last position of this GUI Item from a given GUI Slot.
        /// </summary>
        /// <param name="slot">The GUI Slot you want to set as last one.</param>
        public virtual void SetLastPosition(GUIItemSlot slot)
        {
            m_lastSlot = slot;
            m_lastInventory = null;
        }

        /// <summary>
        /// Tries to move this GUI Item to its last position.
        /// </summary>
        /// <returns>Returns true if successfully moved.</returns>
        public virtual bool TryMoveToLastPosition()
        {
            if (GUI.instance.selected == this)
                GUI.instance.Deselect();

            if (m_lastInventory)
            {
                return m_lastInventory.TryInsert(this, m_lastInventoryPosition)
                    || m_lastInventory.TryAutoInsert(this);
            }

            if (m_lastSlot && m_lastSlot.CanEquip(this))
            {
                m_lastSlot.Equip(this);
                return true;
            }
            else if (Level.instance.player.inventory.instance.TryAddItem(item))
            {
                Destroy(gameObject);
                return true;
            }

            return false;
        }

        public void OnPointerEnter(PointerEventData _)
        {
#if UNITY_STANDALONE || UNITY_WEBGL
            m_hovering = true;

            if (!GUI.instance.selected)
                GUIItemInspector.instance.Show(this);
#endif
        }

        public void OnPointerExit(PointerEventData _)
        {
#if UNITY_STANDALONE || UNITY_WEBGL
            m_hovering = false;
            GUIItemInspector.instance.Hide();
#endif
        }

        public void OnPointerDown(PointerEventData eventData)
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
                GUIItemInspector.instance.Show(this);
                GUIItemInspector.instance.SetPositionRelativeTo((RectTransform)transform);
                EventSystem.current.SetSelectedGameObject(gameObject);
            }

            m_hovering = true;
            m_lastClickTime = Time.time;

#endif
        }

        public virtual void OnDrag(PointerEventData _)
        {
            if (onMerchant)
                return;

            GUI.instance.Select(this);
        }

        public virtual void OnEndDrag(PointerEventData eventData)
        {
            if (TryDropOnUiTarget(eventData))
                return;

            StartCoroutine(DropAfterUiDropHandlers());
        }

        protected virtual bool TryDropOnUiTarget(PointerEventData eventData)
        {
            if (GUI.instance.selected != this)
                return false;

            if (TryDropOnRaycastTarget(eventData))
                return true;

            return TryDropOnRectTarget(eventData);
        }

        protected virtual bool TryDropOnRaycastTarget(PointerEventData eventData)
        {
            if (EventSystem.current == null)
                return false;

            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var result in results)
            {
                if (!result.gameObject)
                    continue;

                var slot = result.gameObject.GetComponentInParent<GUIItemSlot>();
                if (slot)
                    return TryDropOnItemSlot(slot);

                var inventory = result.gameObject.GetComponentInParent<GUIInventory>();
                if (inventory)
                    return TryDropOnInventory(inventory);
            }

            return false;
        }

        protected virtual bool TryDropOnRectTarget(PointerEventData eventData)
        {
            var slots = Object.FindObjectsByType<GUIItemSlot>(FindObjectsSortMode.None);

            foreach (var slot in slots)
            {
                if (slot && IsPointerInside((RectTransform)slot.transform, eventData))
                    return TryDropOnItemSlot(slot);
            }

            var inventories = Object.FindObjectsByType<GUIInventory>(FindObjectsSortMode.None);

            foreach (var inventory in inventories)
            {
                if (
                    inventory
                    && inventory.gridContainer
                    && IsPointerInside(inventory.gridContainer, eventData)
                )
                    return TryDropOnInventory(inventory);
            }

            return false;
        }

        protected virtual bool IsPointerInside(RectTransform rect, PointerEventData eventData)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(
                rect,
                eventData.position,
                eventData.pressEventCamera
            );
        }

        protected virtual bool TryDropOnItemSlot(GUIItemSlot slot)
        {
            if (slot.TryEquipOrStackSelectedItem())
                return true;

            TryMoveToLastPosition();
            GameAudio.instance.PlayDeniedSound();
            return true;
        }

        protected virtual bool TryDropOnInventory(GUIInventory inventory)
        {
            if (inventory.TryPlace(this))
                GUI.instance.Deselect();
            else
            {
                TryMoveToLastPosition();
                GameAudio.instance.PlayDeniedSound();
            }

            return true;
        }

        protected virtual System.Collections.IEnumerator DropAfterUiDropHandlers()
        {
            yield return null;

            if (GUI.instance.selected == this)
                GUI.instance.DropItem();
        }

        public virtual void OnDrop(PointerEventData _)
        {
            if (TryStack(GUI.instance.selected))
                GUI.instance.ClearSelection();
        }

        public virtual void OnDeselect(BaseEventData _)
        {
            m_hovering = false;
            GUIItemInspector.instance.Hide();
        }

        /// <summary>
        /// Initializes the GUI Item with a given Item Instance.
        /// </summary>
        /// <param name="item">The Item Instance this GUI Item represents.</param>
        public virtual void Initialize(ItemInstance item)
        {
            if (item == null)
                return;

            this.item = item;
            this.item.onStackChanged += UpdateStackText;

            image.sprite = item.data.image;
            stackText.enabled = item.IsStackable();
            ((RectTransform)transform).sizeDelta =
                new Vector2(item.columns, item.rows) * Inventory.CellSize;
            merchant = GetComponentInParent<GUIMerchant>();

            UpdateStackText();
        }

        protected virtual void OnDisable()
        {
            if (m_hovering)
            {
                m_hovering = false;
                GUIItemInspector.instance.Hide();
            }
        }
    }
}
