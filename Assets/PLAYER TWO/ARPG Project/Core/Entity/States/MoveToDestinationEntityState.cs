using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class MoveToDestinationEntityState : EntityState
    {
        public override void Enter(Entity entity) { }
        public override void Exit(Entity entity) { }

        public override void Step(Entity entity)
        {
            entity.FaceMoveDirection();
            entity.HandleWaypointMovement();
            entity.SnapToGround();
            entity.EvaluateDirectionalMovement();
        }
    }
}
