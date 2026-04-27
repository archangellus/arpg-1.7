namespace PLAYERTWO.ARPGProject
{
    public partial class ItemInstance
    {
        /// <summary>
        /// Returns the data of this Item Instance casted to a given type.
        /// </summary>
        public virtual T GetData<T>() where T : Item => data as T;

        /// <summary>
        /// Returns the data of this Item Instance as an Item Equippable.
        /// </summary>
        public virtual ItemEquippable GetEquippable() => GetData<ItemEquippable>();

        /// <summary>
        /// Returns the data of this Item Instance as an Item Skill.
        /// </summary>
        public virtual ItemSkill GetSkill() => GetData<ItemSkill>();

        /// <summary>
        /// Returns the data of this Item Instance as an Item Armor.
        /// </summary>
        public virtual ItemArmor GetArmor() => GetData<ItemArmor>();

        /// <summary>
        /// Returns the data of this Item Instance as an Item Consumable.
        /// </summary>
        public virtual ItemConsumable GetConsumable() => GetData<ItemConsumable>();

        /// <summary>
        /// Returns the data of this Item Instance as an Item Potion.
        /// </summary>
        public virtual ItemPotion GetPotion() => GetData<ItemPotion>();

        /// <summary>
        /// Returns the data of this Item Instance as an Item Weapon.
        /// </summary>
        public virtual ItemWeapon GetWeapon() => GetData<ItemWeapon>();

        /// <summary>
        /// Returns the data of this Item Instance as an Item Shield.
        /// </summary>
        public virtual ItemShield GetShield() => GetData<ItemShield>();

        /// <summary>
        /// Returns the data of this Item Instance as an Item Blade.
        /// </summary>
        public virtual ItemBlade GetBlade() => GetData<ItemBlade>();

        /// <summary>
        /// Returns the data of this Item Instance as an Item Bow.
        /// </summary>
        public virtual ItemBow GetBow() => GetData<ItemBow>();

        /// <summary>
        /// Returns true if this Item allows stacking.
        /// </summary>
        public virtual bool IsStackable() => data.canStack;

        /// <summary>
        /// Returns true if this Item is equippable.
        /// </summary>
        public virtual bool IsEquippable() => data is ItemEquippable;

        /// <summary>
        /// Returns true if this Item represents a Skill.
        /// </summary>
        public virtual bool IsSkill() => data is ItemSkill;

        /// <summary>
        /// Returns true if this Item is an Armor.
        /// </summary>
        public virtual bool IsArmor() => data is ItemArmor;

        /// <summary>
        /// Returns true if this Item is Consumable.
        /// </summary>
        public virtual bool IsConsumable() => data is ItemConsumable;

        /// <summary>
        /// Returns true if this Item is a Potion.
        /// </summary>
        public virtual bool IsPotion() => data is ItemPotion;

        /// <summary>
        /// Returns true if this Item is a Weapon.
        /// </summary>
        public virtual bool IsWeapon() => data is ItemWeapon;

        /// <summary>
        /// Returns true if this Item is a Shield.
        /// </summary>
        public virtual bool IsShield() => data is ItemShield;

        /// <summary>
        /// Returns true if this Item is a Blade.
        /// </summary>
        public virtual bool IsBlade() => data is ItemBlade;

        /// <summary>
        /// Returns true if this Item is a Bow.
        /// </summary>
        public virtual bool IsBow() => data is ItemBow;
    }
}
