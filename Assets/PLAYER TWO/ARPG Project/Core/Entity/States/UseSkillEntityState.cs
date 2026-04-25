using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class UseSkillEntityState : EntityState
    {
        protected float m_duration;

        protected Vector3 m_faceDirection;

        public override void Enter(Entity entity)
        {
            m_duration = 0;
            entity.lateralVelocity = Vector3.zero;
            UpdateFaceDirection(entity);
            entity.onMagicAttack?.Invoke();
        }

        public override void Exit(Entity entity) { }

        public override void Step(Entity entity)
        {
            entity.SnapToGround();

            if (entity.skills.CanFaceDirection())
                entity.FaceTo(m_faceDirection);

            m_duration += Time.deltaTime;

            if (m_duration >= entity.skillDuration)
            {
                entity.states.ChangeTo<IdleEntityState>();
            }
        }

        protected virtual void UpdateFaceDirection(Entity entity)
        {
            if (entity.target)
            {
                m_faceDirection = entity.GetDirectionToTarget();
                return;
            }

            m_faceDirection = entity.lookDirection;
        }
    }
}
