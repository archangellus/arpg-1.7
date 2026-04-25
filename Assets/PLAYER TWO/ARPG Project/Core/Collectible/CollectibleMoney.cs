using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Collectible/Collectible Money")]
    public class CollectibleMoney : Collectible
    {
        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when the money drops.")]
        public AudioClip dropClip;

        [Tooltip("The Audio Clip that plays when collectible the money.")]
        public AudioClip collectClip;

        /// <summary>
        /// The amount of money on this Collectible.
        /// </summary>
        public int amount { get; set; }

        protected GameAudio m_audio => GameAudio.instance;

        public override string GetName() => amount.ToString();

        protected override bool TryCollect(Inventory inventory)
        {
            inventory.money += amount;
            m_audio.PlayEffect(collectClip);
            return true;
        }

        protected virtual void OnEnable() => m_audio.PlayEffect(dropClip);
    }
}
