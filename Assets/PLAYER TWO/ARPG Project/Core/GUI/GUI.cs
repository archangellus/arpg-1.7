using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI")]
    public class GUI : Singleton<GUI>
    {
        public UnityEvent<GUIItem> onSelectItem;
        public UnityEvent<GUIItem> onDeselectItem;

        [Tooltip("The Input Action Asset with all GUI actions.")]
        public InputActionAsset actions;

        [Header("Items Settings")]
        [Tooltip("The prefab to use when instantiating GUI Items.")]
        public GUIItem itemPrefab;

        [Header("Stack Split Settings")]
        [Tooltip(
            "Optional custom UI prefab for the stack split menu. "
                + "Leave empty to use the generated default menu."
        )]
        public GUIStackSplitMenu stackSplitMenuPrefab;

        [Tooltip(
            "Optional parent for the stack split menu. "
                + "Leave empty to parent the menu to this GUI transform."
        )]
        public RectTransform stackSplitMenuContainer;

        [Header("Containers Settings")]
        [Tooltip("The container with all collectible titles.")]
        public RectTransform collectiblesContainer;

        [Header("Item Drop Settings")]
        [Tooltip("If true, the Player can drop items from the GUI.")]
        public bool canDropItems = true;

        [Tooltip(
            "The duration in seconds before being able to move the Player after dropping an Item."
        )]
        public float movementRestorationDelay = 0.2f;

        [Header("Input Callbacks")]
        public UnityEvent onToggleSkills;
        public UnityEvent onToggleCharacter;
        public UnityEvent onToggleInventory;
        public UnityEvent onTogglePetInventory;
        public UnityEvent onToggleQuestLog;
        public UnityEvent onToggleMap;
        public UnityEvent onToggleMenu;
        public UnityEvent onToggleCollectiblesNames;

        protected InputAction m_toggleSkills;
        protected InputAction m_toggleCharacter;
        protected InputAction m_toggleInventory;
        protected InputAction m_togglePetInventory;
        protected InputAction m_toggleQuestLog;
        protected InputAction m_toggleMap;
        protected InputAction m_toggleMenu;
        protected InputAction m_toggleMenuWebGL;
        protected InputAction m_dropItem;
        protected InputAction m_toggleCollectiblesNames;

        protected Entity m_entity;

        protected float m_dropTime;
        protected int m_selectionFrame = -1;
        protected bool m_waitingForSelectionRelease;
        protected GUIItem m_splitPreview;
        protected GUIStackSplitMenu m_stackSplitMenu;

        public GUIItem selected { get; protected set; }

        protected virtual void InitializeEntity() => m_entity = Level.instance.player;

        protected virtual void InitializeActions()
        {
            m_toggleSkills = actions["Toggle Skills"];
            m_toggleCharacter = actions["Toggle Character"];
            m_toggleInventory = actions["Toggle Inventory"];
            m_togglePetInventory = actions.FindAction("Toggle Pet Inventory", false);
            m_toggleQuestLog = actions["Toggle Quest Log"];
            m_toggleMap = actions["Toggle Map"];
            m_toggleMenu = actions["Toggle Menu"];
            m_toggleMenuWebGL = actions["Toggle Menu (WebGL)"];
            m_dropItem = actions["Drop Item"];
            m_toggleCollectiblesNames = actions["Toggle Collectibles Names"];
        }

        protected virtual void InitializeCallbacks()
        {
            m_toggleSkills.performed += _ => onToggleSkills.Invoke();
            m_toggleCharacter.performed += _ => onToggleCharacter.Invoke();
            m_toggleInventory.performed += _ => onToggleInventory.Invoke();

            if (m_togglePetInventory != null)
                m_togglePetInventory.performed += _ => HandlePetInventoryToggle();

            m_toggleQuestLog.performed += _ => onToggleQuestLog.Invoke();
            m_toggleMap.performed += _ => onToggleMap.Invoke();
#if UNITY_WEBGL
            m_toggleMenuWebGL.performed += _ => onToggleMenu.Invoke();
#else
            m_toggleMenu.performed += _ => onToggleMenu.Invoke();
#endif
            m_dropItem.performed += _ => DropItem();
            m_toggleCollectiblesNames.performed += _ => onToggleCollectiblesNames.Invoke();
        }

        protected virtual void InitializeConsoleCallbacks()
        {
            Console.instance.onConsoleOpened.AddListener(() => actions.Disable());
            Console.instance.onConsoleClosed.AddListener(() =>
            {
                actions.Enable();
                SetPetInventoryInputActive(PetInventorySettings.isPetActive);
            });
        }

        protected virtual void InitializeGameCallbacks()
        {
            GameScenes.instance.onSceneLoadTriggered.AddListener(SafeDeselect);
            PetSummonOwnership.onActivePetChanged += HandlePetActiveChanged;
            SetPetInventoryInputActive(PetInventorySettings.isPetActive);
        }

        protected virtual void HandlePetInventoryToggle()
        {
            if (!PetInventorySettings.isPetActive)
                return;

            onTogglePetInventory.Invoke();

            if (onTogglePetInventory.GetPersistentEventCount() == 0)
                GUIWindowsManager.instance.SafeCall(w => w.TogglePetInventory());
        }

        protected virtual void HandlePetActiveChanged(bool active)
        {
            SetPetInventoryInputActive(active);
        }

        protected virtual void SetPetInventoryInputActive(bool active)
        {
            if (m_togglePetInventory == null)
                return;

            if (active && !m_togglePetInventory.enabled)
                m_togglePetInventory.Enable();
            else if (!active && m_togglePetInventory.enabled)
                m_togglePetInventory.Disable();
        }

        public virtual void Select(GUIItem item)
        {
            if (!selected)
            {
                selected = item;
                m_selectionFrame = Time.frameCount;
                m_waitingForSelectionRelease = Mouse.current != null && Mouse.current.leftButton.isPressed;
                selected.transform.SetParent(transform);
                selected.Select();
                m_entity.canUpdateDestination = false;
                GUIItemInspector.instance.SafeCall(i => i.Hide());
                onSelectItem?.Invoke(selected);
            }
        }

        public virtual void Deselect()
        {
            if (selected)
            {
                var item = selected;
                DestroySplitPreview();
                selected.Deselect();
                selected = null;
                m_waitingForSelectionRelease = false;
                m_entity.canUpdateDestination = true;
                onDeselectItem?.Invoke(item);
            }
        }

        public virtual void ClearSelection()
        {
            if (selected)
            {
                DestroySplitPreview();
                Destroy(selected.gameObject);
                selected = null;
                m_waitingForSelectionRelease = false;
                m_entity.canUpdateDestination = true;
            }
        }

        public virtual void SafeDeselect()
        {
            if (!selected)
                return;

            selected.TryMoveToLastPosition();
            Deselect();
        }

        public virtual void DropItem()
        {
            if (!selected || !canDropItems)
                return;

            if (TryDropPetInventoryItemOnUi())
                return;

            if (TryDropPetInventoryItem())
                return;

                // >>> PLUGIN_PATCH:ArcDrop::FIND:return;|R2_974f70e6
                            var selectedItem = selected;
                            var dropHandled = EventBus.RaiseArcDropRequested(
                                this,
                                selectedItem,
                                m_entity,
                                () =>
                                {
                                    if (selectedItem)
                                        Destroy(selectedItem.gameObject);

                                    if (selected == selectedItem)
                                        selected = null;

                                    m_dropTime = Time.time;
                                },
                                SafeDeselect
                            );

                            if (dropHandled)
                                return;
                // <<< PLUGIN_PATCH:ArcDrop::FIND:return;|R2_974f70e6

            if (Level.instance.TryInstantiateItemDropAtMousePosition(selected.item))
            {
                Destroy(selected.gameObject);
                selected = null;
                m_dropTime = Time.time;
            }
#if UNITY_ANDROID || UNITY_IOS
            else
            {
                SafeDeselect();
            }
#endif
        }

        public virtual bool TryPlaceSelectedPetItem(GUIInventory inventory)
        {
            if (!inventory || !TryPrepareSelectedPetItem())
                return false;

            if (inventory.TryPlace(selected))
                Deselect();
            else
            {
                selected.TryMoveToLastPosition();
                GameAudio.instance.PlayDeniedSound();
            }

            return true;
        }

        public virtual bool TryEquipSelectedPetItem(GUIItemSlot slot)
        {
            if (!(slot is GUIEquipmentSlot) || !TryPrepareSelectedPetItem())
                return false;

            if (slot.TryEquipOrStackSelectedItem())
                return true;

            selected.TryMoveToLastPosition();
            GameAudio.instance.PlayDeniedSound();
            return true;
        }

        protected virtual bool TryPrepareSelectedPetItem()
        {
            if (!PetInventorySettings.isPetActive || !selected)
                return false;

            var petInventory = GUIWindowsManager.instance.SafeGet(w => w.GetPetInventory());

            if (!petInventory)
                return false;

            if (selected.WasRemovedFrom(petInventory))
                return true;

            return petInventory.Contains(selected) && petInventory.TryRemove(selected);
        }


        /// <summary>
        /// Returns true while the player is holding either Shift key for stack splitting.
        /// </summary>
        public virtual bool IsStackSplitModifierPressed()
        {
            return Keyboard.current != null
                && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        }

        /// <summary>
        /// Returns true if a selected GUI Item can start a stack split operation.
        /// </summary>
        public virtual bool CanSplitSelectedItem(GUIItem carried)
        {
            return selected
                && carried
                && selected == carried
                && IsStackSplitModifierPressed()
                && selected.item != null
                && selected.item.IsStackable()
                && selected.item.stack > 1
                && selected.hasLastInventory
                && !selected.onMerchant;
        }

        public virtual bool CanSplitSelectedItemToSlot(GUIItemSlot slot, GUIItem carried)
        {
            if (!CanSplitSelectedItem(carried) || !slot)
                return false;

            var split = selected.item.CopyWithStack(1);
            return slot.CanEquip(CreateValidationItem(split))
                || (slot.item && slot.item.item != null && slot.item.item.CanStack(split));
        }

        protected virtual GUIItem CreateValidationItem(ItemInstance item)
        {
            var validation = CreateGUIItem(item);
            validation.gameObject.SetActive(false);
            Destroy(validation.gameObject);
            return validation;
        }

        /// <summary>
        /// Tries to place one item from the selected stack into an Inventory and opens the split menu.
        /// </summary>
        public virtual bool TrySplitSelectedItemToInventory(GUIInventory inventory, GUIItem carried)
        {
            if (!CanSplitSelectedItem(carried) || !inventory)
                return false;

            if (WouldSplitOverlapSourceInventoryCell(inventory, carried))
            {
                GameAudio.instance.PlayDeniedSound();
                return true;
            }

            var splitItem = selected.item.CopyWithStack(1);
            var splitGuiItem = CreateGUIItem(splitItem);

            if (!inventory.TryPlace(splitGuiItem))
            {
                Destroy(splitGuiItem.gameObject);
                GameAudio.instance.PlayDeniedSound();
                return true;
            }

            CompleteInitialSplitMove(selected.item, splitItem, 0);
            return true;
        }

        protected virtual bool WouldSplitOverlapSourceInventoryCell(GUIInventory inventory, GUIItem carried)
        {
            if (!inventory || !carried || inventory != carried.lastInventory)
                return false;

            var target = inventory.FindClosestCell(carried);
            var source = carried.lastInventoryPosition;

            return target.x < source.row + carried.item.rows
                && target.x + carried.item.rows > source.row
                && target.y < source.column + carried.item.columns
                && target.y + carried.item.columns > source.column;
        }

        /// <summary>
        /// Tries to place one item from the selected stack into an item slot and opens the split menu.
        /// </summary>
        public virtual bool TrySplitSelectedItemToSlot(GUIItemSlot slot, GUIItem carried)
        {
            if (!CanSplitSelectedItem(carried) || !slot)
                return false;

            var splitItem = selected.item.CopyWithStack(1);
            var splitGuiItem = CreateGUIItem(splitItem);
            var destinationInitialStack = 0;

            if (slot.item && slot.item.item != null && slot.item.item.CanStack(splitItem))
            {
                destinationInitialStack = slot.item.item.stack;
                slot.item.item.stack += 1;
                Destroy(splitGuiItem.gameObject);
                splitItem = slot.item.item;
            }
            else if (slot.CanEquip(splitGuiItem))
            {
                slot.Equip(splitGuiItem);
            }
            else
            {
                Destroy(splitGuiItem.gameObject);
                GameAudio.instance.PlayDeniedSound();
                return true;
            }

            CompleteInitialSplitMove(selected.item, splitItem, destinationInitialStack);
            return true;
        }

        /// <summary>
        /// Tries to move one item from the selected stack into another visible stack and opens the split menu.
        /// </summary>
        public virtual bool TrySplitSelectedItemToExistingStack(GUIItem destination, GUIItem carried)
        {
            if (!CanSplitSelectedItem(carried) || !destination || destination == carried)
                return false;

            if (destination.item == null || !destination.item.CanStack(selected.item.CopyWithStack(1)))
                return false;

            var destinationInitialStack = destination.item.stack;
            destination.item.stack += 1;
            CompleteInitialSplitMove(selected.item, destination.item, destinationInitialStack);
            return true;
        }

        protected virtual void CompleteInitialSplitMove(
            ItemInstance source,
            ItemInstance destination,
            int destinationInitialStack
        )
        {
            source.stack -= 1;
            DestroySplitPreview();
            selected.TryMoveToLastPosition();
            ShowStackSplitMenu(source, destination, destinationInitialStack);
        }

        protected virtual void ShowStackSplitMenu(
            ItemInstance source,
            ItemInstance destination,
            int destinationInitialStack
        )
        {
            if (!m_stackSplitMenu)
                m_stackSplitMenu = GUIStackSplitMenu.Create(
                    stackSplitMenuContainer ? stackSplitMenuContainer : transform,
                    stackSplitMenuPrefab
                );

            m_stackSplitMenu.Show(source, destination, 1, destinationInitialStack);
        }

        protected virtual void HandleSplitPreview()
        {
            if (!selected)
            {
                DestroySplitPreview();
                return;
            }

            if (!CanSplitSelectedItem(selected))
            {
                DestroySplitPreview();
                return;
            }

            if (m_splitPreview || !selected.lastInventory)
                return;

            m_splitPreview = selected.lastInventory.CreateVisualCopy(
                selected,
                selected.lastInventoryPosition
            );
        }

        protected virtual void DestroySplitPreview()
        {
            if (m_splitPreview)
                Destroy(m_splitPreview.gameObject);

            m_splitPreview = null;
        }

        protected virtual bool TryDropPetInventoryItemOnUi()
        {
            if (!TryPrepareSelectedPetItem())
                return false;

            if (TryDropSelectedPetItemOnEquipmentSlot())
                return true;

            return TryDropSelectedPetItemOnInventory();
        }

        protected virtual bool TryDropSelectedPetItemOnEquipmentSlot()
        {
            var slots = Object.FindObjectsByType<GUIEquipmentSlot>(FindObjectsSortMode.None);

            foreach (var slot in slots)
            {
                if (slot && IsPointerInside((RectTransform)slot.transform))
                    return TryEquipSelectedPetItem(slot);
            }

            return false;
        }

        protected virtual bool TryDropSelectedPetItemOnInventory()
        {
            var inventories = Object.FindObjectsByType<GUIInventory>(FindObjectsSortMode.None);

            foreach (var inventory in inventories)
            {
                if (inventory && IsPointerInside(inventory.gridContainer))
                    return TryPlaceSelectedPetItem(inventory);
            }

            return false;
        }

        protected virtual bool IsPointerInside(RectTransform rect)
        {
            return rect
                && RectTransformUtility.RectangleContainsScreenPoint(
                    rect,
                    EntityInputs.GetPointerPosition(),
                    GetEventCamera(rect)
                );
        }

        protected virtual Camera GetEventCamera(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();

            if (!canvas || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera;
        }

        protected virtual bool TryDropPetInventoryItem()
        {
            var petInventory = GUIWindowsManager.instance.SafeGet(w => w.GetPetInventory());

            if (!petInventory || !selected.WasRemovedFrom(petInventory))
                return false;

            var petTransform = PetInventorySettings.activePetTransform;

            if (!petTransform)
            {
                SafeDeselect();
                return true;
            }

            Level.instance.InstantiateItemDrop(selected.item, petTransform.position);
            Destroy(selected.gameObject);
            selected = null;
            m_dropTime = Time.time;
            return true;
        }

        protected virtual void HandleSelectedPetItemPointerClick()
        {
            if (Mouse.current == null || !selected || !IsSelectedPetItem())
                return;

            if (m_waitingForSelectionRelease)
            {
                if (Mouse.current.leftButton.isPressed)
                    return;

                m_waitingForSelectionRelease = false;
                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame || Time.frameCount == m_selectionFrame)
                return;

            if (TryClickSelectedPetItemOnRaycastTarget())
                return;

            TryDropPetInventoryItemOnUi();
        }

        protected virtual bool IsSelectedPetItem()
        {
            var petInventory = GUIWindowsManager.instance.SafeGet(w => w.GetPetInventory());

            return petInventory
                && selected
                && (selected.WasRemovedFrom(petInventory) || petInventory.Contains(selected));
        }

        protected virtual bool TryClickSelectedPetItemOnRaycastTarget()
        {
            if (EventSystem.current == null)
                return false;

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = EntityInputs.GetPointerPosition(),
            };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);

            foreach (var result in results)
            {
                if (!result.gameObject)
                    continue;

                var slot = result.gameObject.GetComponentInParent<GUIEquipmentSlot>();
                if (slot)
                    return TryEquipSelectedPetItem(slot);

                var inventory = result.gameObject.GetComponentInParent<GUIInventory>();
                if (inventory)
                    return TryPlaceSelectedPetItem(inventory);
            }

            return false;
        }

        protected virtual void HandleItemPosition()
        {
            if (!selected)
                return;

            selected.transform.position = EntityInputs.GetPointerPosition();
        }

        protected virtual void HandleDropEntityRestoration()
        {
            if (
                !selected
                && !m_entity.canUpdateDestination
                && Time.time - m_dropTime > movementRestorationDelay
            )
            {
                m_entity.canUpdateDestination = true;
            }
        }

        public GUIItem CreateGUIItem(ItemInstance item, RectTransform container = null)
        {
            var parent = container ? container : transform;
            var instance = Instantiate(itemPrefab, parent);
            instance.Initialize(item);
            return instance;
        }

        protected virtual void Start()
        {
            InitializeEntity();
            InitializeActions();
            InitializeCallbacks();
            InitializeConsoleCallbacks();
            InitializeGameCallbacks();
        }

        protected virtual void Update()
        {
            HandleSelectedPetItemPointerClick();
            HandleSplitPreview();
        }

        protected virtual void LateUpdate()
        {
            HandleItemPosition();
            HandleDropEntityRestoration();
        }

        protected virtual void OnEnable()
        {
            actions.Enable();
            SetPetInventoryInputActive(PetInventorySettings.isPetActive);
        }

        protected virtual void OnDisable() => actions.Disable();

        protected virtual void OnDestroy()
        {
            PetSummonOwnership.onActivePetChanged -= HandlePetActiveChanged;
        }
    }
}
