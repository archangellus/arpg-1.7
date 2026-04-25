using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Quest Button")]
    public class GUIQuestButton : MonoBehaviour
    {
        [Tooltip("A reference to the Text component used as title.")]
        public Text title;

        [Tooltip("A reference to the Text component used as the progress counter.")]
        public Text progress;

        [Tooltip("A reference to the Game Object that represents the completion sign.")]
        public GameObject completed;

        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when clicking on this button.")]
        public AudioClip clickClip;

        protected Button m_button;
        protected QuestInstance m_quest;

        protected virtual void InitializeButton()
        {
            m_button = GetComponent<Button>();

            m_button.onClick.AddListener(() =>
            {
                ShowQuestWindow();
                GameAudio.instance.PlayUiEffect(clickClip);
            });
        }

        protected virtual void InitializeCallbacks()
        {
            LevelQuests.instance.onProgressChanged.AddListener(OnProgressChanged);
            LevelQuests.instance.onQuestCompleted.AddListener(OnQuestCompleted);
        }

        protected virtual void ShowQuestWindow() =>
            GUIWindowsManager.instance.quest.SetQuest(m_quest.data);

        protected virtual void UpdateProgress()
        {
            if (m_quest == null)
                return;

            progress.text = m_quest.GetProgressText();
        }

        /// <summary>
        /// Sets the Quest Instance that this button represents.
        /// </summary>
        /// <param name="quest">The Quest Instance you want to set.</param>
        public virtual void SetQuest(QuestInstance quest)
        {
            if (quest == null)
                return;

            m_quest = quest;
            title.text = m_quest.data.title;
            completed.SetActive(quest.completed);

            UpdateProgress();
        }

        protected virtual void OnProgressChanged(QuestInstance quest)
        {
            if (m_quest != quest)
                return;

            UpdateProgress();
        }

        protected virtual void OnQuestCompleted(QuestInstance quest)
        {
            if (m_quest != quest)
                return;

            completed.SetActive(true);
        }

        protected virtual void Start()
        {
            InitializeButton();
            InitializeCallbacks();
        }
    }
}
