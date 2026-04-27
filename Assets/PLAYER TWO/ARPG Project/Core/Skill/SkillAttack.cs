using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Skill Attack", menuName = "PLAYER TWO/ARPG Project/Skills/Skill Attack")]
    public class SkillAttack : Skill
    {
        public enum DamageMode { Regular, Magic }

        public enum RequiredWeapon { None, Blade, Bow }

        [Header("Attack Settings")]
        [Tooltip("If true, the Skill will active the regular melee hitbox when casted.")]
        public bool useMeleeHitbox;

        [Tooltip("The minimum distance to a target to use this Skill.")]
        public float minAttackDistance;

        [Tooltip("The required equipped weapon type to perform the Skill.")]
        public RequiredWeapon requiredWeapon;

        [Header("Damage Settings")]
        [Tooltip("The damage mode of this Skill.")]
        public DamageMode damageMode = DamageMode.Magic;

        [Tooltip("The minimum damage this Skill can cause.")]
        public int minDamage;

        [Tooltip("The maximum damage this Skill can cause.")]
        public int maxDamage;

        /// <summary>
        /// Returns a random value between the minimum and maximum damage of this Skill.
        /// </summary>
        public virtual int GetDamage() => Random.Range(minDamage, maxDamage);
    }
}
