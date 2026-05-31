using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Pet Inventory")]
    public class GUIPetInventory : GUIInventory
    {
        protected virtual void Start() => EnsureInitialized();

        public virtual bool EnsureInitialized()
        {
            if (isInitialized)
                return true;

            if (!PetInventorySettings.instance)
            {
                Debug.LogWarning("A GUIPetInventory needs a PetInventorySettings component in the scene.");
                return false;
            }

            moneyText.SafeCall(t => t.gameObject.SetActive(false));
            SetInventory(PetInventorySettings.instance.inventory);
            InitializeInventory();
            return isInitialized;
        }

        public virtual bool TryMoveToPlayerInventory(GUIItem item)
        {
            var playerInventory = GUIWindowsManager.instance.GetInventory();

            if (!playerInventory || !TryRemove(item))
                return false;

            if (playerInventory.TryAutoInsert(item))
                return true;

            item.TryMoveToLastPosition();
            return false;
        }
    }
}
