using UnityEngine;
using UnityEngine.Events;
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
                m_togglePetInventory.performed += _ =>
                {
                    onTogglePetInventory.Invoke();

                    if (onTogglePetInventory.GetPersistentEventCount() == 0)
                        GUIWindowsManager.instance.SafeCall(w => w.TogglePetInventory());
                };

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
            Console.instance.onConsoleClosed.AddListener(() => actions.Enable());
        }

        protected virtual void InitializeGameCallbacks()
        {
            GameScenes.instance.onSceneLoadTriggered.AddListener(SafeDeselect);
        }

        public virtual void Select(GUIItem item)
        {
            if (!selected)
            {
                selected = item;
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
                selected.Deselect();
                selected = null;
                m_entity.canUpdateDestination = true;
                onDeselectItem?.Invoke(item);
            }
        }

        public virtual void ClearSelection()
        {
            if (selected)
            {
                Destroy(selected.gameObject);
                selected = null;
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

        protected virtual void LateUpdate()
        {
            HandleItemPosition();
            HandleDropEntityRestoration();
        }

        protected virtual void OnEnable() => actions.Enable();

        protected virtual void OnDisable() => actions.Disable();
    }
}
