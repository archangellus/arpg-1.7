using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class ItemSerializer
    {
        public int itemId = -1;
        public int durability;
        public int stack;
        public ItemAttributes.AttributeEntry[] attributes;
        public int rarityId = -1;
        public int[] prefixIndices;
        public int[] suffixIndices;

        public ItemSerializer() { }

        public ItemSerializer(ItemInstance item)
        {
            itemId = GameDatabase.instance.GetElementId<Item>(item.data);
            durability = item.durability;
            stack = item.stack;
            rarityId = item.rarityId;
            prefixIndices = item.prefixIndices?.ToArray();
            suffixIndices = item.suffixIndices?.ToArray();

            var types = (ItemAttributes.AttributeType[])
                System.Enum.GetValues(typeof(ItemAttributes.AttributeType));
            attributes = new ItemAttributes.AttributeEntry[types.Length];

            for (int i = 0; i < types.Length; i++)
                attributes[i] = new ItemAttributes.AttributeEntry
                {
                    type = types[i],
                    value = item.ContainAttributes() ? item.attributes[types[i]] : 0,
                };
        }

        public virtual string ToJson() => JsonUtility.ToJson(this);

        public static ItemSerializer FromJson(string json) =>
            JsonUtility.FromJson<ItemSerializer>(json);
    }
}
