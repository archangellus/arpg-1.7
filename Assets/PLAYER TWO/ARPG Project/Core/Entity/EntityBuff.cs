using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// A positive entity effect that increases stats by a given percentage.
    /// Each field represents the percentage increase (0 = no change, 100 = +100%).
    /// <see cref="damageTakenReduction"/> is the exception: it reduces incoming damage
    /// by the given percentage instead of increasing a stat.
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Entity Buff",
        menuName = "PLAYER TWO/ARPG Project/Entity/Entity Buff"
    )]
    public class EntityBuff : EntityEffect
    {
        [Header("Stat Bonuses")]
        [Range(0, 100)]
        [Tooltip(
            "Increases physical damage output by this percentage. 0 = no change, 100 = +100%."
        )]
        public int damageIncrease;

        [Range(0, 100)]
        [Tooltip("Increases magic damage output by this percentage. 0 = no change, 100 = +100%.")]
        public int magicDamageIncrease;

        [Range(0, 100)]
        [Tooltip(
            "Reduces damage taken by this percentage. 0 = no change, 100 = -100% damage taken."
        )]
        public int damageTakenReduction;

        [Range(0, 100)]
        [Tooltip("Increases attack speed by this percentage. 0 = no change, 100 = +100%.")]
        public int attackSpeedIncrease;

        [Range(0, 100)]
        [Tooltip("Increases move speed by this percentage. 0 = no change, 100 = +100%.")]
        public int moveSpeedIncrease;

        [Range(0, 100)]
        [Tooltip("Increases accuracy rating by this percentage. 0 = no change, 100 = +100%.")]
        public int accuracyIncrease;

        [Range(0, 100)]
        [Tooltip("Increases evasion rating by this percentage. 0 = no change, 100 = +100%.")]
        public int evasionIncrease;

        /// <summary>
        /// Returns a human-readable string listing all stat bonuses this buff provides.
        /// </summary>
        public override string GetModifiersText()
        {
            var lines = new System.Text.StringBuilder();
            AppendIncrease(lines, damageIncrease, "Damage");
            AppendIncrease(lines, magicDamageIncrease, "Magic Damage");
            AppendReduction(lines, damageTakenReduction, "Damage Taken");
            AppendIncrease(lines, attackSpeedIncrease, "Attack Speed");
            AppendIncrease(lines, moveSpeedIncrease, "Move Speed");
            AppendIncrease(lines, accuracyIncrease, "Accuracy");
            AppendIncrease(lines, evasionIncrease, "Evasion");
            return lines.ToString().TrimEnd();
        }

        protected static void AppendIncrease(
            System.Text.StringBuilder lines,
            int increase,
            string stat
        )
        {
            if (increase <= 0)
                return;

            lines.AppendLine($"Increases {stat} by {increase}%");
        }

        protected static void AppendReduction(
            System.Text.StringBuilder lines,
            int reduction,
            string stat
        )
        {
            if (reduction <= 0)
                return;

            lines.AppendLine($"Reduces {stat} by {reduction}%");
        }

        /// <summary>Returns the physical damage multiplier for this buff (e.g., 50 → 1.5).</summary>
        public float DamageMultiplier => 1f + damageIncrease / 100f;

        /// <summary>Returns the magic damage multiplier for this buff (e.g., 50 → 1.5).</summary>
        public float MagicDamageMultiplier => 1f + magicDamageIncrease / 100f;

        /// <summary>
        /// Returns the damage-taken multiplier for this buff (e.g., 30 → 0.7 = 30% less damage taken).
        /// </summary>
        public float DamageTakenMultiplier => 1f - damageTakenReduction / 100f;

        /// <summary>Returns the attack speed multiplier for this buff (e.g., 50 → 1.5).</summary>
        public float AttackSpeedMultiplier => 1f + attackSpeedIncrease / 100f;

        /// <summary>Returns the move speed multiplier for this buff (e.g., 50 → 1.5).</summary>
        public float MoveSpeedMultiplier => 1f + moveSpeedIncrease / 100f;

        /// <summary>Returns the accuracy rating multiplier for this buff (e.g., 50 → 1.5).</summary>
        public float AccuracyMultiplier => 1f + accuracyIncrease / 100f;

        /// <summary>Returns the evasion rating multiplier for this buff (e.g., 50 → 1.5).</summary>
        public float EvasionMultiplier => 1f + evasionIncrease / 100f;
    }
}
