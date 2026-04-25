using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public abstract class ItemWeapon : ItemEquippable
    {
        [Header("Weapon Settings")]
        [Tooltip("The base minimum damage of this Item.")]
        public int minDamage;

        [Tooltip("The base maximum damage of this Item.")]
        public int maxDamage;

        [Tooltip("The base attack speed of this Item.")]
        public int attackSpeed;

        [Tooltip("The list of audio clips this Item can play when used to perform attacks.")]
        public AudioClip[] attackClips;

        [Header("Combo Settings")]
        [Tooltip("The maximum number of combos this Weapon can perform.")]
        public int maxCombos = 3;

        [Tooltip("The time it takes to stop a combo it no new attack is performed.")]
        public float timeToStopCombo = 1f;

        [Tooltip("The time it takes to perform the next combo.")]
        public float nextComboDelay = 0.1f;

        /// <summary>
        /// Get a random damage based on the maximum and minimum base damage settings.
        /// </summary>
        public virtual int GetDamage() => Random.Range(minDamage, maxDamage);
    }
}
