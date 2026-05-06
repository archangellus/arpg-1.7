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
            public Button learnButton;
            public GameObject learnedState;
            public Text statusText;
        }

        [Header("Target")]
        [Tooltip("Optional explicit entity reference. Leave empty to auto-resolve Level.instance.player at runtime.")]
        public Entity entity;
        [Tooltip("Optional explicit skill manager reference. Leave empty for auto-resolution.")]
        public EntitySkillManager skillManager;

        [Header("Runtime Binding")]
        [Min(0.1f)] public float resolveRetryInterval = 0.5f;

        [Header("Progression")]
        [Min(0)] public int availableSkillPoints;
        public bool autoGrantPointOnLevelGain = true;

        [Header("UI")]
        public Text pointsText;
        public SkillTreeNode[] nodes;

        private int m_lastKnownLevel = -1;
        private float m_nextResolveTime;
        private readonly HashSet<string> m_completedQuests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> m_learnedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            TryResolveRuntimeReferences(force: true);
            HookButtons();
            RefreshUI();
        }

        private void OnEnable()
        {
            EventBus.Subscribe(EventBus.QuestCompleted, OnQuestCompletedEvent);
            CaptureCurrentLevel();
            RefreshUI();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe(EventBus.QuestCompleted, OnQuestCompletedEvent);
        }

        private void Update()
        {
            if (Time.unscaledTime >= m_nextResolveTime)
                TryResolveRuntimeReferences();

            if (!autoGrantPointOnLevelGain || !entity)
                return;

            var currentLevel = entity.stats.level;

            if (m_lastKnownLevel < 0)
                m_lastKnownLevel = currentLevel;

            if (currentLevel <= m_lastKnownLevel)
                return;

            var gainedLevels = currentLevel - m_lastKnownLevel;
            availableSkillPoints += gainedLevels;
            m_lastKnownLevel = currentLevel;
            RefreshUI();
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
            if (!force && entity != null && skillManager != null)
                return true;

            m_nextResolveTime = Time.unscaledTime + Mathf.Max(0.1f, resolveRetryInterval);

            if (entity == null && Level.instance != null)
                entity = Level.instance.player;

            if (skillManager == null)
            {
                if (entity != null)
                    skillManager = entity.skills;
                else
                    skillManager = FindFirstObjectByType<EntitySkillManager>(FindObjectsInactive.Exclude);
            }

            if (entity == null && skillManager != null)
                entity = skillManager.GetComponent<Entity>();

            return entity != null && skillManager != null;
        }

        private bool TryLearnNode(SkillTreeNode node)
        {
            if (node == null || !CanLearnNode(node, out _))
                return false;

            if (skillManager == null || node.skill == null)
                return false;

            if (!skillManager.TryLearnSkill(node.skill))
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

            if (m_learnedNodeIds.Contains(node.id) || (skillManager != null && node.skill != null && skillManager.skills.Contains(node.skill)))
            {
                reason = "Learned";
                return false;
            }

            if (availableSkillPoints < node.skillPointCost)
            {
                reason = "Not enough points";
                return false;
            }

            var level = entity ? entity.stats.level : 0;
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

                    if (skillManager == null || !skillManager.skills.Contains(requiredSkill))
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
            m_lastKnownLevel = entity ? entity.stats.level : -1;
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
