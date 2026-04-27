using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class ItemSerializer
    {
        [System.Serializable]
        public class Attributes
        {
            public int damage;
            public int damagePercent;
            public int attackSpeed;
            public int critical;
            public int defense;
            public int defensePercent;
            public int mana;
            public int manaPercent;
            public int health;
            public int healthPercent;
        }

        public int itemId = -1;
        public int durability;
        public int stack;
        public Attributes attributes;

        public ItemSerializer() { }

        public ItemSerializer(ItemInstance item)
        {
            this.itemId = GameDatabase.instance.GetElementId<Item>(item.data);
            this.durability = item.durability;
            this.stack = item.stack;
            this.attributes = new Attributes();

            if (item.ContainAttributes())
            {
                var attributes = item.attributes;

                this.attributes.damage = attributes.damage;
                this.attributes.damagePercent = attributes.damagePercent;
                this.attributes.attackSpeed = attributes.attackSpeed;
                this.attributes.critical = attributes.critical;
                this.attributes.defense = attributes.defense;
                this.attributes.defensePercent = attributes.defensePercent;
                this.attributes.mana = attributes.mana;
                this.attributes.manaPercent = attributes.manaPercent;
                this.attributes.health = attributes.health;
                this.attributes.healthPercent = attributes.healthPercent;
            }
        }

        public virtual string ToJson() => JsonUtility.ToJson(this);

        public static ItemSerializer FromJson(string json) =>
            JsonUtility.FromJson<ItemSerializer>(json);
    }
}
