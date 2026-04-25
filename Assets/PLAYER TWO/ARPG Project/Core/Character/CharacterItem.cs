using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class CharacterItem
    {
        public Item data;
        public int stack;
        public ItemAttributes.AttributeEntry[] attributes;

        [HideInInspector]
        public int durability;

        public CharacterItem(
            Item data,
            ItemAttributes.AttributeEntry[] attributes,
            int durability,
            int stack
        )
        {
            this.data = data;
            this.attributes = attributes;
            this.durability = durability;
            this.stack = stack;
        }

        public ItemInstance ToItemInstance(bool withDefaultDurability = false)
        {
            var a = ItemAttributes.CreateFromSerializer(attributes);

            if (withDefaultDurability)
            {
                var durability =
                    data is ItemEquippable ? (data as ItemEquippable).maxDurability : 0;
                return new ItemInstance(data, a, durability, stack);
            }

            return new ItemInstance(data, a, durability, stack);
        }

        public static CharacterItem CreateFromSerializer(ItemSerializer serializer)
        {
            var data = GameDatabase.instance.FindElementById<Item>(serializer.itemId);
            return new CharacterItem(
                data,
                serializer.attributes,
                serializer.durability,
                serializer.stack
            );
        }
    }
}
