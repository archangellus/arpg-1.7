using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Merchant Inventory")]
    public class GUIMerchantInventory : GUIInventory
    {
        protected GUIMerchant m_merchant;

        protected virtual void InitializeMerchant() =>
            m_merchant = GetComponentInParent<GUIMerchant>();

        public override bool TryPlace(GUIItem item)
        {
            if (m_merchant && m_merchant.TryBuy(item))
            {
                item.merchant = m_merchant;
                UpdateSlots();
                return true;
            }

            return false;
        }

        protected virtual void Start()
        {
            InitializeMerchant();
        }
    }
}
