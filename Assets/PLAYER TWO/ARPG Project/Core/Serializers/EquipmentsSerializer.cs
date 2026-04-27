using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class EquipmentsSerializer
    {
        public ItemSerializer rightHand;
        public ItemSerializer leftHand;
        public ItemSerializer helm;
        public ItemSerializer chest;
        public ItemSerializer pants;
        public ItemSerializer gloves;
        public ItemSerializer boots;
        public ItemSerializer[] consumables;

        public EquipmentsSerializer(CharacterEquipments equipments)
        {
            SerializeItem(equipments.currentRightHand, ref rightHand);
            SerializeItem(equipments.currentLeftHand, ref leftHand);
            SerializeItem(equipments.currentHelm, ref helm);
            SerializeItem(equipments.currentChest, ref chest);
            SerializeItem(equipments.currentPants, ref pants);
            SerializeItem(equipments.currentGloves, ref gloves);
            SerializeItem(equipments.currentBoots, ref boots);
            SerializeConsumables(equipments.currentConsumables);
        }

        protected virtual void SerializeItem(ItemInstance instance, ref ItemSerializer item)
        {
            if (instance != null)
                item = new ItemSerializer(instance);
        }

        protected virtual void SerializeConsumables(ItemInstance[] consumables)
        {
            this.consumables = new ItemSerializer[consumables.Length];

            for (int i = 0; i < this.consumables.Length; i++)
            {
                if (consumables[i] == null) continue;

                this.consumables[i] = new ItemSerializer(consumables[i]);
            }
        }

        public virtual string ToJson() => JsonUtility.ToJson(this);

        public static EquipmentsSerializer FromJson(string json) =>
            JsonUtility.FromJson<EquipmentsSerializer>(json);
    }
}
