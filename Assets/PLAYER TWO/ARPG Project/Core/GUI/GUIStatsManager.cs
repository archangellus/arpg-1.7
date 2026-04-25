using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Stats Manager")]
    public class GUIStatsManager : MonoBehaviour
    {
        [Header("GUI Texts")]
        [Tooltip("A reference to the Text component that represents the Stats level.")]
        public Text levelText;

        [Tooltip("A reference to the Text component that represents the Stats available points.")]
        public Text availablePointsText;

        [Tooltip("A reference to the Text component that represents the Stats current experience points.")]
        public Text currentExpText;

        [Tooltip("A reference to the Text component that represents the Stats next level experience points.")]
        public Text nextLevelExp;

        [Tooltip("A reference to the Text component that represents the Stats damage points.")]
        public Text damageText;

        [Tooltip("A reference to the Text component that represents the Stats defense points.")]
        public Text defenseText;

        [Tooltip("A reference to the Text component that represents the Stats attack speed. points")]
        public Text attackSpeedText;

        [Tooltip("A reference to the Text component that represents the Stats maximum health points.")]
        public Text maxHealthText;

        [Tooltip("A reference to the Text component that represents the Stats maximum mana points.")]
        public Text maxManaText;

        [Tooltip("A reference to the Text component that represents the Stats magic damage points.")]
        public Text magicDamageText;

        [Header("GUI Attributes")]
        [Tooltip("A reference to the GUI Stats Attributes representing strength points.")]
        public GUIStatsAttribute strength;

        [Tooltip("A reference to the GUI Stats Attributes representing dexterity points.")]
        public GUIStatsAttribute dexterity;

        [Tooltip("A reference to the GUI Stats Attributes representing vitality points.")]
        public GUIStatsAttribute vitality;

        [Tooltip("A reference to the GUI Stats Attributes representing energy points.")]
        public GUIStatsAttribute energy;

        protected Entity m_entity;

        public event System.Action<int> onPointsChanged;

        protected int m_availablePoints;

        /// <summary>
        /// Returns the current available points to distribute.
        /// </summary>
        public int availablePoints
        {
            get
            {
                return m_availablePoints;
            }

            set
            {
                m_availablePoints = Mathf.Max(0, value);
                onPointsChanged?.Invoke(m_availablePoints);
                availablePointsText.text = m_availablePoints.ToString();
            }
        }

        protected virtual void InitializeEntity() => m_entity = Level.instance.player;

        protected virtual void InitializeCallbacks()
        {
            m_entity.stats.onLevelUp.AddListener(Refresh);
            m_entity.stats.onRecalculate.AddListener(Refresh);
            m_entity.stats.onExperienceChanged.AddListener(() =>
                currentExpText.text = m_entity.stats.experience.ToString());
        }

        protected virtual void InitializeTexts()
        {
            levelText.text = m_entity.stats.level.ToString();
            availablePointsText.text = m_availablePoints.ToString();
        }

        /// <summary>
        /// Applies all distributed points to the Player's Stats Manager.
        /// </summary>
        public virtual void Apply() => m_entity.stats.BulkDistribute(strength.distributedPoints,
            dexterity.distributedPoints, vitality.distributedPoints, energy.distributedPoints);

        /// <summary>
        /// Refreshes all attributes to display the current values.
        /// </summary>
        public virtual void Refresh()
        {
            levelText.text = m_entity.stats.level.ToString();
            currentExpText.text = m_entity.stats.experience.ToString();
            nextLevelExp.text = m_entity.stats.nextLevelExp.ToString();
            damageText.text = $"{m_entity.stats.minDamage} - {m_entity.stats.maxDamage}";
            defenseText.text = m_entity.stats.defense.ToString();
            attackSpeedText.text = $"{m_entity.stats.attackSpeed.ToString()} / {Game.instance.maxAttackSpeed}";
            maxHealthText.text = m_entity.stats.maxHealth.ToString();
            maxManaText.text = m_entity.stats.maxMana.ToString();
            magicDamageText.text = $"{m_entity.stats.minMagicDamage} - {m_entity.stats.maxMagicDamage}";
            availablePoints = m_entity.stats.availablePoints;
            strength.Reset(m_entity.stats.strength);
            dexterity.Reset(m_entity.stats.dexterity);
            vitality.Reset(m_entity.stats.vitality);
            energy.Reset(m_entity.stats.energy);
        }

        protected virtual void Start()
        {
            InitializeEntity();
            InitializeCallbacks();
            InitializeTexts();
            Refresh();
        }
    }
}
