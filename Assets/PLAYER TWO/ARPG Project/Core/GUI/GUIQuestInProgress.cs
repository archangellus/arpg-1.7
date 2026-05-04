using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Quest In Progress")]
    public class GUIQuestInProgress : MonoBehaviour
    {
        [Tooltip("The prefab used to represent each Quest in progress.")]
        public GUIQuestList questList;

        [Tooltip("A reference to the container to instantiate the quest items on.")]
        public Transform container;

        protected List<GUIQuestList> m_items = new List<GUIQuestList>();

        protected virtual void InitializeCallbacks()
        {
            LevelQuests.instance.onQuestAdded.AddListener(OnQuestChanged);
            LevelQuests.instance.onQuestRemoved.AddListener(OnQuestChanged);
            LevelQuests.instance.onQuestCompleted.AddListener(OnQuestChanged);
            LevelQuests.instance.onProgressChanged.AddListener(OnQuestChanged);
        }

        protected virtual void HideAllItems()
        {
            foreach (var item in m_items)
                item.gameObject.SetActive(false);
        }

        protected virtual void CreateOrShowItems(QuestInstance[] quests)
        {
            if (quests == null)
                return;

            var inProgress = new List<QuestInstance>();

            for (int i = 0; i < quests.Length; i++)
            {
                if (!quests[i].completed)
                    inProgress.Add(quests[i]);
            }

            if (inProgress.Count == 0)
                return;

            inProgress.Reverse();

            for (int i = 0; i < inProgress.Count; i++)
            {
                if (m_items.Count <= i)
                    m_items.Add(Instantiate(questList, container));

                m_items[i].SetQuest(inProgress[i]);
                m_items[i].gameObject.SetActive(true);
            }
        }

        protected virtual void UpdateList()
        {
            HideAllItems();
            CreateOrShowItems(Game.instance.quests.list);
        }

        protected virtual void OnQuestChanged(QuestInstance _) => UpdateList();

        protected virtual void Start() => InitializeCallbacks();

        protected virtual void OnEnable() => UpdateList();
    }
}
