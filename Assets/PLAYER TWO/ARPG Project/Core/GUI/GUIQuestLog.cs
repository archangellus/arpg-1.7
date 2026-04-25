using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Quest Log")]
    public class GUIQuestLog : MonoBehaviour
    {
        [Tooltip("The prefab used to represent each Quest.")]
        public GUIQuestButton questButton;

        [Tooltip("A reference to the container to instantiate the quest buttons on.")]
        public Transform container;

        protected List<GUIQuestButton> m_buttons = new List<GUIQuestButton>();

        protected virtual void InitializeCallbacks()
        {
            LevelQuests.instance.onQuestAdded.AddListener(OnQuestAdded);
            LevelQuests.instance.onQuestRemoved.AddListener(OnQuestRemoved);
        }

        protected virtual void HideAllButtons()
        {
            foreach (var button in m_buttons)
            {
                button.gameObject.SetActive(false);
            }
        }

        protected virtual void CreateOrShowButtons(QuestInstance[] quests)
        {
            if (quests == null)
                return;

            System.Array.Reverse(quests);

            for (int i = 0; i < quests.Length; i++)
            {
                if (m_buttons.Count <= i)
                    m_buttons.Add(Instantiate(questButton, container));

                m_buttons[i].SetQuest(quests[i]);
                m_buttons[i].gameObject.SetActive(true);
            }
        }

        protected virtual void UpdateButtons()
        {
            HideAllButtons();
            CreateOrShowButtons(Game.instance.quests.list);
        }

        protected virtual void OnQuestAdded(QuestInstance _) => UpdateButtons();

        protected virtual void OnQuestRemoved(QuestInstance _) => UpdateButtons();

        protected virtual void Start() => InitializeCallbacks();

        protected virtual void OnEnable() => UpdateButtons();
    }
}
