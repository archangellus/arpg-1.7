using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class SkillInstance
    {
        /// <summary>
        /// Returns the scriptable object that represents this Skill Instance.
        /// </summary>
        public Skill data { get; protected set; }

        protected float m_lastPerformTime;
        protected EntityStatsManager m_stats;

        public SkillInstance(Skill data, EntityStatsManager stats = null)
        {
            this.data = data;
            m_stats = stats;
        }

        /// <summary>
        /// Returns the skill's cooldown duration after applying the entity's cooldown reduction.
        /// </summary>
        public virtual float GetEffectiveCoolDown() =>
            data.coolDown
            * Mathf.Max(0f, 1f - (m_stats != null ? m_stats.skillCoolDownReduction : 0f));

        /// <summary>
        /// Performs the Skill.
        /// </summary>
        public virtual void Perform() => m_lastPerformTime = Time.time;

        /// <summary>
        /// Returns true if this Skill can be performed on this frame.
        /// </summary>
        public virtual bool CanPerform() =>
            m_lastPerformTime == 0 || Time.time >= m_lastPerformTime + GetEffectiveCoolDown();

        /// <summary>
        /// Returns the remaining cool down time for this Skill to be available again.
        /// </summary>
        public virtual float GetRemainingCoolDown()
        {
            if (m_lastPerformTime == 0)
                return 0;

            var coolDown = GetEffectiveCoolDown();
            return Mathf.Clamp(coolDown - (Time.time - m_lastPerformTime), 0, coolDown);
        }
    }
}
