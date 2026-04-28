using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

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
            [System.Serializable]
            public class QuestToggleTarget
            {
                [Tooltip("The object to be toggled.")]
                public GameObject target;

                [Range(0f, 1f)]
                [Tooltip("Delay before toggling this object.")]
                public float delay;
            }

            [Tooltip("The Quest this rule observes.")]
            public Quest quest;

            [Tooltip("The objects that will be toggled when this rule is evaluated.")]
            public List<QuestToggleTarget> targets = new();

            [FormerlySerializedAs("objects"), HideInInspector]
            public List<GameObject> legacyObjects = new();

            [FormerlySerializedAs("objectDelays"), HideInInspector]
            public List<float> legacyObjectDelays = new();

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
        protected readonly Dictionary<GameObject, Coroutine> m_pendingActions = new();

        protected virtual void OnValidate()
        {
            if (rules == null)
                return;

            foreach (var rule in rules)
                EnsureRuleTargets(rule);
        }

        protected virtual void Start()
        {
            InitializeManager();
            InitializeCallbacks();

            if (evaluateOnStart)
                EvaluateAllRules();
        }

        protected virtual void OnDestroy()
        {
            foreach (var action in m_pendingActions.Values)
            {
                if (action != null)
                    StopCoroutine(action);
            }

            m_pendingActions.Clear();

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

            EnsureRuleTargets(rule);

            var action = GetActionFromQuestState(rule);
            ApplyAction(rule, action);
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

        protected virtual void ApplyAction(QuestToggleRule rule, ToggleAction action)
        {
            if (action == ToggleAction.Ignore || rule == null || rule.targets == null)
                return;

            var active = action == ToggleAction.Enable;

            for (var i = 0; i < rule.targets.Count; i++)
            {
                if (rule.targets[i] == null)
                    continue;

                var target = rule.targets[i].target;

                if (target == null)
                    continue;

                if (m_pendingActions.TryGetValue(target, out var pending))
                {
                    if (pending != null)
                        StopCoroutine(pending);

                    m_pendingActions.Remove(target);
                }

                var delay = Mathf.Clamp01(rule.targets[i].delay);

                if (delay > 0f)
                {
                    var coroutine = StartCoroutine(ApplyActionDelayed(target, active, delay));
                    m_pendingActions[target] = coroutine;
                }
                else
                {
                    target.SetActive(active);
                }
            }
        }

        protected virtual IEnumerator ApplyActionDelayed(GameObject target, bool active, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (target != null)
                target.SetActive(active);

            m_pendingActions.Remove(target);
        }

        protected virtual void EnsureRuleTargets(QuestToggleRule rule)
        {
            if (rule == null)
                return;

            rule.targets ??= new List<QuestToggleRule.QuestToggleTarget>();
            rule.legacyObjects ??= new List<GameObject>();
            rule.legacyObjectDelays ??= new List<float>();

            if (rule.legacyObjects.Count == 0)
                return;

            for (var i = 0; i < rule.legacyObjects.Count; i++)
            {
                var legacyTarget = rule.legacyObjects[i];
                var legacyDelay = i < rule.legacyObjectDelays.Count ? Mathf.Clamp01(rule.legacyObjectDelays[i]) : 0f;

                if (i < rule.targets.Count)
                {
                    rule.targets[i] ??= new QuestToggleRule.QuestToggleTarget();

                    if (rule.targets[i].target == null && legacyTarget != null)
                    {
                        rule.targets[i].target = legacyTarget;
                        rule.targets[i].delay = legacyDelay;
                    }
                }
                else
                {
                    rule.targets.Add(new QuestToggleRule.QuestToggleTarget
                    {
                        target = legacyTarget,
                        delay = legacyDelay
                    });
                }
            }

            rule.legacyObjects.Clear();
            rule.legacyObjectDelays.Clear();
        }
    }
}