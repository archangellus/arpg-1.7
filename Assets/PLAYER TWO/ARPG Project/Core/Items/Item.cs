using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class Item : ScriptableObject
    {
        [Header("Item Settings")]
        [Tooltip("The prefab that represents this Item in the game scene.")]
        public GameObject prefab;

        [Tooltip("The base price of this Item.")]
        public int price;

        [Header("Drop Settings")]
        [Tooltip("The position relative to the drop point to place the Item when dropping it.")]
        public Vector3 dropPosition;

        [Tooltip("The rotation relative to world space to place the Item when dropping it.")]
        public Vector3 dropRotation = new Vector3(-90, 0, 45);

        [Header("Inventory Settings")]
        [Tooltip("The sprite that represents this Item on the Inventory.")]
        public Sprite image;

        [Tooltip("The number of rows this Item occupies in the Inventory.")]
        public int rows = 1;

        [Tooltip("The number of column this Item occupies in the Inventory.")]
        public int columns = 1;

        [Tooltip("If true, this Item can be stacked on the Inventory.")]
        public bool canStack;

        [Tooltip("The maximum stack size of this Item on the Inventory.")]
        public int stackCapacity;

        /// <summary>
        /// Instantiates the Item's prefab as child of a given Transform.
        /// </summary>
        /// <param name="slot">The slot to attach the Item to.</param>
        /// <returns>Returns the instance of the newly instantiated Game Object.</returns>
        public virtual GameObject Instantiate(Transform slot)
        {
            return Instantiate(prefab, slot);
        }
    }
}
