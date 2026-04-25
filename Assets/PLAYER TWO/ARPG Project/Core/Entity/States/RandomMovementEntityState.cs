using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class RandomMovementEntityState : EntityState
    {
        protected float[] m_walkingDistances = new float[] { 1.5f, 3.5f, 5, 2, 4 };
        protected float[] m_waitingDurations = new float[] { 2, 3.5f, 2.5f, 3 };

        protected float m_waitingDuration;

        public override void Enter(Entity entity) => Randomize(entity);

        public override void Exit(Entity entity) { }

        public override void Step(Entity entity)
        {
            entity.HandleWaypointMovement();
            entity.FaceMoveDirection();
            entity.SnapToGround();

            if (entity.GetDistanceToDestination() < 0.1f)
            {
                m_waitingDuration -= Time.deltaTime;

                if (m_waitingDuration <= 0)
                {
                    Randomize(entity);
                }
            }
        }

        protected virtual void Randomize(Entity entity)
        {
            UpdateWaitingDuration();
            UpdateDestination(entity);
        }

        protected virtual void UpdateDestination(Entity entity)
        {
            var point = Random.insideUnitCircle;
            var direction = new Vector3(point.x, 0, point.y).normalized;
            var distanceIndex = Random.Range(0, m_walkingDistances.Length);
            var destination = entity.initialPosition + direction * m_walkingDistances[distanceIndex];
            entity.TryCalculatePath(destination);
        }

        protected virtual void UpdateWaitingDuration()
        {
            var index = Random.Range(0, m_waitingDurations.Length);
            m_waitingDuration = m_waitingDurations[index];
        }
    }
}
