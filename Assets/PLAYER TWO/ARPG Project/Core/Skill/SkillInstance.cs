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

        public SkillInstance(Skill data)
        {
            this.data = data;
        }

        /// <summary>
        /// Performs the Skill.
        /// </summary>
        public virtual void Perform() => m_lastPerformTime = Time.time;

        /// <summary>
        /// Returns true if this Skill can be performed on this frame.
        /// </summary>
        public virtual bool CanPerform() =>
            m_lastPerformTime == 0 || Time.time >= m_lastPerformTime + data.coolDown;

        /// <summary>
        /// Returns the remaining cool down time for this Skill to be available again.
        /// </summary>
        public virtual float GetRemainingCoolDown()
        {
            if (m_lastPerformTime == 0)
                return 0;

            return Mathf.Clamp(data.coolDown - (Time.time - m_lastPerformTime), 0, data.coolDown);
        }
    }
}
