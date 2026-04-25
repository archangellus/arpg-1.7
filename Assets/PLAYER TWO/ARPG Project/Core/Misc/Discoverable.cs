using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Discoverable")]
    public class Discoverable : MonoBehaviour
    {
        public enum State
        {
            Undiscovered = 0,
            Discovered = 1,
        }

        [Header("Discoverable Settings")]
        [Tooltip("The unique identifier for this discoverable.")]
        [SerializeField, UniqueIdGen]
        protected string m_id;

        [Tooltip("The current state of this discoverable.")]
        [SerializeField]
        protected State m_state = State.Undiscovered;

        [Space(15f)]
        public UnityEvent onDiscover;

        /// <summary>
        /// The unique identifier for this discoverable.
        /// </summary>
        public string id => m_id;

        /// <summary>
        /// The current state of this discoverable.
        /// When set to <see cref="State.Discovered"/>,
        /// the <see cref="onDiscover"/> event will be invoked.
        /// </summary>
        public State state
        {
            get => m_state;
            set
            {
                if (state != value)
                {
                    m_state = value;

                    if (m_state == State.Discovered)
                        onDiscover.Invoke();
                }
            }
        }

        /// <summary>
        /// Sets the state of this discoverable.
        /// </summary>
        /// <param name="value">The new state of this discoverable.</param>
        public virtual void SetState(State value) => state = value;

        /// <summary>
        /// Sets the state of this discoverable to <see cref="State.Discovered"/>.
        /// </summary>
        public virtual void SetStateToDiscovered() => state = State.Discovered;

        /// <summary>
        /// Sets the state of this discoverable to <see cref="State.Undiscovered"/>.
        /// </summary>
        public virtual void SetStateToUndiscovered() => state = State.Undiscovered;

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (GameTags.IsPlayer(other))
                SetStateToDiscovered();
        }
    }
}
