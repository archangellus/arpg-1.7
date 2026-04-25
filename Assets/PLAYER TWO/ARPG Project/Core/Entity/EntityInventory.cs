using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Inventory")]
    public class EntityInventory : MonoBehaviour
    {
        [Header("Inventory Events")]
        public UnityEvent<ItemInstance> onItemAdded;
        public UnityEvent<ItemInstance> onItemInserted;
        public UnityEvent onItemRemoved;

        protected Inventory m_inventory;

        /// <summary>
        /// Returns the instance of the Inventory.
        /// </summary>
        public Inventory instance
        {
            get
            {
                if (m_inventory == null)
                {
                    m_inventory = new Inventory(
                        Game.instance.inventoryRows,
                        Game.instance.inventoryColumns
                    );
                    m_inventory.onItemAdded += (item, _) => onItemAdded.Invoke(item);
                    m_inventory.onItemInserted += (item, _) => onItemInserted.Invoke(item);
                    m_inventory.onItemRemoved += () => onItemRemoved.Invoke();
                }

                return m_inventory;
            }
        }
    }
}
