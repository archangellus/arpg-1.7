namespace PLAYERTWO.ARPGProject
{
    public abstract class EntityState
    {
        /// <summary>
		/// Called when this State is invoked.
		/// </summary>
        public abstract void Enter(Entity entity);

        /// <summary>
		/// Called when this State changes to another.
		/// </summary>
        public abstract void Exit(Entity entity);

        /// <summary>
		/// Called every frame where this State is activated.
		/// </summary>
        public abstract void Step(Entity entity);
    }
}
