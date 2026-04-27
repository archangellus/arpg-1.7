using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Shield", menuName = "PLAYER TWO/ARPG Project/Item/Bow")]
    public class ItemBow : ItemWeapon
    {
        public enum Type { Bow, Crossbow }

        [Header("Bow Settings")]
        [Tooltip("The type of the Bow. Regular bow is equipped on the left hand slot. Crossbows are equipped on the right hand slot.")]
        public Type type;

        [Tooltip("The prefab of the projectile the Bow shoots.")]
        public Projectile projectile;

        [Tooltip("The minimum distance to shot the bow towards a target.")]
        public float shotDistance;

        [Header("Hand Settings")]
        [Tooltip("The offset position in local space applied to the prefab on the Entity's hands.")]
        public Vector3 handPosition;

        [Tooltip("The offset rotation in local space applied to the prefab on the Entity's hands.")]
        public Vector3 handRotation;

        /// <summary>
        /// Returns true if it's a regular Bow.
        /// </summary>
        public virtual bool IsBow() => type == Type.Bow;

        /// <summary>
        /// Returns true if it's a Cross Bow.
        /// </summary>
        public virtual bool IsCrossBow() => type == Type.Crossbow;

        /// <summary>
        /// Instantiates the Item's prefab, applying the Bow's hand offsets, as a child of the left hand slot,
        /// if the Bow's type is regular Bow, or the right hand slot, if the Bow's type is Cross Bow.
        /// </summary>
        /// <param name="rightHandSlot">The transform to assign the prefab to correspondent to the right hand.</param>
        /// <param name="leftHandSlot">The transform to assign the prefab to correspondent to the left hand.</param>
        /// <returns>Returns the instance of the newly instantiated Game Object.</returns>
        public virtual GameObject Instantiate(Transform rightHandSlot, Transform leftHandSlot)
        {
            var slot = type == ItemBow.Type.Bow ? leftHandSlot : rightHandSlot;
            var instance = Instantiate(prefab, slot);
            instance.transform.localPosition += handPosition;
            instance.transform.localRotation *= Quaternion.Euler(handRotation);
            return instance;
        }
    }
}
