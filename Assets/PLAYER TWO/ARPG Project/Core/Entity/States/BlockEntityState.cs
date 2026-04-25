using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class BlockEntityState : EntityState
    {
        protected float m_timer;

        public override void Enter(Entity entity)
        {
            m_timer = entity.blockDuration;
        }

        public override void Exit(Entity entity) { }

        public override void Step(Entity entity)
        {
            entity.lateralVelocity = Vector3.zero;

            entity.FaceLookDirection();
            entity.SnapToGround();

            m_timer -= Time.deltaTime;

            if (m_timer <= 0)
                entity.states.ChangeTo<IdleEntityState>();
        }
    }
}
