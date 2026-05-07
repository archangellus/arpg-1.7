using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject.SkillTreeUI
{
    public class SkillTreeUIController : MonoBehaviour
    {
        [Serializable]
        public class SkillTreeNode
        {
            public string id;
            public string displayName;
            public Skill skill;
            [Min(1)] public int skillPointCost = 1;
            [Min(0)] public int requiredLevel;
            public string[] requiredQuestIds;
            public Skill[] requiredSkills;

            [Header("Optional UI Binding")]
            [Tooltip("Optional node root used to auto-find the Learn Button, Learned State, and Status Text. Leave empty if the references below are assigned manually.")]
            public Transform root;
            public Button learnButton;
            public GameObject learnedState;
            public Text statusText;
        }

        [Header("Runtime Binding")]
        [Tooltip("How often the controller retries auto-resolving the player Entity and EntitySkillManager while they are unavailable.")]
        [Min(0.1f)] public float resolveRetryInterval = 0.5f;

        [Tooltip("When enabled, the controller tries to fill missing node UI references from each node root or matching child names.")]
        public bool autoBindMissingNodeReferences = true;

        [Tooltip("Logs a warning when a node is missing its Learn Button or Learned State reference after auto-binding.")]
        public bool warnAboutMissingNodeReferences = true;

        [Header("Progression")]
        [Min(0)] public int availableSkillPoints;
        public bool autoGrantPointOnLevelGain = true;

        [Header("UI")]
        public Text pointsText;
        public SkillTreeNode[] nodes;

        public Entity Entity => m_entity;
        public EntitySkillManager SkillManager => m_skillManager;

        private Entity m_entity;
        private EntitySkillManager m_skillManager;
        private int m_lastKnownLevel = -1;
        private float m_nextResolveTime;
        private readonly HashSet<string> m_completedQuests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> m_learnedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            TryResolveRuntimeReferences(force: true);
            ResolveNodeViewReferences();
            HookButtons();
            RefreshUI();
        }

        private void OnEnable()
        {
            EventBus.Subscribe(EventBus.QuestCompleted, OnQuestCompletedEvent);
            EventBus.Subscribe(EventBus.EntityLevelUp, OnEntityLevelUpEvent);
            TryResolveRuntimeReferences(force: true);
            CaptureCurrentLevel();
            RefreshUI();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe(EventBus.QuestCompleted, OnQuestCompletedEvent);
            EventBus.Unsubscribe(EventBus.EntityLevelUp, OnEntityLevelUpEvent);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                ResolveNodeViewReferences(logWarnings: false);
        }
#endif

        private void Update()
        {
            if (Time.unscaledTime >= m_nextResolveTime)
                TryResolveRuntimeReferences();

            if (m_entity && m_lastKnownLevel < 0)
                m_lastKnownLevel = m_entity.stats.level;
        }

        public bool TryLearnNode(string nodeId)
        {
            var node = nodes?.FirstOrDefault(n => string.Equals(n.id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (node == null)
                return false;

            return TryLearnNode(node);
        }

        public void MarkQuestCompleted(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
                return;

            m_completedQuests.Add(questId.Trim());
            RefreshUI();
        }

        public void RebindNodeViews()
        {
            ResolveNodeViewReferences();
            HookButtons();
            RefreshUI();
        }

        private void ResolveNodeViewReferences(bool logWarnings = true)
        {
            if (!autoBindMissingNodeReferences || nodes == null)
                return;

            foreach (var node in nodes)
            {
                if (node == null)
                    continue;

                var root = ResolveNodeRoot(node);

                if (!node.learnButton)
                    node.learnButton = FindNamedComponent<Button>(root, "Learn Button", "LearnButton", "Learn");

                if (!node.learnedState)
                    node.learnedState = FindNamedGameObject(root, "Learned State", "LearnedState", "Learned");

                if (!node.statusText)
                    node.statusText = FindNamedComponent<Text>(root, "Status Text", "StatusText", "Status");

                if (warnAboutMissingNodeReferences && logWarnings)
                    WarnIfNodeBindingsAreMissing(node);
            }
        }

        private Transform ResolveNodeRoot(SkillTreeNode node)
        {
            if (node == null)
                return transform;

            if (node.root)
                return node.root;

            if (node.learnButton)
                return node.learnButton.transform.parent ? node.learnButton.transform.parent : node.learnButton.transform;

            if (node.learnedState)
                return node.learnedState.transform.parent ? node.learnedState.transform.parent : node.learnedState.transform;

            if (node.statusText)
                return node.statusText.transform.parent ? node.statusText.transform.parent : node.statusText.transform;

            var namedRoot = FindNamedTransform(transform, node.id, node.displayName);
            return namedRoot ? namedRoot : transform;
        }

        private void WarnIfNodeBindingsAreMissing(SkillTreeNode node)
        {
            var label = !string.IsNullOrWhiteSpace(node.displayName) ? node.displayName : node.id;
            if (string.IsNullOrWhiteSpace(label))
                label = "<unnamed>";

            if (!node.learnButton)
            {
                Debug.LogWarning(
                    $"[SkillTreeUI] Node '{label}' is missing a Learn Button reference. " +
                    "Assign a UnityEngine.UI.Button or name the child object 'Learn Button'.",
                    this);
            }

            if (!node.learnedState)
            {
                Debug.LogWarning(
                    $"[SkillTreeUI] Node '{label}' is missing a Learned State reference. " +
                    "This is only a GameObject toggle; no extra script is required. Name the child object 'Learned State' or assign it manually.",
                    this);
            }
        }

        private static Transform FindNamedTransform(Transform root, params string[] names)
        {
            if (!root || names == null || names.Length == 0)
                return null;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (NameMatches(child.name, names, exact: true))
                    return child;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (NameMatches(child.name, names, exact: false))
                    return child;
            }

            return null;
        }

        private static T FindNamedComponent<T>(Transform root, params string[] names) where T : Component
        {
            if (!root)
                return null;

            var components = root.GetComponentsInChildren<T>(true);

            foreach (var component in components)
            {
                if (NameMatches(component.name, names, exact: true))
                    return component;
            }

            foreach (var component in components)
            {
                if (NameMatches(component.name, names, exact: false))
                    return component;
            }

            return components.FirstOrDefault();
        }

        private static GameObject FindNamedGameObject(Transform root, params string[] names)
        {
            var transform = FindNamedTransform(root, names);
            return transform ? transform.gameObject : null;
        }

        private static bool NameMatches(string candidate, string[] names, bool exact)
        {
            if (string.IsNullOrWhiteSpace(candidate) || names == null)
                return false;

            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (exact)
                {
                    if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (candidate.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void HookButtons()
        {
            if (nodes == null)
                return;

            foreach (var node in nodes)
            {
                if (node?.learnButton == null)
                    continue;

                node.learnButton.onClick.RemoveAllListeners();
                node.learnButton.onClick.AddListener(() => TryLearnNode(node));
            }
        }

        private bool TryResolveRuntimeReferences(bool force = false)
        {
            if (!force && m_entity != null && m_skillManager != null)
                return true;

            m_nextResolveTime = Time.unscaledTime + Mathf.Max(0.1f, resolveRetryInterval);

            if (m_entity == null && Level.instance != null)
                m_entity = Level.instance.player;

            if (m_skillManager == null)
            {
                if (m_entity != null)
                    m_skillManager = m_entity.skills;
                else
                    m_skillManager = FindFirstObjectByType<EntitySkillManager>(FindObjectsInactive.Exclude);
            }

            if (m_entity == null && m_skillManager != null)
                m_entity = m_skillManager.GetComponent<Entity>();

            return m_entity != null && m_skillManager != null;
        }

        private bool TryLearnNode(SkillTreeNode node)
        {
            if (node == null || !CanLearnNode(node, out _))
                return false;

            if (m_skillManager == null || node.skill == null)
                return false;

            if (!m_skillManager.TryLearnSkill(node.skill))
                return false;

            availableSkillPoints -= node.skillPointCost;
            m_learnedNodeIds.Add(node.id);
            RefreshUI();
            return true;
        }

        private bool CanLearnNode(SkillTreeNode node, out string reason)
        {
            reason = "Ready";

            if (node == null)
            {
                reason = "Invalid node";
                return false;
            }

            if (string.IsNullOrWhiteSpace(node.id))
            {
                reason = "Missing node id";
                return false;
            }

            if (m_learnedNodeIds.Contains(node.id) || (m_skillManager != null && node.skill != null && m_skillManager.skills.Contains(node.skill)))
            {
                reason = "Learned";
                return false;
            }

            if (availableSkillPoints < node.skillPointCost)
            {
                reason = "Not enough points";
                return false;
            }

            var level = m_entity ? m_entity.stats.level : 0;
            if (level < node.requiredLevel)
            {
                reason = $"Requires level {node.requiredLevel}";
                return false;
            }

            if (node.requiredSkills != null)
            {
                foreach (var requiredSkill in node.requiredSkills)
                {
                    if (!requiredSkill)
                        continue;

                    if (m_skillManager == null || !m_skillManager.skills.Contains(requiredSkill))
                    {
                        reason = $"Requires skill: {requiredSkill.name}";
                        return false;
                    }
                }
            }

            if (node.requiredQuestIds != null)
            {
                foreach (var questId in node.requiredQuestIds)
                {
                    if (string.IsNullOrWhiteSpace(questId))
                        continue;

                    if (!m_completedQuests.Contains(questId.Trim()))
                    {
                        reason = $"Requires quest: {questId}";
                        return false;
                    }
                }
            }

            return true;
        }

        private void OnEntityLevelUpEvent(object payload)
        {
            if (!autoGrantPointOnLevelGain)
                return;

            if (payload is not Entity leveledEntity)
                return;

            if (!TryResolveRuntimeReferences() || m_entity == null || leveledEntity != m_entity)
                return;

            availableSkillPoints += 1;
            m_lastKnownLevel = m_entity.stats.level;
            RefreshUI();
        }

        private void OnQuestCompletedEvent(object payload)
        {
            if (payload == null)
                return;

            if (payload is Quest quest)
            {
                MarkQuestCompleted(quest.name);
                return;
            }

            if (payload is string questId)
            {
                MarkQuestCompleted(questId);
                return;
            }

            if (payload is object[] args && args.Length > 0)
            {
                if (args[0] is Quest q)
                    MarkQuestCompleted(q.name);
                else if (args[0] is string qid)
                    MarkQuestCompleted(qid);
            }
        }

        private void CaptureCurrentLevel()
        {
            m_lastKnownLevel = m_entity ? m_entity.stats.level : -1;
        }

        private void RefreshUI()
        {
            if (pointsText)
                pointsText.text = $"Skill Points: {availableSkillPoints}";

            if (nodes == null)
                return;

            foreach (var node in nodes)
            {
                if (node == null)
                    continue;

                var canLearn = CanLearnNode(node, out var reason);
                var learned = reason == "Learned";

                if (node.learnButton)
                    node.learnButton.interactable = canLearn;

                if (node.learnedState)
                    node.learnedState.SetActive(learned);

                if (node.statusText)
                    node.statusText.text = reason;
            }
        }
    }
}
