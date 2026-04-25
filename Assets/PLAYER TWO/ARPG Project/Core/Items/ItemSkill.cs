using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Skill", menuName = "PLAYER TWO/ARPG Project/Item/Skill")]
    public class ItemSkill : Item
    {
        [Header("Skill Book Settings")]
        [Tooltip("The Skill this book holds.")]
        public Skill skill;

        [Tooltip("The minimum level of the Entity to learn this Skill.")]
        public int requiredLevel;

        [Tooltip("The minimum strength of the Entity to learn this Skill.")]
        public int requiredStrength;

        [Tooltip("The minimum energy of the Entity to learn this Skill.")]
        public int requiredEnergy;
    }
}
