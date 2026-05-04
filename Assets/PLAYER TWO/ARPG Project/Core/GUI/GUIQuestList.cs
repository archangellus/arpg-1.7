using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Quest List")]
    public class GUIQuestList : MonoBehaviour
    {
        [Tooltip("A reference to the Text component used as title.")]
        public Text title;

        [Tooltip("A reference to the Text component used as the progress counter.")]
        public Text progress;

        protected Button m_button;
        protected QuestInstance m_quest;

        protected virtual void InitializeButton()
        {
            m_button = GetComponent<Button>();
            m_button.onClick.AddListener(ShowQuestWindow);
        }

        protected virtual void InitializeCallbacks()
        {
            LevelQuests.instance.onProgressChanged.AddListener(OnProgressChanged);
        }

        protected virtual void ShowQuestWindow() =>
            GUIWindowsManager.instance.quest.SetQuest(m_quest.data);

        protected virtual void UpdateProgress()
        {
            if (m_quest == null)
                return;

            progress.text = m_quest.GetProgressText();
        }

        public virtual void SetQuest(QuestInstance quest)
        {
            if (quest == null)
                return;

            m_quest = quest;
            title.text = m_quest.data.title;

            UpdateProgress();
        }

        protected virtual void OnProgressChanged(QuestInstance quest)
        {
            if (m_quest != quest)
                return;

            UpdateProgress();
        }

        protected virtual void Start()
        {
            InitializeButton();
            InitializeCallbacks();
        }
    }
}
