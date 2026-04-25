using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// This component is used to add quest progression when a destructible is destroyed.
    /// </summary>
    [RequireComponent(typeof(Destructible))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Quest Destructible")]
    public class QuestDestructible : MonoBehaviour
    {
        [Tooltip("The destructible key that matches the progress key of the Quest.")]
        public string destructibleKey;

        protected Destructible m_destructible;

        protected virtual void Start() => InitializeDestructible();

        /// <summary>
        /// Initializes the destructible component and sets up the listener for destruction events.
        /// </summary>
        protected virtual void InitializeDestructible()
        {
            m_destructible = GetComponent<Destructible>();
            m_destructible.OnDestruct.AddListener(AddQuestProgression);
        }

        /// <summary>
        /// Adds quest progression to the quest system using the destructible key.
        /// </summary>
        public virtual void AddQuestProgression() =>
            Game.instance.quests.AddProgress(destructibleKey);
    }
}
