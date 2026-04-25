using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Fountain")]
    public class Fountain : Interactive
    {
        [Header("Fountain Settings")]
        [Tooltip("If true, restores the Entity health points on interacting.")]
        public bool resetHealth;

        [Tooltip("If true, restores the Entity mana points on interacting.")]
        public bool resetMana;

        [Tooltip("The Game Object that represents the content of the fountain.")]
        public GameObject content;

        protected bool m_canUse = true;

        protected override void OnInteract(object other)
        {
            if (other is not Entity || !m_canUse)
                return;

            if (resetHealth)
                (other as Entity).stats.ResetHealth();

            if (resetMana)
                (other as Entity).stats.ResetMana();

            m_canUse = false;
            content.SetActive(false);
        }
    }
}
