using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public partial class ItemInstance
    {
        /// <summary>
        /// Invoked when the durability changed.
        /// </summary>
        public System.Action onChanged;

        /// <summary>
        /// Invoked when the stack size changed.
        /// </summary>
        public System.Action onStackChanged;

        /// <summary>
        /// Invoked when the durability points reach zero.
        /// </summary>
        public System.Action onBreak;

        /// <summary>
        /// The Item data that represents this Item Instance.
        /// </summary>
        public Item data;

        /// <summary>
        /// The additional attributes of this Item Instance.
        /// </summary>
        public ItemAttributes attributes;

        /// <summary>
        /// The index of the rarity tier in the Game Database's item rarities list.
        /// A value of -1 means no rarity (plain item with no affixes).
        /// </summary>
        public int rarityId = -1;

        /// <summary>
        /// Indices of selected prefix affixes from the item's affix scope.
        /// </summary>
        public List<int> prefixIndices;

        /// <summary>
        /// Indices of selected suffix affixes from the item's affix scope.
        /// </summary>
        public List<int> suffixIndices;

        protected int m_stack;

        /// <summary>
        /// The current durability points of this Item Instance.
        /// </summary>
        public int durability { get; protected set; }

        /// <summary>
        /// The size of the item stack.
        /// </summary>
        public int stack
        {
            get { return m_stack; }
            set
            {
                if (!IsStackable())
                    return;

                m_stack = Mathf.Clamp(value, 0, data.stackCapacity);
                onStackChanged?.Invoke();
            }
        }

        /// <summary>
        /// The amount of rows this Item Instance takes on the Inventory.
        /// </summary>
        public int rows => data.rows;

        /// <summary>
        /// The amount of columns this Item Instance takes on the Inventory.
        /// </summary>
        public int columns => data.columns;

        /// <summary>
        /// Creates an Item Instance with no affixes.
        /// </summary>
        public ItemInstance(Item data)
        {
            SetDefaultData(data);
        }

        /// <summary>
        /// Creates an Item Instance and rolls affixes based on a rarity level from the Game Database.
        /// </summary>
        public ItemInstance(Item data, int rarityId)
        {
            SetDefaultData(data);
            GenerateAttributesFromRarity(rarityId);
        }

        /// <summary>
        /// Creates an Item Instance and rolls affixes based on a direct ItemRarity reference.
        /// </summary>
        public ItemInstance(Item data, ItemRarity rarity)
        {
            SetDefaultData(data);
            var id = GameDatabase.instance.itemRarities.IndexOf(rarity);
            if (id >= 0)
                GenerateAttributesFromRarity(id);
        }

        /// <summary>
        /// Creates an Item Instance with pre-existing attributes and default durability.
        /// </summary>
        public ItemInstance(Item data, ItemAttributes attributes)
        {
            this.data = data;
            this.attributes = attributes;

            if (IsEquippable())
                durability = GetEquippable().maxDurability;

            if (IsStackable())
                stack = 1;
        }

        /// <summary>
        /// Creates an Item Instance with pre-existing attributes and explicit durability and stack.
        /// </summary>
        public ItemInstance(Item data, ItemAttributes attributes, int durability, int stack)
        {
            this.data = data;
            this.attributes = attributes;
            this.durability = durability;
            SetStackOrDefault(stack);
        }

        /// <summary>
        /// Creates an Item Instance from fully explicit save data.
        /// </summary>
        public ItemInstance(
            Item data,
            ItemAttributes attributes,
            int durability,
            int stack,
            int rarityId,
            List<int> prefixIndices,
            List<int> suffixIndices
        )
        {
            this.data = data;
            this.attributes = attributes;
            this.durability = durability;
            SetStackOrDefault(stack);
            this.rarityId = rarityId;
            this.prefixIndices = prefixIndices;
            this.suffixIndices = suffixIndices;
        }

        /// <summary>
        /// Tries to stack another item on the stack.
        /// </summary>
        /// <param name="other">The Item Instance you want to try stack.</param>
        /// <returns>Returns true if it was able to stack the item.</returns>
        public virtual bool TryStack(ItemInstance other)
        {
            if (!CanStack(other))
                return false;

            stack += other.stack;
            return true;
        }

        /// <summary>
        /// Creates a separate Item Instance with the same item data, durability, rarity, affixes,
        /// and attributes, using the requested stack size when the item can stack.
        /// </summary>
        /// <param name="stack">The stack size for the copied Item Instance.</param>
        public virtual ItemInstance CopyWithStack(int stack)
        {
            return new ItemInstance(
                data,
                attributes != null ? new ItemAttributes(attributes) : null,
                durability,
                stack,
                rarityId,
                prefixIndices != null ? new List<int>(prefixIndices) : null,
                suffixIndices != null ? new List<int>(suffixIndices) : null
            );
        }

        /// <summary>
        /// Returns the required minimum level to equip this Item.
        /// </summary>
        public virtual int GetRequiredLevel()
        {
            if (IsEquippable())
                return GetEquippable().requiredLevel;
            if (IsSkill())
                return GetSkill().requiredLevel;

            return 0;
        }

        /// <summary>
        /// Returns the required minimum strength to equip this Item.
        /// </summary>
        public virtual int GetRequiredStrength()
        {
            if (IsEquippable())
                return GetEquippable().requiredStrength;
            if (IsSkill())
                return GetSkill().requiredStrength;

            return 0;
        }

        /// <summary>
        /// Returns the required minimum dexterity to equip this Item.
        /// </summary>
        public virtual int GetRequiredDexterity()
        {
            if (IsEquippable())
                return GetEquippable().requiredDexterity;

            return 0;
        }

        /// <summary>
        /// Returns the required minimum energy to equip this Item.
        /// </summary>
        public virtual int GetRequiredEnergy()
        {
            if (IsSkill())
                return GetSkill().requiredEnergy;

            return 0;
        }

        /// <summary>
        /// Returns true if this Item Instance can stack another given Item Instance.
        /// </summary>
        /// <param name="other">The Item Instance you want to check.</param>
        public virtual bool CanStack(ItemInstance other) =>
            IsStackable() && other.data == data && stack + other.stack <= data.stackCapacity;

        /// <summary>
        /// Returns true if the durability points of this Item Instance is zero.
        /// </summary>
        public virtual bool IsBroken() => durability == 0;

        /// <summary>
        /// Returns true if the durability of this Item Instance is at half.
        /// </summary>
        public virtual bool IsAboutToBreak()
        {
            if (!IsEquippable())
                return false;

            return durability <= GetEffectiveMaxDurability() / 2f;
        }

        /// <summary>
        /// Returns true if this Item Instance has additional attributes.
        /// </summary>
        public virtual bool ContainAttributes() => IsEquippable() && attributes != null;

        /// <summary>
        /// Returns true if it's allowed to read the additional attributes from this Item Instance.
        /// </summary>
        public virtual bool UseAttributes() => ContainAttributes() && !IsBroken();

        /// <summary>
        /// Returns the value of the given attribute type, or 0 if attributes are not active.
        /// </summary>
        public virtual int GetAttribute(ItemAttributes.AttributeType type) =>
            UseAttributes() ? attributes[type] : 0;

        /// <summary>
        /// Reduces the durability of this Item Instance by a given amount.
        /// </summary>
        /// <param name="amount">The amount of points to decrease from the durability.</param>
        public virtual void ApplyDamage(int amount)
        {
            if (!IsEquippable())
                return;

            var maxDurability = GetEffectiveMaxDurability();
            durability = Mathf.Clamp(durability - amount, 0, maxDurability);

            if (durability <= 0)
                onBreak?.Invoke();

            onChanged?.Invoke();
        }

        /// <summary>
        /// Returns the minimum and maximum damage of this Item Instance. If the Item is broken or if its
        /// not a Weapon, the damage will always be zero. If it's about to break, the damage is reduced by half.
        /// </summary>
        public virtual MinMax GetDamage()
        {
            if (!IsWeapon() || IsBroken())
                return MinMax.Zero;

            var rarity = GetRarity();
            var damageBonus = rarity != null ? rarity.bonusDamage : 0;
            var minDamage = GetWeapon().minDamage + damageBonus;
            var maxDamage = GetWeapon().maxDamage + damageBonus;

            if (IsAboutToBreak())
                return new((int)(minDamage / 2f), (int)(maxDamage / 2f));

            return new(minDamage, maxDamage);
        }

        /// <summary>
        /// Returns the defense points of this Item Instance. If it's broken, the defense is zero.
        /// If the Item Instance is about to break, the defense is reduced by half.
        /// </summary>
        public virtual int GetDefense()
        {
            if (IsBroken())
                return 0;

            var rarity = GetRarity();
            var defenseBonus = rarity != null ? rarity.bonusDefense : 0;
            var defense = 0;

            if (IsArmor())
                defense = GetArmor().defense + defenseBonus;
            else if (IsShield())
                defense = GetShield().defense + defenseBonus;

            return IsAboutToBreak() ? (int)(defense / 2f) : defense;
        }

        /// <summary>
        /// Sets this Item Instance durability to its maximum points.
        /// </summary>
        public virtual void Repair()
        {
            if (!IsEquippable())
                return;

            durability = GetEffectiveMaxDurability();
            onChanged?.Invoke();
        }

        /// <summary>
        /// Returns the current durability in a rate of zero to one.
        /// </summary>
        public virtual float GetDurabilityRate()
        {
            if (!IsEquippable())
                return 1;

            return durability / (float)GetEffectiveMaxDurability();
        }

        /// <summary>
        /// Returns the display name of this Item Instance, incorporating affix naming rules.
        /// In <see cref="ItemRarity.AffixesMode.Paired"/> mode, a prefix appears before the item name and a
        /// suffix appears after as "of [name]". In <see cref="ItemRarity.AffixesMode.Layered"/> mode, the
        /// rarity display name is prepended instead (e.g. "Rare Iron Sword").
        /// </summary>
        public virtual string GetDisplayName()
        {
            var db = GameDatabase.instance;

            if (rarityId < 0 || !db.itemRarities.IsIndexValid(rarityId))
                return data.name;

            var rarity = db.itemRarities[rarityId];

            if (rarity.affixesMode == ItemRarity.AffixesMode.Layered)
                return $"{rarity.displayName} {data.name}";

            var affixes = rarity.affixes;

            if (affixes == null)
                return data.name;

            var prefix =
                prefixIndices?.Count > 0 && affixes.prefixes.IsIndexValid(prefixIndices[0])
                    ? $"{affixes.prefixes[prefixIndices[0]].name} "
                    : "";
            var suffix =
                suffixIndices?.Count > 0 && affixes.suffixes.IsIndexValid(suffixIndices[0])
                    ? $" {affixes.suffixes[suffixIndices[0]].name}"
                    : "";

            return $"{prefix}{data.name}{suffix}".Trim();
        }

        /// <summary>
        /// Returns the rarity color for this Item Instance, or the fallback color if no rarity is set.
        /// </summary>
        /// <param name="fallback">The color to use when no rarity is assigned.</param>
        public virtual Color GetRarityColor(Color fallback)
        {
            var db = GameDatabase.instance;

            if (rarityId >= 0 && db.itemRarities.IsIndexValid(rarityId))
                return db.itemRarities[rarityId].color;

            return fallback;
        }

        /// <summary>
        /// Returns the Item Rarity for this Item Instance, or null if no rarity is assigned.
        /// </summary>
        public virtual ItemRarity GetRarity()
        {
            var db = GameDatabase.instance;

            if (rarityId >= 0 && db.itemRarities.IsIndexValid(rarityId))
                return db.itemRarities[rarityId];

            return null;
        }

        /// <summary>
        /// Returns the effective maximum durability, including any flat bonus from the item's rarity.
        /// </summary>
        public virtual int GetEffectiveMaxDurability()
        {
            if (!IsEquippable())
                return 0;

            var rarity = GetRarity();
            return GetEquippable().maxDurability + (rarity != null ? rarity.bonusMaxDurability : 0);
        }

        /// <summary>
        /// Returns the effective attack speed of this weapon, including any flat bonus from the item's rarity.
        /// </summary>
        public virtual int GetEffectiveAttackSpeed()
        {
            if (!IsWeapon())
                return 0;

            var rarity = GetRarity();
            return GetWeapon().attackSpeed + (rarity != null ? rarity.bonusAttackSpeed : 0);
        }

        /// <summary>
        /// Returns the effective chance to block as a 0–1 value, including any flat bonus from the item's rarity.
        /// </summary>
        public virtual float GetEffectiveChanceToBlock()
        {
            if (!IsShield())
                return 0;

            var rarity = GetRarity();
            return (GetShield().chanceToBlock + (rarity != null ? rarity.bonusChanceToBlock : 0))
                / 100f;
        }

        /// <summary>
        /// Returns the selling price of this Item Instance.
        /// </summary>
        public virtual int GetSellPrice() => (int)(GetPrice() / 2f);

        /// <summary>
        /// Returns the price of this Item Instance. If it's a stack, the price is multiplied
        /// by the stack size. The durability rate of the Item Instance is multiplied by its final price.
        /// </summary>
        public virtual int GetPrice()
        {
            var price = data.price;

            if (IsStackable())
                price *= stack;

            if (IsEquippable())
            {
                if (attributes != null)
                {
                    var totalAttr = attributes.GetAttributesCount();
                    price += totalAttr * Game.instance.pricePerAttribute;
                }

                price = (int)(price * GetDurabilityRate());
            }

            return price;
        }

        protected string InspectRequired(
            string name,
            int required,
            int current,
            Color error,
            bool breakLine
        )
        {
            var lineBreak = breakLine ? "\n" : "";
            var attr = $"Required {name}: {required}";

            if (current < required)
                return lineBreak + attr.WithColor(error);

            return lineBreak + attr;
        }

        /// <summary>
        /// Returns a string with the Item's general attributes.
        /// </summary>
        /// <param name="stats">The Entity Stats to compare against.</param>
        /// <param name="warning">The color of warning texts.</param>
        /// <param name="error">The color of the error texts.</param>
        /// <param name="special">The color used for values boosted by rarity.</param>
        public virtual string Inspect(
            EntityStatsManager stats,
            Color warning,
            Color error,
            Color special
        )
        {
            var text = "";
            var rarity = GetRarity();

            if (IsArmor())
            {
                var defense = GetArmor().defense + (rarity != null ? rarity.bonusDefense : 0);
                var defenseStr =
                    rarity != null && rarity.bonusDefense > 0
                        ? $"{defense}".WithColor(special)
                        : $"{defense}";
                text += $"Defense: {defenseStr}";
            }
            else if (IsShield())
            {
                var defense = GetShield().defense + (rarity != null ? rarity.bonusDefense : 0);
                var chanceToBlock =
                    GetShield().chanceToBlock + (rarity != null ? rarity.bonusChanceToBlock : 0);
                var defenseStr =
                    rarity != null && rarity.bonusDefense > 0
                        ? $"{defense}".WithColor(special)
                        : $"{defense}";
                var chanceToBlockStr =
                    rarity != null && rarity.bonusChanceToBlock > 0
                        ? $"{chanceToBlock}%".WithColor(special)
                        : $"{chanceToBlock}%";
                text += $"Defense: {defenseStr}";
                text += $"\nChance To Block: {chanceToBlockStr}";
            }
            else if (IsWeapon())
            {
                var damageBonus = rarity != null ? rarity.bonusDamage : 0;
                var minDamage = GetWeapon().minDamage + damageBonus;
                var maxDamage = GetWeapon().maxDamage + damageBonus;
                var attackSpeed =
                    GetWeapon().attackSpeed + (rarity != null ? rarity.bonusAttackSpeed : 0);
                var damageStr =
                    damageBonus > 0
                        ? $"{minDamage} ~ {maxDamage}".WithColor(special)
                        : $"{minDamage} ~ {maxDamage}";
                var attackSpeedStr =
                    rarity != null && rarity.bonusAttackSpeed > 0
                        ? $"{attackSpeed}".WithColor(special)
                        : $"{attackSpeed}";
                text += $"Damage: {damageStr}";
                text += $"\nAttack Speed: {attackSpeedStr}";
            }

            if (IsEquippable())
            {
                var lineBreak = text.Length > 0 ? "\n" : "";
                var maxDurability = GetEffectiveMaxDurability();
                var durabilityValues = $"{durability} of {maxDurability}";
                var hasSpecialDurability = rarity != null && rarity.bonusMaxDurability > 0;

                if (IsAboutToBreak())
                    text += lineBreak + $"Durability: {durabilityValues}".WithColor(warning);
                else if (IsBroken())
                    text += lineBreak + $"Durability: {durabilityValues}".WithColor(error);
                else if (hasSpecialDurability)
                    text += lineBreak + $"Durability: {durabilityValues.WithColor(special)}";
                else
                    text += lineBreak + $"Durability: {durabilityValues}";
            }

            if (GetRequiredLevel() > 1)
                text += InspectRequired(
                    "Level",
                    GetRequiredLevel(),
                    stats.level,
                    error,
                    text.Length > 0
                );

            if (GetRequiredStrength() > 0)
                text += InspectRequired(
                    "Strength",
                    GetRequiredStrength(),
                    stats.strength,
                    error,
                    text.Length > 0
                );

            if (GetRequiredDexterity() > 0)
                text += InspectRequired(
                    "Dexterity",
                    GetRequiredDexterity(),
                    stats.dexterity,
                    error,
                    text.Length > 0
                );

            if (GetRequiredEnergy() > 0)
                text += InspectRequired(
                    "Energy",
                    GetRequiredEnergy(),
                    stats.energy,
                    error,
                    text.Length > 0
                );

            return text;
        }

        protected virtual void SetDefaultData(Item data)
        {
            this.data = data;

            if (IsEquippable())
                durability = GetEquippable().maxDurability;

            SetStackOrDefault(1);
        }

        protected virtual void SetStackOrDefault(int stack)
        {
            if (!IsStackable())
                return;

            this.stack = stack > 0 ? stack : 1;
        }

        /// <summary>
        /// Returns the <see cref="ItemAffixes.AffixScope"/> flag that corresponds to this item's type.
        /// For armor, the specific slot (Helm, Chest, Pants, Gloves, Boots) is returned.
        /// Returns <see cref="ItemAffixes.AffixScope.None"/> when the item type has no affix scope.
        /// </summary>
        protected virtual ItemAffixes.AffixScope GetItemAffixScope()
        {
            if (IsBlade())
                return ItemAffixes.AffixScope.Blade;
            if (IsBow())
                return ItemAffixes.AffixScope.Bow;
            if (IsArmor())
                return GetArmor().slot switch
                {
                    ItemSlots.Helm => ItemAffixes.AffixScope.Helm,
                    ItemSlots.Chest => ItemAffixes.AffixScope.Chest,
                    ItemSlots.Pants => ItemAffixes.AffixScope.Pants,
                    ItemSlots.Gloves => ItemAffixes.AffixScope.Gloves,
                    ItemSlots.Boots => ItemAffixes.AffixScope.Boots,
                    _ => ItemAffixes.AffixScope.None,
                };
            if (IsShield())
                return ItemAffixes.AffixScope.Shield;
            if (IsRing())
                return ItemAffixes.AffixScope.Ring;
            if (IsAmulet())
                return ItemAffixes.AffixScope.Amulet;

            return ItemAffixes.AffixScope.None;
        }

        /// <summary>
        /// Randomly generates additional attributes for this Item Instance based on a rarity level.
        /// The affix selection strategy is determined by the rarity's <see cref="ItemRarity.AffixesMode"/>.
        /// Logs a warning and skips generation if the rarity level is out of bounds.
        /// </summary>
        protected virtual void GenerateAttributesFromRarity(int level)
        {
            if (!IsEquippable())
                return;

            var db = GameDatabase.instance;

            if (!db.itemRarities.IsIndexValid(level))
            {
                Debug.LogWarning(
                    $"ItemInstance: rarityId {level} is out of bounds in the Game Database. "
                        + "No attributes will be generated."
                );
                return;
            }

            var rarity = db.itemRarities[level];

            if (rarity.affixes == null)
                return;

            var itemScope = GetItemAffixScope();
            rarity.RollAffixIndices(
                itemScope,
                out var rolledPrefixIndices,
                out var rolledSuffixIndices
            );

            if (rolledPrefixIndices.Count == 0 && rolledSuffixIndices.Count == 0)
                return;

            rarityId = level;
            durability = GetEffectiveMaxDurability();
            attributes = new ItemAttributes();
            prefixIndices = rolledPrefixIndices;
            suffixIndices = rolledSuffixIndices;

            foreach (var i in prefixIndices)
                attributes.Apply(rarity.affixes.prefixes[i], rarity.valueWeight);

            foreach (var i in suffixIndices)
                attributes.Apply(rarity.affixes.suffixes[i], rarity.valueWeight);
        }

        /// <summary>
        /// Returns a new Item Instance from the Item Serializer.
        /// </summary>
        /// <param name="serializer">The Item Serializer to create the Item Instance from.</param>
        public static ItemInstance CreateFromSerializer(ItemSerializer serializer)
        {
            if (serializer == null || serializer.itemId < 0)
                return null;

            var item = GameDatabase.instance.FindElementById<Item>(serializer.itemId);
            var attributes = ItemAttributes.CreateFromSerializer(serializer.attributes);
            var prefixIndices =
                serializer.prefixIndices != null ? new List<int>(serializer.prefixIndices) : null;
            var suffixIndices =
                serializer.suffixIndices != null ? new List<int>(serializer.suffixIndices) : null;

            return new ItemInstance(
                item,
                attributes,
                serializer.durability,
                serializer.stack,
                serializer.rarityId,
                prefixIndices,
                suffixIndices
            );
        }
    }
}
