using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class CharacterQuests
    {
        public List<QuestInstance> initialQuests = new();

        public QuestsManager m_quests;

        public QuestInstance[] currentQuests =>
            m_quests != null ? m_quests.list : initialQuests.ToArray();

        public QuestsManager manager => m_quests;

        public CharacterQuests() { }

        public CharacterQuests(QuestInstance[] initialQuests)
        {
            this.initialQuests = new List<QuestInstance>(initialQuests);
        }

        /// <summary>
        /// Initializes the Character's Quest Manager.
        /// </summary>
        public virtual void InitializeQuests()
        {
            if (m_quests != null)
                return;

            m_quests = new QuestsManager();

            if (initialQuests == null || initialQuests.Count == 0)
                return;

            var instances = initialQuests.Select(q => new QuestInstance(
                q.data,
                q.progress,
                q.state
            ));

            m_quests.SetQuests(instances.ToArray());
        }

        public static CharacterQuests CreateFromSerializer(QuestsSerializer serializer)
        {
            var quests = serializer.quests.Select(q =>
            {
                var data = GameDatabase.instance.FindElementById<Quest>(q.questId);
                return new QuestInstance(data, q.progress, q.state);
            });

            return new CharacterQuests(quests.ToArray());
        }
    }
}
