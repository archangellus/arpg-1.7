using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Player Inventory")]
    public class GUIPlayerInventory : GUIInventory
    {
        protected virtual void Start()
        {
            SetInventory(Level.instance.player.inventory.instance);
            InitializeInventory();
        }
    }
}
