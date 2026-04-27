using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Armor", menuName = "PLAYER TWO/ARPG Project/Item/Armor")]
    public class ItemArmor : ItemEquippable
    {
        [System.Serializable]
        public class PieceSettings
        {
            public string name;
            public bool show;
        }

        [Header("Armor Settings")]
        [Tooltip("The slot in which this armor can be equipped to.")]
        public ItemSlots slot;

        [Tooltip("The base defense points of this Armor.")]
        public int defense;

        [Header("Rendering Settings")]
        [Tooltip("The mesh to replace on the Entity's Skinned Mesh Renderer correspondent to the Armor slot.")]
        public Mesh mesh;

        [Tooltip("The materials to replace on the Entity's Skinned Mesh Renderer correspondent to the Armor slot.")]
        public Material[] materials;

        [Header("Character Pieces Settings")]
        [Tooltip("The pieces of the character that will be shown or hidden when this armor is equipped.")]
        public PieceSettings[] pieces;

        /// <summary>
        /// Returns true if this Item has custom materials.
        /// </summary>
        public virtual bool HasMaterials() => materials != null && materials.Length > 0;
    }
}
