using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    public class QuestsManager
    {
        /// <summary>
        /// Invoked when a new Quest Instance was added to the active quests list.
        /// </summary>
        public System.Action<QuestInstance> onQuestAdded;

        /// <summary>
        /// Invoked when the progress of any Quest Instance was changed.
        /// </summary>
        public System.Action<QuestInstance> onProgressChanged;

        /// <summary>
        /// Invoked when any Quest Instance was completed.
        /// </summary>
        public System.Action<QuestInstance> onQuestCompleted;

        /// <summary>
        /// Invoked when a Quest Instance was removed from the active quests list.
        /// </summary>
        public System.Action<QuestInstance> onQuestRemoved;

        protected List<QuestInstance> m_quests = new();

        /// <summary>
        /// Returns an array as copy of the active quests list.
        /// </summary>
        public QuestInstance[] list => m_quests.ToArray();

        /// <summary>
        /// Overrides the list of active quests from a given array.
        /// </summary>
        /// <param name="quests">The array you want to read from.</param>
        public virtual void SetQuests(QuestInstance[] quests)
        {
            m_quests = new List<QuestInstance>(quests);

            foreach (var quest in m_quests)
                AssignCallbacks(quest);
        }

        /// <summary>
        /// Adds a new Quest Instance to the active quests list.
        /// </summary>
        /// <param name="quest">The Quest you want to create the instance from.</param>
        public virtual void AddQuest(Quest quest)
        {
            if (ContainsQuest(quest))
                return;

            var instance = new QuestInstance(quest);
            m_quests.Add(instance);
            AssignCallbacks(instance);
            onQuestAdded?.Invoke(m_quests[^1]);
        }

        /// <summary>
        /// Removes a Quest Instance from the active quests list.
        /// </summary>
        /// <param name="quest">The Quest you want to remove.</param>
        public virtual void RemoveQuest(Quest quest)
        {
            if (!TryGetQuest(quest, out var instance) || instance.completed)
                return;

            m_quests.Remove(instance);
            onQuestRemoved?.Invoke(instance);
        }

        /// <summary>
        /// Gets a Quest Instance from the active quests list.
        /// </summary>
        /// <param name="quest">The data you are trying to find.</param>
        /// <param name="instance">The instance representing the given Quest.</param>
        /// <returns>Returns true if the Quest in the the active quests list.</returns>
        public virtual bool TryGetQuest(Quest quest, out QuestInstance instance)
        {
            instance = m_quests.Find(q => q.data == quest);
            return instance != null;
        }

        /// <summary>
        /// Returns true if the active quests list contains a given Quest and it is not completed.
        /// </summary>
        /// <param name="quest">The Quest you want to check.</param>
        /// <returns>Returns true if the Quest is active.</returns>
        public virtual bool IsQuestActive(Quest quest) =>
            m_quests.Exists(q => q.data == quest && !q.completed);

        protected virtual void AssignCallbacks(QuestInstance quest)
        {
            quest.onStateChanged += (state) =>
            {
                if (state == QuestInstance.State.Completed)
                    CompleteQuest(quest);
                onProgressChanged?.Invoke(quest);
            };
        }

        /// <summary>
        /// Completes a given Quest Instance.
        /// </summary>
        /// <param name="quest">The Quest Instance you want to complete.</param>
        protected virtual void CompleteQuest(QuestInstance quest)
        {
            if (Level.instance.player)
                quest.Reward(Level.instance.player);

            onQuestCompleted?.Invoke(quest);
        }

        /// <summary>
        /// Returns true if the active quests list contains a given Quest.
        /// </summary>
        /// <param name="quest">The Quest you want to search on the list.</param>
        public virtual bool ContainsQuest(Quest quest) => m_quests.Exists((q) => q.data == quest);

        /// <summary>
        /// Completes all quests from tue active quests list containing a given destination scene name.
        /// </summary>
        /// <param name="scene">The destination scene name.</param>
        public virtual void ReachedScene(string scene)
        {
            foreach (var quest in m_quests)
                quest.AddProgressScene(scene);
        }

        /// <summary>
        /// Adds progress to all quests from the active quests list containing a given progress key.
        /// It also triggers the completion of Quests that reached their target progress.
        /// </summary>
        /// <param name="key">The progress key of the quests.</param>
        public virtual void AddProgress(string key)
        {
            foreach (var quest in m_quests)
                quest.AddProgressKey(key);
        }

        /// <summary>
        /// Triggers the completion of a given Quest from the active quests list.
        /// </summary>
        /// <param name="quest">The Quest to trigger completion.</param>
        public virtual void Trigger(Quest quest)
        {
            if (TryGetQuest(quest, out var instance))
                instance.AddProgressTrigger();
        }
    }
}
