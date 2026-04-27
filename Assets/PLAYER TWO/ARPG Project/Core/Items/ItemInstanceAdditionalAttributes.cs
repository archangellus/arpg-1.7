namespace PLAYERTWO.ARPGProject
{
    public partial class ItemInstance
    {
        /// <summary>
        /// Returns the amount of additional attributes.
        /// </summary>
        public virtual int GetAttributesCount() => ContainAttributes() ? attributes.GetAttributesCount() : 0;

        /// <summary>
        /// Returns the additional damage points.
        /// </summary>
        public virtual int GetAdditionalDamage() => UseAttributes() ? attributes.damage : 0;

        /// <summary>
        /// Returns the additional attack speed points.
        /// </summary>
        public virtual int GetAttackSpeed() => UseAttributes() ? attributes.attackSpeed : 0;

        /// <summary>
        /// Returns the additional defense points.
        /// </summary>
        public virtual int GetAdditionalDefense() => UseAttributes() ? attributes.defense : 0;

        /// <summary>
        /// Returns the additional mana points.
        /// </summary>
        public virtual int GetAdditionalMana() => UseAttributes() ? attributes.mana : 0;

        /// <summary>
        /// Returns the additional health points.
        /// </summary>
        public virtual int GetAdditionalHealth() => UseAttributes() ? attributes.health : 0;

        /// <summary>
        /// Returns the additional damage multiplier.
        /// </summary>
        public virtual float GetDamageMultiplier() => UseAttributes() ? attributes.GetDamageMultiplier() : 0;

        /// <summary>
        /// Returns the additional critical chance multiplier.
        /// </summary>
        public virtual float GetCriticalChanceMultiplier() => UseAttributes() ? attributes.GetCriticalMultiplier() : 0;

        /// <summary>
        /// Returns the additional defense multiplier.
        /// </summary>
        public virtual float GetDefenseMultiplier() => UseAttributes() ? attributes.GetDefenseMultiplier() : 0;

        /// <summary>
        /// Returns the additional mana multiplier.
        /// </summary>
        public virtual float GetManaMultiplier() => UseAttributes() ? attributes.GetManaMultiplier() : 0;

        /// <summary>
        /// Returns the additional health multiplier.
        /// </summary>
        public virtual float GetHealthMultiplier() => UseAttributes() ? attributes.GetHealthMultiplier() : 0;
    }
}
