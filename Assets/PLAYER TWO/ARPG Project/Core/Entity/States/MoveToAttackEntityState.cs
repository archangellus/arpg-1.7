using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class MoveToAttackEntityState : EntityState
    {
        public override void Enter(Entity entity)
        {
            entity.CancelCombo();
        }

        public override void Exit(Entity entity) { }

        public override void Step(Entity entity)
        {
            entity.FaceMoveDirection();
            entity.SnapToGround();
            entity.EvaluateDirectionalMovement();

            if (entity.IsCloseToAttackTarget())
            {
                entity.Attack();
            }
            else if (entity.TryCalculatePath(entity.target.position))
            {
                entity.HandleWaypointMovement();
            }
        }
    }
}
