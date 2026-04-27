using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class DirectionalMovementEntityState : EntityState
    {
        public override void Enter(Entity entity) { }

        public override void Exit(Entity entity) { }

        public override void Step(Entity entity)
        {
            entity.SnapToGround();
            entity.FaceMoveDirection();

            var moveDirection = entity.inputs.GetMoveDirection();

            if (moveDirection.sqrMagnitude > 0)
                entity.lateralVelocity = moveDirection * entity.moveSpeed;
            else
                entity.states.ChangeTo<IdleEntityState>();
        }
    }
}
