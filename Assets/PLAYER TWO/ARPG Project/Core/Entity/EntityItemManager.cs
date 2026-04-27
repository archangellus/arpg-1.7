using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Item Manager")]
    public partial class EntityItemManager : MonoBehaviour
    {
        public UnityEvent onChanged;
        public UnityEvent<int> onConsumeItem;

        [Header("Item Slots")]
        [Tooltip("A transform used as a weapon slot on the right hand.")]
        public Transform rightHandSlot;

        [Tooltip("A transform used as a weapon slot on the left hand.")]
        public Transform leftHandSlot;

        [Tooltip("A transform used as a shield slot.")]
        public Transform leftHandShieldSlot;

        [Tooltip("A transform used as the origin to instantiate projectiles from.")]
        public Transform projectileOrigin;

        [Header("Item Renderers")]
        [Tooltip("The skinned mesh renderer corresponding to the Character's head.")]
        public SkinnedMeshRenderer helmRenderer;

        [Tooltip("The skinned mesh renderer corresponding to the Character's chest and abdomen.")]
        public SkinnedMeshRenderer chestRenderer;

        [Tooltip("The skinned mesh renderer corresponding to the Character's hips and thighs.")]
        public SkinnedMeshRenderer pantsRenderer;

        [Tooltip("The skinned mesh renderer corresponding to the Character's hands and forearms.")]
        public SkinnedMeshRenderer glovesRenderer;

        [Tooltip("The skinned mesh renderer corresponding to the Character's feet and calfs.")]
        public SkinnedMeshRenderer bootsRenderer;

        [Header("Character Pieces")]
        [Tooltip("The array of GameObjects that represent the Character's pieces.")]
        public GameObject[] entityPieces;

        [Tooltip("The array of names of the pieces that should be visible at the start.")]
        public string[] initialVisiblePieces;

        [Header("Initial Items")]
        [Tooltip("The initial item to be equipped in the right hand.")]
        public Item rightHandItem;

        [Tooltip("The initial item to be equipped in the left hand.")]
        public Item leftHandItem;

        [Header("Durability Settings")]
        [Range(0, 1f)]
        [Tooltip(
            "The chance of decreasing the durability points of the equipped items after receiving damage."
        )]
        public float onDamageDecreaseChance = 0.1f;

        [Tooltip("The amount of durability points lost after receiving a damage.")]
        public int onDamageDecreaseAmount = 1;

        protected GameObject m_rightHandObject;
        protected GameObject m_leftHandObject;

        protected Dictionary<ItemSlots, ItemInstance> m_items = new();
        protected Dictionary<ItemSlots, GameObject> m_activeInstances = new();
        protected Dictionary<ItemSlots, Dictionary<ItemInstance, GameObject>> m_itemInstances =
            new();
        protected Dictionary<ItemSlots, SkinnedMeshRenderer> m_itemRenderers = new();
        protected Dictionary<ItemSlots, Material[]> m_itemMaterials = new();
        protected Dictionary<ItemSlots, Mesh> m_defaultItemMeshes = new();
        protected Dictionary<ItemSlots, Material[]> m_defaultItemMaterials = new();

        protected Dictionary<string, GameObject> m_entityPieces = new();

        protected List<ItemInstance> m_consumables = new();

        protected Entity m_entity;

        /// <summary>
        /// The Entity assigned to this Item Manager.
        /// </summary>
        public Entity entity
        {
            get
            {
                if (!m_entity)
                    m_entity = GetComponent<Entity>();

                return m_entity;
            }
        }

        protected virtual void InitializeRenderers()
        {
            InitializeSlotRenderer(ItemSlots.Helm, ref helmRenderer);
            InitializeSlotRenderer(ItemSlots.Chest, ref chestRenderer);
            InitializeSlotRenderer(ItemSlots.Pants, ref pantsRenderer);
            InitializeSlotRenderer(ItemSlots.Gloves, ref glovesRenderer);
            InitializeSlotRenderer(ItemSlots.Boots, ref bootsRenderer);
        }

        protected virtual void InitializeCallbacks()
        {
            entity.onDamage.AddListener((amount, origin, critical) => ApplyDamage());
        }

        protected virtual void InitializeItems()
        {
            if (rightHandItem)
            {
                var instance = new ItemInstance(rightHandItem);
                Equip(instance, ItemSlots.RightHand);
            }

            if (leftHandItem)
            {
                var instance = new ItemInstance(leftHandItem);
                Equip(instance, ItemSlots.LeftHand);
            }
        }

        protected virtual void InitializeSlotRenderer(
            ItemSlots slot,
            ref SkinnedMeshRenderer renderer
        )
        {
            if (!renderer)
                return;

            var defaultMaterials = new List<Material>();
            m_itemRenderers.Add(slot, renderer);
            m_defaultItemMeshes.Add(slot, renderer.sharedMesh);
            renderer.GetSharedMaterials(defaultMaterials);
            m_itemMaterials.Add(slot, defaultMaterials.ToArray());
            m_defaultItemMaterials.Add(slot, defaultMaterials.ToArray());
        }

        protected virtual void InitializeEntityPieces()
        {
            InitializePiecesHash();
            InitializePiecesVisibility();
        }

        protected virtual void InitializePiecesHash()
        {
            foreach (var piece in entityPieces)
            {
                if (piece == null)
                    continue;

                if (m_entityPieces.ContainsKey(piece.name))
                {
                    Debug.LogWarning(
                        $"The entity '{gameObject.name}' already has a piece named '{piece.name}'."
                    );
                    continue;
                }

                m_entityPieces.Add(piece.name, piece);
            }
        }

        protected virtual void InitializePiecesVisibility()
        {
            foreach (var piece in m_entityPieces)
                piece.Value.SetActive(false);

            foreach (var name in initialVisiblePieces)
            {
                if (!m_entityPieces.ContainsKey(name))
                {
                    Debug.LogWarning(
                        $"The entity '{gameObject.name}' does not have a piece named '{name}'."
                    );
                    continue;
                }

                m_entityPieces[name].SetActive(true);
            }
        }

        /// <summary>
        /// Returns an array containing all the equipped items.
        /// </summary>
        public virtual ItemInstance[] GetEquippedItems()
        {
            var items = m_items.Select(item => item.Value);

            return items.Where(item => item != null).ToArray();
        }

        /// <summary>
        /// Returns true if the Entity can equip a given Item Instance in a given slot.
        /// </summary>
        /// <param name="item">The Item Instance you want to equip.</param>
        /// <param name="slot">The slot in which you want to equip the item to.</param>
        public virtual bool CanEquip(ItemInstance item, ItemSlots slot)
        {
            if (item == null || !item.IsEquippable())
                return false;

            if (entity.stats.level < item.GetEquippable().requiredLevel)
                return false;
            if (entity.stats.strength < item.GetEquippable().requiredStrength)
                return false;
            if (entity.stats.dexterity < item.GetEquippable().requiredDexterity)
                return false;

            if (item.IsArmor() && item.GetArmor().slot != slot)
                return false;
            if (item.IsWeapon() && slot != ItemSlots.RightHand && slot != ItemSlots.LeftHand)
                return false;

            if (item.IsShield())
            {
                if (slot != ItemSlots.LeftHand)
                    return false;

                if (IsUsingWeaponRight())
                {
                    if (!IsUsingBlade() || GetRightBlade().IsTwoHanded())
                        return false;
                }
            }

            if (item.IsBlade())
            {
                if (item.GetBlade().IsTwoHanded())
                {
                    if (slot != ItemSlots.RightHand)
                        return false;
                    if (IsUsingWeaponLeft() || IsUsingShield())
                        return false;
                }
                else if (slot == ItemSlots.LeftHand)
                {
                    if (!IsUsingBlade() || GetRightBlade().IsTwoHanded())
                        return false;
                }
            }

            if (item.IsBow())
            {
                if (slot != ItemSlots.RightHand)
                    return false;
                if (IsUsingWeaponLeft() || IsUsingShield())
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Tries to equip a given Item Instance in a given slot.
        /// </summary>
        /// <param name="item">The Item Instance you want to equip.</param>
        /// <param name="slot">The slot in which you want to equip the Item to.</param>
        /// <returns>Returns true if the Entity was able to equip the Item.</returns>
        public virtual bool TryEquip(ItemInstance item, ItemSlots slot)
        {
            if (!CanEquip(item, slot))
                return false;

            Equip(item, slot);
            return true;
        }

        /// <summary>
        /// Equips an Item Instance to a given slot.
        /// </summary>
        /// <param name="item">The Item Instance you want to equip.</param>
        /// <param name="slot">The slot in which you want to equip the Item to.</param>
        protected virtual void Equip(ItemInstance item, ItemSlots slot)
        {
            if (!m_items.ContainsKey(slot))
                m_items.Add(slot, null);

            m_items[slot] = item;
            m_items[slot].onChanged += OnItemChanged;

            if (item.IsWeapon() || item.IsShield())
                EquipAndInstantiate(item, slot);
            else if (item.IsArmor())
                UpdateArmor(item);

            onChanged?.Invoke();
        }

        /// <summary>
        /// Equips and instantiates the item Game Object from an Item Instance.
        /// The item's parent will be the transform slot corresponding to the item slot.
        /// </summary>
        /// <param name="item">The Item Instance you want to equip and instantiate the Game Object from.</param>
        /// <param name="slot">The slot in which you want to equip the item.</param>
        protected virtual void EquipAndInstantiate(ItemInstance item, ItemSlots slot)
        {
            if (!m_activeInstances.ContainsKey(slot))
                m_activeInstances.Add(slot, null);

            m_activeInstances[slot].SafeCall(i => i.SetActive(false));

            if (!m_itemInstances.ContainsKey(slot))
                m_itemInstances.Add(slot, new Dictionary<ItemInstance, GameObject>());

            if (!m_itemInstances[slot].ContainsKey(item))
            {
                var instance = InstantiateItem(item.data, slot);
                m_itemInstances[slot].Add(item, instance);
                m_activeInstances[slot] = instance;
            }
            else
            {
                m_activeInstances[slot] = m_itemInstances[slot][item];
                m_activeInstances[slot].SetActive(true);
            }
        }

        /// <summary>
        /// Updates the Entity's armor pieces or the Skinned Mesh Renderer.
        /// </summary>
        /// <param name="item">The Item Instance you want to update the armor from.</param>
        protected virtual void UpdateArmor(ItemInstance item)
        {
            EquipAndUpdateRenderer(item, item.GetArmor().slot);
            UpdateEntityPieces(item);
        }

        /// <summary>
        /// Equips and updates the item Skinned Mesh Renderer from an Item Instance.
        /// The renderer will be correspondent to the Item's slot.
        /// </summary>
        /// <param name="item">The Item Instance you want to equip and update the renderer from.</param>
        /// <param name="slot">The slot in which you want to equip the item.</param>
        protected virtual void EquipAndUpdateRenderer(ItemInstance item, ItemSlots slot)
        {
            if (!m_itemRenderers.ContainsKey(slot))
                return;

            m_itemRenderers[slot].sharedMesh = item.GetArmor().mesh;

            if (item.GetArmor().HasMaterials())
                m_itemRenderers[slot].sharedMaterials = item.GetArmor().materials;
        }

        /// <summary>
        /// Updates the entity pieces based on the Item Instance equipped.
        /// </summary>
        /// <param name="item">The Item Instance you want to update the entity pieces from.</param>
        /// <param name="inverse">If true, it will hide instead of showing the pieces or vice versa.</param>
        protected virtual void UpdateEntityPieces(ItemInstance item, bool inverse = false)
        {
            foreach (var piece in item.GetArmor().pieces)
            {
                if (!m_entityPieces.ContainsKey(piece.name))
                {
                    Debug.LogWarning(
                        $"The entity '{gameObject.name}' does not have a piece named '{piece.name}'."
                    );
                    continue;
                }

                var visibility = inverse ? !piece.show : piece.show;
                m_entityPieces[piece.name].SetActive(visibility);
            }
        }

        /// <summary>
        /// Removes the item from a given slot, if any is equipped. It also hides the item
        /// Game Object or restores the Skinned Mesh Renderer to its unequipped state.
        /// </summary>
        /// <param name="slot">The slot you want to remove the item from.</param>
        public virtual void RemoveItem(ItemSlots slot)
        {
            if (!m_items.TryGetValue(slot, out var item))
                return;

            if ((item.IsWeapon() || item.IsShield()) && m_itemInstances[slot][m_items[slot]])
            {
                m_itemInstances[slot][m_items[slot]].SetActive(false);
            }
            else if (item.IsArmor())
            {
                if (m_itemRenderers.ContainsKey(slot))
                {
                    m_itemRenderers[slot].sharedMesh = m_defaultItemMeshes[slot];

                    if (item.GetArmor().HasMaterials())
                        m_itemRenderers[slot].sharedMaterials = m_defaultItemMaterials[slot];
                }

                UpdateEntityPieces(item, inverse: true);
            }

            m_items[slot].onChanged -= OnItemChanged;
            m_items[slot] = null;
            onChanged?.Invoke();
        }

        /// <summary>
        /// Returns the Item Instance equipped in a given slot or initializes the slot.
        /// </summary>
        /// <param name="slot">The slot you want to get the Item Instance from or initialize.</param>
        public virtual ItemInstance GetOrInitializeItem(ItemSlots slot)
        {
            if (!m_items.ContainsKey(slot))
                m_items.Add(slot, null);

            return m_items[slot];
        }

        /// <summary>
        /// Sets the list of available consumable items.
        /// </summary>
        /// <param name="items">The array of Item Instance to assign to the consumable list.</param>
        public virtual void SetConsumables(ItemInstance[] items)
        {
            if (items == null)
                return;

            m_consumables = new List<ItemInstance>(items);
        }

        /// <summary>
        /// Sets a consumable item in the consumables list based on its index.
        /// </summary>
        /// <param name="index">The index of the slot in the consumables list.</param>
        /// <param name="item">The Item Instance you want to assign to the consumables slots.</param>
        public virtual void SetConsumable(int index, ItemInstance item) =>
            m_consumables[index] = item;

        /// <summary>
        /// Returns an array with all the consumables items.
        /// </summary>
        public virtual ItemInstance[] GetConsumables() => m_consumables.ToArray();

        public virtual void ConsumeItem(ItemInstance item)
        {
            var index = m_consumables.FindIndex(c => c == item);
            ConsumeItem(index);
        }

        /// <summary>
        /// Consumes an item from the consumable items list and applies its effects.
        /// </summary>
        /// <param name="index">The index from the consumables list.</param>
        public virtual void ConsumeItem(int index)
        {
            if (m_consumables.IsInvalidOrNullAt(index))
                return;

            if (m_consumables[index].stack > 0)
            {
                m_consumables[index].stack--;
                m_consumables[index].GetConsumable().Consume(entity);

                if (m_consumables[index].stack == 0)
                    m_consumables[index] = null;
            }

            onConsumeItem?.Invoke(index);
        }

        /// <summary>
        /// Applies damage to all the items if the random chance was met.
        /// </summary>
        public virtual void ApplyDamage()
        {
            foreach (var item in m_items)
            {
                if (item.Value == null)
                    continue;

                if (Random.Range(0, 1f) < onDamageDecreaseChance)
                    item.Value.ApplyDamage(onDamageDecreaseAmount);
            }
        }

        /// <summary>
        /// Instantiates the item's Game Object on a given slot.
        /// </summary>
        /// <param name="item">The Item you want to instantiate the Game Object from.</param>
        /// <param name="slot">The slot in which you want to instantiate the item to.</param>
        /// <returns>The instance of the newly instantiated Game Object.</returns>
        protected virtual GameObject InstantiateItem(Item item, ItemSlots slot)
        {
            GameObject instance = null;

            if (item is ItemBlade blade)
            {
                if (slot == ItemSlots.RightHand)
                    instance = blade.InstantiateRightHand(rightHandSlot);
                else if (slot == ItemSlots.LeftHand)
                    instance = blade.InstantiateLeftHand(leftHandSlot);
            }
            else if (item is ItemBow bow)
                instance = bow.Instantiate(rightHandSlot, leftHandSlot);
            else if (item is ItemShield)
                instance = item.Instantiate(leftHandShieldSlot);

            return instance;
        }

        /// <summary>
        /// Returns the accumulated defense points from all the equipped items.
        /// </summary>
        public virtual int GetDefense()
        {
            var total = 0;

            foreach (var item in m_items)
            {
                if (item.Value == null)
                    continue;

                total += item.Value.GetDefense();
            }

            return total;
        }

        /// <summary>
        /// Returns the accumulated minimum and maximum damage from all the equipped items.
        /// </summary>
        public virtual MinMax GetDamage()
        {
            var total = MinMax.Zero;

            foreach (var item in m_items)
            {
                if (item.Value == null)
                    continue;

                var damage = item.Value.GetDamage();
                total.min += damage.min;
                total.max += damage.max;
            }

            return total;
        }

        /// <summary>
        /// Returns the attack speed of the equipped weapons. If the Entity is equipping
        /// two weapons, the attack speed will increase by half of the left weapon's attack speed.
        /// </summary>
        public virtual int GetAttackSpeed()
        {
            if (!IsUsingWeaponRight())
                return 0;

            var total = GetRightWeapon().attackSpeed;

            if (IsUsingWeaponLeft())
                total += (int)(GetLeftWeapon().attackSpeed * 0.5f);

            return total;
        }

        /// <summary>
        /// Returns the chance to block an attack from the shield. If no shield
        /// is equipped, it always returns zero.
        /// </summary>
        public virtual float GetChanceToBlock()
        {
            if (!IsUsingShield() || GetLeftHand().IsBroken())
                return 0;

            return GetShield().chanceToBlock / 100f;
        }

        /// <summary>
        /// Returns the accumulated points from all additional attributes (the 'blue' attributes).
        /// </summary>
        public virtual ItemFinalAttributes GetFinalAttributes() => new(m_items.Values.ToArray());

        /// <summary>
        /// Instantiates, calculates the damage, and configure the projectile form the current weapon.
        /// </summary>
        public virtual Projectile ShootProjectile()
        {
            if (!IsUsingBow())
                return null;

            var damage = entity.stats.GetDamage(out var critical);
            var projectile = Instantiate(
                GetBow().projectile,
                projectileOrigin.position,
                projectileOrigin.rotation
            );

            projectile.SetDamage(entity, damage, critical, entity.targetTags);
            return projectile;
        }

        protected virtual void OnItemChanged() => onChanged.Invoke();

        protected virtual void Awake()
        {
            InitializeRenderers();
            InitializeEntityPieces();
            InitializeCallbacks();
            InitializeItems();
        }
    }
}
