using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    public class WeaponAttributes : ItemAttributes
    {
        public enum Attribute
        {
            Damage,
            DamagePercent,
            AttackSpeed,
            CriticalChance
        }

        public WeaponAttributes(int minAttributes = 0, int maxAttributes = 0)
        {
            var attributes = GetAttributes();

            for (int i = 0; i < attributes.Count; i++)
            {
                if (!CanAddAttribute(i, attributes.Count, minAttributes, maxAttributes))
                    break;

                switch (attributes[i].type)
                {
                    default:
                    case Attribute.Damage:
                        damage = attributes[i].points;
                        break;
                    case Attribute.DamagePercent:
                        damagePercent = attributes[i].points;
                        break;
                    case Attribute.AttackSpeed:
                        attackSpeed = attributes[i].points;
                        break;
                    case Attribute.CriticalChance:
                        critical = attributes[i].points;
                        break;
                }
            }
        }

        protected virtual List<(Attribute type, int points)> GetAttributes()
        {
            var attributes = new List<(Attribute, int)>();
            attributes.Add((Attribute.Damage, GetRandomPoint()));
            attributes.Add((Attribute.DamagePercent, GetRandomPercentage()));
            attributes.Add((Attribute.AttackSpeed, GetRandomPoint()));
            attributes.Add((Attribute.CriticalChance, GetRandomPercentage()));
            return Shuffle<(Attribute, int)>(attributes);
        }
    }
}
