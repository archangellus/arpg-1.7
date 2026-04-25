using System;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>The elemental type of a debuff. Drives immunity checks and elemental conflict rules.</summary>
    public enum EntityDebuffType
    {
        Normal = 0,
        Fire = 1,
        Ice = 2,
        Poison = 3,
        Bleed = 4,
    }

    /// <summary>
    /// Flags mask of debuff types used by <see cref="EntityEffectManager.immuneTo"/>
    /// (displayed as a multi-select in the Inspector).
    /// </summary>
    [Flags]
    public enum EntityDebuffTypeMask
    {
        None = 0,
        Normal = 1 << 0,
        Fire = 1 << 1,
        Ice = 1 << 2,
        Poison = 1 << 3,
        Bleed = 1 << 4,
    }

    /// <summary>
    /// A negative entity effect that reduces stat multipliers and optionally applies damage over time.
    /// All reduction fields are expressed as integer percentages (0–75) to keep values readable
    /// in the Inspector and to ensure they scale proportionally across all entity power levels.
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Entity Debuff",
        menuName = "PLAYER TWO/ARPG Project/Entity/Entity Debuff"
    )]
    public class EntityDebuff : EntityEffect
    {
        [Header("Debuff Type")]
        [Tooltip(
            "The elemental type of this debuff. Used for immunity checks. "
                + "Non-Normal types cannot stack on the same entity."
        )]
        public EntityDebuffType debuffType;

        [Header("Stat Reductions")]
        [Range(0, 75)]
        [Tooltip("Percentage of physical damage output reduced. 0 = no change, 75 = -75%.")]
        public int damageReduction;

        [Range(0, 75)]
        [Tooltip("Percentage of magic damage output reduced. 0 = no change, 75 = -75%.")]
        public int magicDamageReduction;

        [Range(0, 75)]
        [Tooltip("Percentage of defense reduced. 0 = no change, 75 = -75%.")]
        public int defenseReduction;

        [Range(0, 75)]
        [Tooltip("Percentage of attack speed reduced. 0 = no change, 75 = -75%.")]
        public int attackSpeedReduction;

        [Range(0, 75)]
        [Tooltip("Percentage of move speed reduced. 0 = no change, 75 = -75%.")]
        public int moveSpeedReduction;

        [Range(0, 75)]
        [Tooltip("Percentage of accuracy rating reduced. 0 = no change, 75 = -75%.")]
        public int accuracyReduction;

        [Range(0, 75)]
        [Tooltip("Percentage of evasion rating reduced. 0 = no change, 75 = -75%.")]
        public int evasionReduction;

        [Header("Damage Over Time")]
        [Range(0, 25)]
        [Tooltip(
            "Percentage of the entity's max health dealt as damage per tick. Set to 0 to disable DoT."
        )]
        public int dotDamagePercent;

        [Min(0f)]
        [Tooltip("Time in seconds between each DoT tick. Defaults to 1 second if set to 0.")]
        public float dotInterval = 1f;

        /// <summary>
        /// Physical damage multiplier derived from <see cref="damageReduction"/> (e.g. 30% → 0.70).
        /// </summary>
        public float DamageMultiplier => 1f - damageReduction / 100f;

        /// <summary>
        /// Magic damage multiplier derived from <see cref="magicDamageReduction"/> (e.g. 30% → 0.70).
        /// </summary>
        public float MagicDamageMultiplier => 1f - magicDamageReduction / 100f;

        /// <summary>
        /// Defense multiplier derived from <see cref="defenseReduction"/> (e.g. 30% → 0.70).
        /// </summary>
        public float DefenseMultiplier => 1f - defenseReduction / 100f;

        /// <summary>
        /// Attack speed multiplier derived from <see cref="attackSpeedReduction"/>.
        /// </summary>
        public float AttackSpeedMultiplier => 1f - attackSpeedReduction / 100f;

        /// <summary>
        /// Move speed multiplier derived from <see cref="moveSpeedReduction"/>.
        /// </summary>
        public float MoveSpeedMultiplier => 1f - moveSpeedReduction / 100f;

        /// <summary>
        /// Accuracy rating multiplier derived from <see cref="accuracyReduction"/> (e.g. 50% → 0.50).
        /// </summary>
        public float AccuracyMultiplier => 1f - accuracyReduction / 100f;

        /// <summary>
        /// Evasion rating multiplier derived from <see cref="evasionReduction"/> (e.g. 50% → 0.50).
        /// </summary>
        public float EvasionMultiplier => 1f - evasionReduction / 100f;

        /// <summary>
        /// Returns true if this debuff applies damage over time.
        /// </summary>
        public bool HasDot() => dotDamagePercent > 0;

        /// <summary>
        /// Returns a human-readable string listing all stat reductions this debuff applies.
        /// Does not include damage-over-time details.
        /// </summary>
        public override string GetModifiersText()
        {
            var lines = new System.Text.StringBuilder();
            AppendReduction(lines, damageReduction, "Damage");
            AppendReduction(lines, magicDamageReduction, "Magic Damage");
            AppendReduction(lines, defenseReduction, "Defense");
            AppendReduction(lines, attackSpeedReduction, "Attack Speed");
            AppendReduction(lines, moveSpeedReduction, "Move Speed");
            AppendReduction(lines, accuracyReduction, "Accuracy");
            AppendReduction(lines, evasionReduction, "Evasion");
            return lines.ToString().TrimEnd();
        }

        protected static void AppendReduction(
            System.Text.StringBuilder lines,
            int reduction,
            string stat
        )
        {
            if (reduction <= 0)
                return;

            lines.AppendLine($"Decreases {stat} by {reduction}%");
        }
    }
}
