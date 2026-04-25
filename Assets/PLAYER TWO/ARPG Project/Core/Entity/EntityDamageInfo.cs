using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// The elemental type of a damage event. Used to tint damage text and drive
    /// future type-specific mechanics such as resistances.
    /// </summary>
    public enum DamageType
    {
        /// <summary>Standard physical or untyped damage.</summary>
        Normal = 0,

        /// <summary>Fire elemental damage.</summary>
        Fire = 1,

        /// <summary>Ice elemental damage.</summary>
        Ice = 2,

        /// <summary>Poison elemental damage.</summary>
        Poison = 3,

        /// <summary>Lightning elemental damage.</summary>
        Lightning = 4,
    }

    /// <summary>
    /// Flags mask for damage types. Used by <see cref="EntityStatsManager"/> for
    /// per-entity immunity and resistance settings (multi-select in the Inspector).
    /// Bit positions align with <see cref="DamageType"/> integer values via <c>1 &lt;&lt; (int)DamageType</c>.
    /// </summary>
    [System.Flags]
    public enum ElementalDamageTypeMask
    {
        None = 0,
        Normal = 1 << 0,
        Fire = 1 << 1,
        Ice = 1 << 2,
        Poison = 1 << 3,
        Lightning = 1 << 4,
    }

    /// <summary>
    /// Describes whether damage was caused by a direct attack or an indirect source such as a
    /// damage-over-time effect. Used to control AI reactions and other context-sensitive logic.
    /// </summary>
    public enum DamageMode
    {
        /// <summary>A direct attack from an entity (melee, projectile, skill).</summary>
        Active,

        /// <summary>An indirect source such as a damage-over-time effect.</summary>
        Passive,
    }

    /// <summary>
    /// A single damage component carrying an elemental type and a raw amount.
    /// Weapon attacks may contain multiple layers (Normal + elemental), while
    /// skills and DoT effects always use a single layer.
    /// </summary>
    [System.Serializable]
    public struct DamageLayer
    {
        /// <summary>The elemental type of this damage component.</summary>
        public DamageType type;

        /// <summary>The raw (pre-defense) damage amount for this component.</summary>
        public int amount;

        public DamageLayer(DamageType type, int amount)
        {
            this.type = type;
            this.amount = amount;
        }
    }

    /// <summary>
    /// Carries all information needed to resolve a damage event. <see cref="layers"/> holds
    /// the per-type raw damage components. After <see cref="Entity.Damage"/> resolves the hit,
    /// <see cref="amount"/> is set to the final total and <see cref="sourcePosition"/> is
    /// populated before <see cref="Entity.onDamage"/> is invoked.
    /// </summary>
    public struct EntityDamageInfo
    {
        /// <summary>
        /// The individual damage components, one per damage type. Weapon attacks may have
        /// multiple layers; skills always have exactly one layer.
        /// </summary>
        public List<DamageLayer> layers;

        /// <summary>
        /// Total resolved (post-defense) damage. Written by <see cref="Entity.Damage"/> after
        /// <c>ResolveDamageAmount</c>; read by <see cref="Entity.onDamage"/> listeners.
        /// </summary>
        public int amount;

        /// <summary>Whether this hit is a critical strike.</summary>
        public bool critical;

        /// <summary>Optional entity effects to apply if the hit lands (not blocked). The chance roll applies to all of them.</summary>
        public EntityEffect[] effects;

        /// <summary>The probability (0–1) that the effects are applied on a successful hit.</summary>
        public float effectChance;

        /// <summary>
        /// If true, the target may stun the attacker based on stun chance.
        /// Set to false for sources like damage-over-time ticks.
        /// </summary>
        public bool canStun;

        /// <summary>
        /// If true, the target may block this hit based on block chance.
        /// Set to false for sources like damage-over-time ticks.
        /// </summary>
        public bool canBlock;

        /// <summary>
        /// If true, the target's defense is applied to the Normal (physical) damage layer.
        /// Set to false for sources like damage-over-time ticks.
        /// </summary>
        public bool canDefend;

        /// <summary>
        /// Whether the damage originates from a direct attack or an indirect source.
        /// AI uses this to decide whether to search for the damage source.
        /// </summary>
        public DamageMode damageMode;

        /// <summary>
        /// World position of the attacker. Populated by <see cref="Entity.Damage"/> before
        /// <see cref="Entity.onDamage"/> is invoked; do not set this manually.
        /// </summary>
        public Vector3 sourcePosition;

        /// <summary>
        /// If true, this damage originated from a damage reflection effect.
        /// Used to prevent infinite reflection loops.
        /// </summary>
        public bool isReflected;

        /// <summary>
        /// The primary damage type, derived from the first layer. Used by event listeners
        /// and other systems that need a single representative type.
        /// </summary>
        public DamageType primaryDamageType =>
            layers != null && layers.Count > 0 ? layers[0].type : DamageType.Normal;

        /// <summary>
        /// Creates a single-layer damage info. Used by skills, DoT effects, and any source
        /// with a single elemental type.
        /// </summary>
        public EntityDamageInfo(
            int amount,
            bool critical,
            EntityEffect[] effects = null,
            float effectChance = 1f,
            bool canStun = true,
            bool canBlock = true,
            bool canDefend = true,
            DamageMode damageMode = DamageMode.Active,
            DamageType damageType = DamageType.Normal
        )
        {
            layers = new List<DamageLayer> { new DamageLayer(damageType, amount) };
            this.amount = 0;
            this.critical = critical;
            this.effects = effects;
            this.effectChance = effectChance;
            this.canStun = canStun;
            this.canBlock = canBlock;
            this.canDefend = canDefend;
            this.damageMode = damageMode;
            sourcePosition = Vector3.zero;
            isReflected = false;
        }

        /// <summary>
        /// Creates a multi-layer damage info. Used by weapon melee and bow attacks that may
        /// carry both a Normal (physical) layer and one or more elemental layers from affixes.
        /// </summary>
        public EntityDamageInfo(
            List<DamageLayer> layers,
            bool critical,
            EntityEffect[] effects = null,
            float effectChance = 1f,
            bool canStun = true,
            bool canBlock = true,
            bool canDefend = true,
            DamageMode damageMode = DamageMode.Active
        )
        {
            this.layers = layers;
            this.amount = 0;
            this.critical = critical;
            this.effects = effects;
            this.effectChance = effectChance;
            this.canStun = canStun;
            this.canBlock = canBlock;
            this.canDefend = canDefend;
            this.damageMode = damageMode;
            sourcePosition = Vector3.zero;
            isReflected = false;
        }
    }
}
