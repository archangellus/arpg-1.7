using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Runtime wrapper tracking the remaining duration and DoT state of one active
    /// <see cref="EntityEffect"/> on an entity.
    /// </summary>
    [System.Serializable]
    public class EntityEffectInstance
    {
        /// <summary>
        /// The effect data asset.
        /// </summary>
        public EntityEffect data { get; protected set; }

        /// <summary>
        /// The entity that applied this effect.
        /// </summary>
        public Entity source { get; protected set; }

        /// <summary>
        /// The instantiated particle GameObject for this effect, if any.
        /// </summary>
        public GameObject particleInstance;

        /// <summary>
        /// Remaining duration in seconds. Only decremented when <see cref="EntityEffect.duration"/> > 0.
        /// </summary>
        public float remainingDuration { get; protected set; }

        protected float m_dotTimer;

        public EntityEffectInstance(EntityEffect effect, Entity source)
        {
            data = effect;
            this.source = source;
            remainingDuration = effect.duration;
        }

        /// <summary>
        /// Decrements the remaining duration by the given delta. Returns true when the effect expires.
        /// Effects with <see cref="EntityEffect.duration"/> of 0 are permanent and never expire.
        /// </summary>
        public bool Tick(float deltaTime)
        {
            if (data.duration <= 0)
                return false;

            remainingDuration -= deltaTime;
            return remainingDuration <= 0;
        }

        /// <summary>
        /// Accumulates time and returns true when a DoT tick should fire.
        /// Only applies to <see cref="EntityDebuff"/> instances with DoT enabled.
        /// Resets the internal timer on a successful tick.
        /// </summary>
        public bool ShouldTickDot(float deltaTime)
        {
            if (data is not EntityDebuff debuff || !debuff.HasDot())
                return false;

            m_dotTimer += deltaTime;
            var interval = debuff.dotInterval > 0 ? debuff.dotInterval : 1f;

            if (m_dotTimer >= interval)
            {
                m_dotTimer -= interval;
                return true;
            }

            return false;
        }
    }
}
