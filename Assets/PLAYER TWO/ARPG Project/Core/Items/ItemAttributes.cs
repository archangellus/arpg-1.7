using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class ItemAttributes
    {
        /// <summary>
        /// Enumerates all attribute types that can be applied to an item.
        /// </summary>
        public enum AttributeType
        {
            Strength,
            Dexterity,
            Vitality,
            Energy,
            Damage,
            DamagePercent,
            MagicDamage,
            MagicDamagePercent,
            AttackSpeed,
            CriticalChancePercent,
            Defense,
            DefensePercent,
            Mana,
            ManaPercent,
            Health,
            HealthPercent,
            HealthRegeneration,
            ManaRegeneration,
            LightningDamage,
            FireDamage,
            IceDamage,
            PoisonDamage,
            LightningDamagePercent,
            FireDamagePercent,
            IceDamagePercent,
            PoisonDamagePercent,
            AllResistancesPercent,
            LightningResistPercent,
            FireResistPercent,
            IceResistPercent,
            PoisonResistPercent,
            ChanceToStunPercent,
            StunRecoveryPercent,
            ChanceOfBlockingPercent,
            BlockRecoveryPercent,
            SkillCoolDownPercent,
            Accuracy,
            AccuracyPercent,
            Evasion,
            EvasionPercent,
            HealthOnHit,
            ManaOnHit,
            HealthOnKill,
            ManaOnKill,
            HealthLeechPercent,
            ManaLeechPercent,
            DamageReflectionPercent,
            DamageToManaPercent,
            ChanceToBleedPercent,
            ChanceToBurnPercent,
            ChanceToFreezePercent,
            ChanceToPoisonPercent,
        }

        /// <summary>
        /// Represents a single attribute key-value pair, used for serialization.
        /// </summary>
        [System.Serializable]
        public class AttributeEntry
        {
            public AttributeType type;
            public int value;
        }

        protected Dictionary<AttributeType, int> m_values;

        // Convention-based maps: DamageType name + suffix → AttributeType.
        // Built once at startup — adding new elements only requires new enum values.
        static readonly Dictionary<DamageType, AttributeType> s_elementFlatDamageMap =
            BuildElementalMap("Damage");
        static readonly Dictionary<DamageType, AttributeType> s_elementPercentDamageMap =
            BuildElementalMap("DamagePercent");
        static readonly Dictionary<DamageType, AttributeType> s_elementResistMap =
            BuildElementalMap("ResistPercent");

        static Dictionary<DamageType, AttributeType> BuildElementalMap(string suffix)
        {
            var map = new Dictionary<DamageType, AttributeType>();

            foreach (DamageType damageType in System.Enum.GetValues(typeof(DamageType)))
            {
                var target = damageType.ToString() + suffix;

                foreach (AttributeType attr in System.Enum.GetValues(typeof(AttributeType)))
                {
                    if (attr.ToString() == target)
                    {
                        map[damageType] = attr;
                        break;
                    }
                }
            }

            return map;
        }

        public ItemAttributes()
        {
            m_values = new Dictionary<AttributeType, int>();

            foreach (AttributeType type in System.Enum.GetValues(typeof(AttributeType)))
                m_values[type] = 0;
        }

        /// <summary>
        /// Gets or sets the value for the given attribute type.
        /// </summary>
        public int this[AttributeType type]
        {
            get => m_values[type];
            set => m_values[type] = value;
        }

        protected virtual string GetAttributeText(AttributeType type, int value)
        {
            var name = type.ToString();

            if (name.EndsWith("ResistPercent"))
            {
                var elementName = SplitPascalCase(name.Replace("ResistPercent", ""));
                return $"Reduces {elementName} damage taken by {value}%";
            }

            if (
                name.EndsWith("DamagePercent")
                && type != AttributeType.DamagePercent
                && type != AttributeType.MagicDamagePercent
            )
            {
                var elementName = SplitPascalCase(name.Replace("DamagePercent", ""));
                return $"Increases {elementName} Damage by +{value}%";
            }

            if (
                name.EndsWith("Damage")
                && type != AttributeType.Damage
                && type != AttributeType.MagicDamage
            )
                return $"+{value} to {SplitPascalCase(name)}";

            if (name.EndsWith("OnHit") || name.EndsWith("OnKill"))
                return $"+{value} {SplitPascalCase(name)}";

            if (type == AttributeType.SkillCoolDownPercent)
                return $"Reduces Skill Cooldown by {value}%";

            if (name.EndsWith("Percent"))
            {
                name = SplitPascalCase(name.Replace("Percent", ""));
                return $"Increases {name} by +{value}%";
            }

            return $"+{value} of Additional {SplitPascalCase(name)}";
        }

        public virtual float GetDamageMultiplier() => 1f + this[AttributeType.DamagePercent] / 100f;

        public virtual float GetMagicDamageMultiplier() =>
            1f + this[AttributeType.MagicDamagePercent] / 100f;

        public virtual float GetCriticalMultiplier() =>
            1f + this[AttributeType.CriticalChancePercent] / 100f;

        public virtual float GetDefenseMultiplier() =>
            1f + this[AttributeType.DefensePercent] / 100f;

        public virtual float GetManaMultiplier() => 1f + this[AttributeType.ManaPercent] / 100f;

        public virtual float GetHealthMultiplier() => 1f + this[AttributeType.HealthPercent] / 100f;

        public virtual float GetSkillCoolDownMultiplier() =>
            1f - this[AttributeType.SkillCoolDownPercent] / 100f;

        /// <summary>
        /// Returns the flat elemental damage bonus for the given damage type
        /// (e.g. <see cref="AttributeType.LightningDamage"/>).
        /// </summary>
        public virtual int GetElementalFlatDamageBonus(DamageType type) =>
            s_elementFlatDamageMap.TryGetValue(type, out var attr) ? m_values[attr] : 0;

        /// <summary>
        /// Returns the elemental damage multiplier for the given type as a factor
        /// (e.g. 30% bonus → 1.30).
        /// </summary>
        public virtual float GetElementalDamageMultiplier(DamageType type) =>
            1f
            + (s_elementPercentDamageMap.TryGetValue(type, out var attr) ? m_values[attr] : 0)
                / 100f;

        /// <summary>
        /// Returns the elemental resistance as a damage multiplier for the given type,
        /// combining the per-element resist and <see cref="AttributeType.AllResistancesPercent"/>
        /// (e.g. 20% element + 10% all → 0.70).
        /// </summary>
        public virtual float GetElementalResistMultiplier(DamageType type)
        {
            var elementResist = s_elementResistMap.TryGetValue(type, out var attr)
                ? m_values[attr]
                : 0;
            var allResist = m_values[AttributeType.AllResistancesPercent];
            return 1f - (elementResist + allResist) / 100f;
        }

        public virtual int GetAttributesCount()
        {
            var count = 0;

            foreach (var value in m_values.Values)
            {
                if (value > 0)
                    count++;
            }

            return count;
        }

        public virtual string Inspect()
        {
            var text = "";

            foreach (AttributeType type in System.Enum.GetValues(typeof(AttributeType)))
            {
                var value = m_values[type];

                if (value <= 0)
                    continue;

                if (text.Length > 0)
                    text += "\n";

                text += GetAttributeText(type, value);
            }

            return text;
        }

        /// <summary>
        /// Applies an affix entry to this instance by rolling a biased random value within
        /// each attribute's allowed range and adding it to the corresponding entry.
        /// </summary>
        /// <param name="entry">The affix entry to apply.</param>
        /// <param name="valueWeight">
        /// Bias applied to the roll: 0 leans toward <c>minValue</c>, 0.5 gives a balanced
        /// full-range roll, 1 leans toward <c>maxValue</c>.
        /// </param>
        public virtual void Apply(ItemAffixes.AffixEntry entry, float valueWeight = 0.5f)
        {
            if (entry == null)
                return;

            foreach (var attr in entry.attributes)
            {
                float t = Mathf.Clamp01(valueWeight + (Random.value - 0.5f));
                m_values[attr.type] += Mathf.RoundToInt(
                    Mathf.Lerp(attr.minValue, attr.maxValue, t)
                );
            }
        }

        /// <summary>
        /// Creates an ItemAttributes instance from an array of serialized attribute entries.
        /// </summary>
        public static ItemAttributes CreateFromSerializer(AttributeEntry[] entries)
        {
            var instance = new ItemAttributes();

            if (entries == null)
                return instance;

            foreach (var entry in entries)
                instance[entry.type] = entry.value;

            return instance;
        }

        /// <summary>
        /// Creates an ItemAttributes instance with values accumulated from all non-null items.
        /// </summary>
        public static ItemAttributes Accumulate(ItemInstance[] items)
        {
            var result = new ItemAttributes();

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                foreach (AttributeType type in System.Enum.GetValues(typeof(AttributeType)))
                    result[type] += item.GetAttribute(type);
            }

            return result;
        }

        private static string SplitPascalCase(string name)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1]))
                    sb.Append(' ');

                sb.Append(name[i]);
            }

            return sb.ToString();
        }
    }
}
