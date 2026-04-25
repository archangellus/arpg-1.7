using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class QuestsSerializer
    {
        [System.Serializable]
        public class Quest
        {
            public int questId;
            public int progress;
            public int state;
        }

        public List<Quest> quests = new();

        public QuestsSerializer(CharacterQuests quests)
        {
            foreach (var quest in quests.currentQuests)
            {
                var id = GameDatabase.instance.GetElementId(quest.data);

                var questData = new Quest()
                {
                    questId = id,
                    progress = quest.progress,
                    state = (int)quest.state,
                };

                this.quests.Add(questData);
            }
        }

        public virtual string ToJson() => JsonUtility.ToJson(this);

        public static QuestsSerializer FromJson(string json) =>
            JsonUtility.FromJson<QuestsSerializer>(json);
    }
}
