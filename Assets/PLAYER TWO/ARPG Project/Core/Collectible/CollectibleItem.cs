using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Collectible/Collectible Item")]
    public class CollectibleItem : Collectible
    {
        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when the Collectible is dropped.")]
        public AudioClip dropRegularClip;

        [Tooltip("The Audio Clip that plays when the Collectible is dropped with an Item Armor.")]
        public AudioClip dropArmorClip;

        [Tooltip("The Audio Clip that plays when the Collectible is dropped with an Item Weapon.")]
        public AudioClip dropWeaponClip;

        [Tooltip("The Audio Clip that plays when collecting.")]
        public AudioClip collectRegularClip;

        [Tooltip("The Audio Clip that plays when collecting an Item Armor.")]
        public AudioClip collectArmorClip;

        [Tooltip("The Audio Clip that plays when collecting an Item Weapon.")]
        public AudioClip collectWeaponClip;

        /// <summary>
        /// Returns the Item Instance of this Collectible.
        /// </summary>
        public ItemInstance item { get; protected set; }

        protected GameAudio m_audio => GameAudio.instance;

        /// <summary>
        /// Sets the Item Instance of this Collectible Item.
        /// </summary>
        /// <param name="item">The Item Instance you want to set to this Collectible.</param>
        public virtual void SetItem(ItemInstance item)
        {
            this.item = item;

            var position = transform.position + item.data.dropPosition;
            var rotation = Quaternion.Euler(item.data.dropRotation);

            Instantiate(this.item.data.prefab, position, rotation, transform);
            PlayClip(dropRegularClip, dropArmorClip, dropWeaponClip);
            // >>> PLUGIN_PATCH:ItemRarity::FIND:PlayClip(dropRegularClip, dropArmorClip, dropWeaponClip);|R5_d9beebf8
            EventBus.RaiseRarityItemSet(this);
            // <<< PLUGIN_PATCH:ItemRarity::FIND:PlayClip(dropRegularClip, dropArmorClip, dropWeaponClip);|R5_d9beebf8
                                }

                                // >>> PLUGIN_PATCH:ItemRarity::FIND:public override string GetName() => item.data.name;|R5_69827081
                                // __PLUGIN_REPLACE_ORIGINAL:ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICBwdWJsaWMgb3ZlcnJpZGUgc3RyaW5nIEdldE5hbWUoKSA9PiBpdGVtLmRhdGEubmFtZTsNCg==
                                public override string GetName() => item.GetDisplayName();
                                // <<< PLUGIN_PATCH:ItemRarity::FIND:public override string GetName() => item.data.name;|R5_69827081

        protected override bool TryCollect(Inventory inventory)
        {
            if (inventory.TryAddOrStack(item))
            {
                PlayClip(collectRegularClip, collectArmorClip, collectWeaponClip);
                return true;
            }

            m_audio.PlayDeniedSound();
            return false;
        }

        protected virtual void PlayClip(AudioClip regular, AudioClip armor, AudioClip weapon)
        {
            if (Level.TimeSinceLevelStart < 0.1f)
                return;

            if (item.IsArmor())
                m_audio.PlayEffect(armor);
            else if (item.IsWeapon())
                m_audio.PlayEffect(weapon);
            else
                m_audio.PlayEffect(regular);
        }
    }
}
