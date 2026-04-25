namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class CharacterItemAttributes
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

        public static CharacterItemAttributes
            CreateFromSerializer(ItemSerializer.Attributes attributes)
        {
            return new CharacterItemAttributes()
            {
                damage = attributes.damage,
                damagePercent = attributes.damagePercent,
                attackSpeed = attributes.attackSpeed,
                critical = attributes.critical,
                defense = attributes.defense,
                defensePercent = attributes.defensePercent,
                mana = attributes.mana,
                manaPercent = attributes.manaPercent,
                health = attributes.health,
                healthPercent = attributes.healthPercent,
            };
        }
    }
}
