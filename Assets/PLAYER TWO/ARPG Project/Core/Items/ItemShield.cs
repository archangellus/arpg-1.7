using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Shield", menuName = "PLAYER TWO/ARPG Project/Item/Shield")]
    public class ItemShield : ItemEquippable
    {
        [Header("Shield Settings")]
        [Tooltip("The base defense points of this Shield.")]
        public int defense;

        [Tooltip("The chance to block an attack.")]
        public int chanceToBlock;

        [Header("Transform Settings")]
        [Tooltip("The offset position in local space applied to the prefab on the Entity's arm.")]
        public Vector3 armPosition;

        [Tooltip("The offset rotation in local space applied to the prefab on the Entity's arm.")]
        public Vector3 armRotation;

        /// <summary>
        /// Instantiates the Item's prefab, applying the Shield's arm offsets, as a child of a given Transform.
        /// </summary>
        /// <param name="slot">The transform to assign the prefab to.</param>
        /// <returns>Returns the instance of the newly instantiated Game Object.</returns>
        public override GameObject Instantiate(Transform slot)
        {
            var instance = Instantiate(prefab, slot);
            instance.transform.localPosition += armPosition;
            instance.transform.localRotation *= Quaternion.Euler(armRotation);
            return instance;
        }
    }
}
