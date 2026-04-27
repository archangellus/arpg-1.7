using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    public class ArmorAttributes : ItemAttributes
    {
        public enum Attribute
        {
            Defense,
            DefensePercent,
            Mana,
            ManaPercent,
            Health,
            HealthPercent,
        }

        public ArmorAttributes(int minAttributes = 0, int maxAttributes = 0)
        {
            var attributes = GetAttributes();

            for (int i = 0; i < attributes.Count; i++)
            {
                if (!CanAddAttribute(i, attributes.Count, minAttributes, maxAttributes))
                    break;

                switch (attributes[i].type)
                {
                    default:
                    case Attribute.Defense:
                        defense = attributes[i].points;
                        break;
                    case Attribute.DefensePercent:
                        defensePercent = attributes[i].points;
                        break;
                    case Attribute.Mana:
                        mana = attributes[i].points;
                        break;
                    case Attribute.ManaPercent:
                        manaPercent = attributes[i].points;
                        break;
                    case Attribute.Health:
                        health = attributes[i].points;
                        break;
                    case Attribute.HealthPercent:
                        healthPercent = attributes[i].points;
                        break;
                }
            }
        }

        protected virtual List<(Attribute type, int points)> GetAttributes()
        {
            var attributes = new List<(Attribute, int)>();
            attributes.Add((Attribute.Defense, GetRandomPoint()));
            attributes.Add((Attribute.DefensePercent, GetRandomPercentage()));
            attributes.Add((Attribute.Mana, GetRandomPoint()));
            attributes.Add((Attribute.ManaPercent, GetRandomPercentage()));
            attributes.Add((Attribute.Health, GetRandomPoint()));
            attributes.Add((Attribute.HealthPercent, GetRandomPercentage()));
            return Shuffle<(Attribute, int)>(attributes);
        }
    }
}
