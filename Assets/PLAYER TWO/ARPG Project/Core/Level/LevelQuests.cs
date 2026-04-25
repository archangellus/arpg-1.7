using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Game/Level Quests")]
    public class LevelQuests : Singleton<LevelQuests>
    {
        public UnityEvent<QuestInstance> onQuestAdded;
        public UnityEvent<QuestInstance> onProgressChanged;
        public UnityEvent<QuestInstance> onQuestCompleted;
        public UnityEvent<QuestInstance> onQuestRemoved;

        public QuestsManager quests => Game.instance.quests;

        protected virtual void InitializeCallbacks()
        {
            quests.onQuestAdded += onQuestAdded.Invoke;
            quests.onProgressChanged += onProgressChanged.Invoke;
            quests.onQuestCompleted += onQuestCompleted.Invoke;
            quests.onQuestRemoved += onQuestRemoved.Invoke;
        }

        protected virtual void Start() => InitializeCallbacks();
    }
}
