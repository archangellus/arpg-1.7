namespace PLAYERTWO.ARPGProject
{
    public partial class EntityItemManager
    {
        /// <summary>
        /// Returns true if the Entity is equipping a weapon in any of its hands.
        /// </summary>
        public virtual bool IsUsingWeapon() => IsUsingWeaponLeft() || IsUsingWeaponRight();

        /// <summary>
        /// Returns true if the Entity is equipping a weapon in the right hand.
        /// </summary>
        public virtual bool IsUsingWeaponRight() => GetOrInitializeItem(ItemSlots.RightHand)?.data is ItemWeapon;

        /// <summary>
        /// Returns true if the Entity is equipping a weapon in the left hand.
        /// </summary>
        public virtual bool IsUsingWeaponLeft() => GetOrInitializeItem(ItemSlots.LeftHand)?.data is ItemWeapon;

        /// <summary>
        /// Returns true if the equipped weapon in the right hand is a blade.
        /// </summary>
        public virtual bool IsUsingBlade() => GetOrInitializeItem(ItemSlots.RightHand)?.data is ItemBlade;

        /// <summary>
        /// Returns true if the equipped weapon in the left hand is a blade.
        /// </summary>
        public virtual bool IsUsingBladeLeft() => GetOrInitializeItem(ItemSlots.LeftHand)?.data is ItemBlade;

        /// <summary>
        /// Returns true if the Entity is equipping a shield.
        /// </summary>
        public virtual bool IsUsingShield() => GetOrInitializeItem(ItemSlots.LeftHand)?.data is ItemShield;

        /// <summary>
        /// Returns true if the Entity is equipping a bow or crossbow.
        /// </summary>
        public virtual bool IsUsingBow() => GetOrInitializeItem(ItemSlots.RightHand)?.data is ItemBow;

        /// <summary>
        /// Returns the Item Instance of the item equipped on the right hand slot.
        /// </summary>
        public virtual ItemInstance GetRightHand() => GetOrInitializeItem(ItemSlots.RightHand);

        /// <summary>
        /// Returns the Item Instance of the item equipped on the left hand slot.
        /// </summary>
        public virtual ItemInstance GetLeftHand() => GetOrInitializeItem(ItemSlots.LeftHand);

        /// <summary>
        /// Returns the Item Instance of the item equipped on the helm slot.
        /// </summary>
        public virtual ItemInstance GetHelm() => GetOrInitializeItem(ItemSlots.Helm);

        /// <summary>
        /// Returns the Item Instance of the item equipped on the chest slot.
        /// </summary>
        public virtual ItemInstance GetChest() => GetOrInitializeItem(ItemSlots.Chest);

        /// <summary>
        /// Returns the Item Instance of the item equipped on the pants slot.
        /// </summary>
        public virtual ItemInstance GetPants() => GetOrInitializeItem(ItemSlots.Pants);

        /// <summary>
        /// Returns the Item Instance of the item equipped on the gloves slot.
        /// </summary>
        public virtual ItemInstance GetGloves() => GetOrInitializeItem(ItemSlots.Gloves);

        /// <summary>
        /// Returns the Item Instance of the item equipped on the boots slot.
        /// </summary>
        public virtual ItemInstance GetBoots() => GetOrInitializeItem(ItemSlots.Boots);

        /// <summary>
        /// Returns the Item Weapon of the item equipped on the right hand slot.
        /// </summary>
        public virtual ItemWeapon GetRightWeapon() => GetOrInitializeItem(ItemSlots.RightHand).GetData<ItemWeapon>();

        /// <summary>
        /// Returns the Item Weapon of the item equipped on the left hand slot.
        /// </summary>
        public virtual ItemWeapon GetLeftWeapon() => GetOrInitializeItem(ItemSlots.LeftHand).GetData<ItemWeapon>();

        /// <summary>
        /// Returns the Item Weapon of the item equipped on the right or left hand slots.
        /// </summary>
        public virtual ItemWeapon GetWeapon() => IsUsingWeaponRight() ? GetRightWeapon() : GetLeftWeapon();

        /// <summary>
        /// Returns the Item Blade of the item equipped on the right hand slot.
        /// </summary>
        public virtual ItemBlade GetRightBlade() => GetOrInitializeItem(ItemSlots.RightHand).GetData<ItemBlade>();

        /// <summary>
        /// Returns the Item Blade of the item equipped on the left hand slot.
        /// </summary>
        public virtual ItemBlade GetLeftBlade() => GetOrInitializeItem(ItemSlots.LeftHand).GetData<ItemBlade>();

        /// <summary>
        /// Returns the Item Shield of the item equipped on the shield slot.
        /// </summary>
        public virtual ItemShield GetShield() => GetOrInitializeItem(ItemSlots.LeftHand).GetData<ItemShield>();

        /// <summary>
        /// Returns the Item Bow of the item equipped on the right hand slot.
        /// </summary>
        public virtual ItemBow GetBow() => GetOrInitializeItem(ItemSlots.RightHand).GetData<ItemBow>();
    }
}
