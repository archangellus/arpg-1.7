using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/UI/UI Summon Material Binder")]
    public class UISummonMaterialBinder : MonoBehaviour
    {
        [Tooltip("Name (or partial name) to search among this object's children. All matching Images are used.")]
        public string targetImageName = "Summon";

        [Tooltip("If true, include inactive Image GameObjects when resolving targets. Useful for selection overlays controlled by other scripts.")]
        public bool includeInactiveTargets = true;

        [Tooltip("If true, matching Images are force-enabled while any summon is active and disabled otherwise.")]
        public bool controlImageEnabled = false;

        [Tooltip("If true, only images belonging to GUISkillSlot components with summon skills are affected.")]
        public bool onlySummonSkillSlots = true;

        [Tooltip("Material applied to matching Image components while the player has any active summon (entity summon or pet summon).")]
        public Material activeSummonMaterial;

        [Tooltip("How often summon state should be refreshed.")]
        [Min(0.05f)]
        public float refreshInterval = 0.2f;

        private readonly List<Image> m_targetImages = new();
        private readonly Dictionary<Image, Material> m_originalMaterials = new();
        private readonly Dictionary<Image, bool> m_originalEnabled = new();

        private float m_nextRefreshTime;

        private void OnEnable()
        {
            ResolveTargetImages();
            Refresh();
        }

        private void Update()
        {
            if (Time.time < m_nextRefreshTime)
                return;

            m_nextRefreshTime = Time.time + refreshInterval;
            Refresh();
        }

        private void OnDisable()
        {
            RestoreAllOriginals();
        }

        public void Refresh()
        {
            ResolveTargetImages();

            ApplyVisualState();
        }

        private void ResolveTargetImages()
        {
            m_targetImages.Clear();

            var images = GetComponentsInChildren<Image>(includeInactiveTargets);
            if (images == null || images.Length == 0)
                return;

            string filter = targetImageName?.Trim();

            foreach (var image in images)
            {
                if (image == null)
                    continue;

                bool matches = string.IsNullOrEmpty(filter) ||
                    image.name.Equals(filter, System.StringComparison.OrdinalIgnoreCase) ||
                    image.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (!matches)
                    continue;

                if (onlySummonSkillSlots && !BelongsToSummonSkillSlot(image))
                    continue;

                m_targetImages.Add(image);

                if (!m_originalMaterials.ContainsKey(image))
                    m_originalMaterials[image] = image.material;

                if (!m_originalEnabled.ContainsKey(image))
                    m_originalEnabled[image] = image.enabled;
            }
        }

        private bool BelongsToSummonSkillSlot(Image image)
        {
            var slot = image.GetComponentInParent<GUISkillSlot>(true);

            if (slot == null || slot.skill == null)
                return false;

            return slot.skill is SkillSummon || slot.skill is SkillSummonPet;
        }

        private bool IsSkillSummonActive(Skill slotSkill)
        {
            if (Level.instance == null || Level.instance.player == null)
                return false;

            if (slotSkill == null)
                return false;

            var player = Level.instance.player;
            int playerId = player.GetInstanceID();
            int skillId = slotSkill.GetInstanceID();

            var ownerships = Object.FindObjectsByType<SummonSkillOwnership>(FindObjectsSortMode.None);

            foreach (var ownership in ownerships)
            {
                if (ownership == null || !ownership.gameObject.activeInHierarchy)
                    continue;

                if (ownership.ownerEntityId == playerId && ownership.skillInstanceId == skillId)
                    return true;
            }

            return false;
        }

        private void RestoreAllOriginals()
        {
            for (int i = m_targetImages.Count - 1; i >= 0; i--)
            {
                var image = m_targetImages[i];
                if (image == null)
                    continue;

                if (controlImageEnabled && m_originalEnabled.TryGetValue(image, out var wasEnabled))
                    image.enabled = wasEnabled;

                if (m_originalMaterials.TryGetValue(image, out var originalMaterial))
                    image.material = originalMaterial;
            }
        }

        private void ApplyVisualState()
        {
            for (int i = m_targetImages.Count - 1; i >= 0; i--)
            {
                var image = m_targetImages[i];

                if (image == null)
                {
                    m_targetImages.RemoveAt(i);
                    continue;
                }

                var slot = image.GetComponentInParent<GUISkillSlot>(true);
                bool isSummonActive = slot != null && IsSkillSummonActive(slot.skill);

                if (isSummonActive)
                {
                    if (controlImageEnabled)
                        image.enabled = true;

                    if (activeSummonMaterial != null)
                        image.material = activeSummonMaterial;
                }
                else
                {
                    if (controlImageEnabled && m_originalEnabled.TryGetValue(image, out var wasEnabled))
                        image.enabled = wasEnabled;

                    if (m_originalMaterials.TryGetValue(image, out var originalMaterial))
                        image.material = originalMaterial;
                }
            }
        }
    }
}