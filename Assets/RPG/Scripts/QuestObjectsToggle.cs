using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Quest Object Toggle")]
    public class QuestObjectToggle : MonoBehaviour
    {
        public enum ToggleAction
        {
            Ignore,
            Enable,
            Disable,
        }

        [System.Serializable]
        public class QuestToggleRule
        {
            [Tooltip("The Quest this rule observes.")]
            public Quest quest;

            [Tooltip("The objects that will be toggled when this rule is evaluated.")]
            public List<GameObject> objects = new();

            [Header("Actions by Quest State")]
            [Tooltip("Action to apply when the Quest is not in the Player quest log.")]
            public ToggleAction onQuestNotInLog = ToggleAction.Ignore;

            [Tooltip("Action to apply when the Quest is in progress.")]
            public ToggleAction onQuestInProgress = ToggleAction.Ignore;

            [Tooltip("Action to apply when the Quest is waiting for turn-in (Return to Giver).")]
            public ToggleAction onQuestReturnToGiver = ToggleAction.Ignore;

            [Tooltip("Action to apply when the Quest is completed.")]
            public ToggleAction onQuestCompleted = ToggleAction.Ignore;
        }

        [Tooltip("If true, all rules are evaluated once when this component starts.")]
        public bool evaluateOnStart = true;

        [Tooltip("The list of Quest rules and the objects affected by each rule.")]
        public List<QuestToggleRule> rules = new();

        protected QuestsManager m_manager;

        protected virtual void Start()
        {
            InitializeManager();
            InitializeCallbacks();

            if (evaluateOnStart)
                EvaluateAllRules();
        }

        protected virtual void OnDestroy()
        {
            if (m_manager == null)
                return;

            m_manager.onQuestAdded -= OnQuestChanged;
            m_manager.onProgressChanged -= OnQuestChanged;
            m_manager.onQuestCompleted -= OnQuestChanged;
            m_manager.onQuestRemoved -= OnQuestChanged;
        }

        protected virtual void InitializeManager()
        {
            if (Game.instance == null || Game.instance.currentCharacter == null)
                return;

            m_manager = Game.instance.currentCharacter.quests.manager;
        }

        protected virtual void InitializeCallbacks()
        {
            if (m_manager == null)
                return;

            m_manager.onQuestAdded += OnQuestChanged;
            m_manager.onProgressChanged += OnQuestChanged;
            m_manager.onQuestCompleted += OnQuestChanged;
            m_manager.onQuestRemoved += OnQuestChanged;
        }

        protected virtual void OnQuestChanged(QuestInstance _)
        {
            EvaluateAllRules();
        }

        public virtual void EvaluateAllRules()
        {
            if (m_manager == null)
                InitializeManager();

            foreach (var rule in rules)
                EvaluateRule(rule);
        }

        protected virtual void EvaluateRule(QuestToggleRule rule)
        {
            if (rule == null || rule.quest == null)
                return;

            var action = GetActionFromQuestState(rule);
            ApplyAction(rule.objects, action);
        }

        protected virtual ToggleAction GetActionFromQuestState(QuestToggleRule rule)
        {
            if (m_manager == null || !m_manager.TryGetQuest(rule.quest, out var instance))
                return rule.onQuestNotInLog;

            return instance.state switch
            {
                QuestInstance.State.InProgress => rule.onQuestInProgress,
                QuestInstance.State.ReturnToGiver => rule.onQuestReturnToGiver,
                QuestInstance.State.Completed => rule.onQuestCompleted,
                _ => ToggleAction.Ignore,
            };
        }

        protected virtual void ApplyAction(List<GameObject> objects, ToggleAction action)
        {
            if (action == ToggleAction.Ignore || objects == null)
                return;

            var active = action == ToggleAction.Enable;

            foreach (var target in objects)
            {
                if (target)
                    target.SetActive(active);
            }
        }
    }
}