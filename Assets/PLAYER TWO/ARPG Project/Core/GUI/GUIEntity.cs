using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Entity")]
    public class GUIEntity : Singleton<GUIEntity>
    {
        [Header("Health Settings")]
        [Tooltip("A reference to the Image component used to display the Player health.")]
        public Image healthImage;

        [Tooltip("A reference to the Text component used to display the health points.")]
        public Text healthText;

        [Header("Mana Settings")]
        [Tooltip("A reference to the Image component used to display the Player mana.")]
        public Image manaImage;

        [Tooltip("A reference to the Text component used to display the mana points.")]
        public Text manaText;

        [Header("Experience Settings")]
        [Tooltip("References the experience bar Image component.")]
        public Image experienceImage;

        [Tooltip("References the experience Text component.")]
        public Text experienceText;

        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when the Player selects a Skill.")]
        public AudioClip selectSkillAudio;

        [Header("HUD Events")]
        public UnityEvent<GUIItem> onEquipConsumable;
        public UnityEvent<GUIItem> onUnequipConsumable;

        protected Entity m_entity;
        protected GUISkillSlot m_currentSkill;

        protected GUIConsumableSlot[] m_consumableSlots;
        protected GUISkillSlot[] m_skillSlots;

        protected virtual void InitializeEntity() => m_entity = Level.instance.player;

        protected virtual void InitializeConsumableSlots()
        {
            m_consumableSlots = GetComponentsInChildren<GUIConsumableSlot>();

            var consumables = m_entity.items.GetConsumables();

            for (int i = 0; i < consumables.Length; i++)
            {
                if (consumables[i] == null)
                    continue;

                m_consumableSlots[i].Equip(GUI.instance.CreateGUIItem(consumables[i]));
            }
        }

        protected virtual void InitializeSkillsSlots() =>
            m_skillSlots = GetComponentsInChildren<GUISkillSlot>();

        protected virtual void InitializeSkillsSlotsCallbacks()
        {
            foreach (var slot in m_skillSlots)
            {
                slot.onIconClick.AddListener(() =>
                {
                    m_entity.skills.ChangeTo(slot.skill);
                });
            }
        }

        protected virtual void InitializeCallbacks()
        {
            m_entity.stats.onHealthChanged.AddListener(UpdateHealth);
            m_entity.stats.onManaChanged.AddListener(UpdateMana);
            m_entity.stats.onExperienceChanged.AddListener(UpdateExperience);
            m_entity.stats.onRecalculate.AddListener(() =>
            {
                UpdateHealth();
                UpdateMana();
                UpdateExperience();
            });
        }

        protected virtual void InitializeConsumablesCallbacks()
        {
            m_entity.items.onConsumeItem.AddListener(
                (index) =>
                {
                    if (m_consumableSlots[index].item == null)
                        return;

                    if (m_consumableSlots[index].item.item.stack == 0)
                        m_consumableSlots[index].Clear();
                }
            );

            for (int i = 0; i < m_consumableSlots.Length; i++)
            {
                var index = i;

                m_consumableSlots[i]
                    .onEquip.AddListener(
                        (guiItem) =>
                        {
                            m_entity.items.SetConsumable(index, guiItem.item);
                            onEquipConsumable.Invoke(guiItem);
                        }
                    );

                m_consumableSlots[i]
                    .onUnequip.AddListener(
                        (guiItem) =>
                        {
                            m_entity.items.SetConsumable(index, null);
                            onUnequipConsumable.Invoke(guiItem);
                        }
                    );
            }
        }

        protected virtual void InitializeSkillsCallbacks()
        {
            m_entity.skills.onChanged.AddListener((_) => UpdateSelectedSkill());
            m_entity.skills.onUpdatedEquippedSkills.AddListener((_) => UpdateSkills());
            m_entity.skills.onPerform.AddListener(
                (skill) => m_currentSkill.StartCoolDown(skill.data.coolDown)
            );
        }

        protected virtual void InitializeHUD()
        {
            UpdateHealth();
            UpdateMana();
            UpdateExperience();
            UpdateSkills();
        }

        /// <summary>
        /// Updates the health image and text.
        /// </summary>
        public virtual void UpdateHealth()
        {
            if (healthImage)
                healthImage.fillAmount = m_entity.stats.GetHealthPercent();

            if (healthText)
                healthText.text = $"{m_entity.stats.health} / {m_entity.stats.maxHealth}";
        }

        /// <summary>
        /// Updates the mana image and text.
        /// </summary>
        public virtual void UpdateMana()
        {
            if (manaImage)
                manaImage.fillAmount = m_entity.stats.GetManaPercent();

            if (manaText)
                manaText.text = $"{m_entity.stats.mana} / {m_entity.stats.maxMana}";
        }

        /// <summary>
        /// Updates the experience mana and text.
        /// </summary>
        public virtual void UpdateExperience()
        {
            if (experienceImage)
                experienceImage.fillAmount = m_entity.stats.GetExperiencePercent();

            if (experienceText)
                experienceText.text =
                    $"{m_entity.stats.experience} / {m_entity.stats.nextLevelExp}";
        }

        /// <summary>
        /// Updates the Skills slots.
        /// </summary>
        public virtual void UpdateSkills()
        {
            for (int i = 0; i < m_skillSlots.Length; i++)
            {
                if (i >= m_entity.skills.equipped.Count || m_entity.skills.equipped[i] == null)
                {
                    m_skillSlots[i].SetSkill(null);
                    continue;
                }

                var remainingCoolDown = m_entity.skills.equipped[i].GetRemainingCoolDown();
                m_skillSlots[i]
                    .SetSkill(m_entity.skills.equipped[i].data, false, remainingCoolDown);
            }

            UpdateSelectedSkill();
        }

        /// <summary>
        /// Updates the selected Skill.
        /// </summary>
        public virtual void UpdateSelectedSkill()
        {
            if (m_currentSkill)
                m_currentSkill.Select(false);

            if (m_entity.skills.current)
            {
                if (Time.timeSinceLevelLoad > 0)
                    GameAudio.instance.PlayUiEffect(selectSkillAudio);

                m_currentSkill = m_skillSlots[m_entity.skills.index];
                m_currentSkill.Select(true);
            }
        }

        /// <summary>
        /// Tries to equip a consumable.
        /// </summary>
        /// <param name="item">The GUI Item of the consumable.</param>
        /// <returns>Returns true if the item was equipped.</returns>
        public virtual bool TryEquipConsumable(GUIItem item)
        {
            for (int i = 0; i < m_consumableSlots.Length; i++)
            {
                if (m_consumableSlots[i].CanEquip(item))
                {
                    m_consumableSlots[i].Equip(item);
                    return true;
                }
            }

            return false;
        }

        protected virtual void Start()
        {
            InitializeEntity();
            InitializeConsumableSlots();
            InitializeSkillsSlots();
            InitializeSkillsSlotsCallbacks();
            InitializeCallbacks();
            InitializeConsumablesCallbacks();
            InitializeSkillsCallbacks();
            InitializeHUD();
        }
    }
}
