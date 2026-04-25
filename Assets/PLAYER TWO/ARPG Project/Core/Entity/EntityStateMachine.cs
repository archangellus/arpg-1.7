using System;
using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    public class EntityStateMachine
    {
        protected Entity m_entity;
        protected EntityState m_current;

        protected Dictionary<Type, EntityState> m_states = new Dictionary<Type, EntityState>();

        public EntityState current => m_current;

        public EntityStateMachine(Entity entity)
        {
            m_entity = entity;
        }

        /// <summary>
        /// Adds a new State to the state machine.
        /// </summary>
        /// <param name="state">The instance of the state you want to add.</param>
        public virtual void AddState(EntityState state)
        {
            var type = state.GetType();

            if (!m_states.ContainsKey(type))
            {
                m_states.Add(type, state);
            }
        }

        /// <summary>
		/// Changes to a given Entity State based on its class type.
		/// </summary>
		/// <typeparam name="TState">The class of the state you want to change to.</typeparam>
        public virtual void ChangeTo<T>(bool changeToSelf = false) where T : EntityState
        {
            if (!changeToSelf && m_current is T) return;

            var type = typeof(T);

            if (!m_states.ContainsKey(type))
            {
                m_states.Add(type, (EntityState)Activator.CreateInstance(type));
            }

            m_current?.Exit(m_entity);
            m_current = m_states[type];
            m_current.Enter(m_entity);
        }

        /// <summary>
		/// Returns true if the type of the current State matches a given one.
		/// </summary>
		/// <param name="type">The type you want to compare to.</param>
        public virtual bool IsCurrent<T>() where T : EntityState => m_current is T;

        /// <summary>
        /// Updates the State Machine.
        /// </summary>
        public virtual void HandleStep()
        {
            if (m_current != null)
            {
                m_current.Step(m_entity);
            }
        }
    }
}
