using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class IdleEntityState : EntityState
    {
        public override void Enter(Entity entity) { }

        public override void Exit(Entity entity) { }

        public override void Step(Entity entity)
        {
            entity.lateralVelocity = Vector3.zero;

            entity.SnapToGround();
            entity.EvaluateDirectionalMovement();
        }
    }
}
