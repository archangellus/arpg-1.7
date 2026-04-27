using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Skill Inspector")]
    public class GUISkillInspector : GUIInspector<GUISkillInspector>
    {
        [Tooltip("A reference to the Text component used as the Skill name.")]
        public Text skillName;

        [Tooltip("A reference to the Text component used as the Skill mana cost.")]
        public Text manaCost;

        [Tooltip("A reference to the Text component used as the Skill health cost.")]
        public Text bloodCost;

        [Tooltip("A reference to the Text component used as the Skill damage.")]
        public Text damage;

        [Tooltip("A reference to the Text component used as the Skill damage mode.")]
        public Text damageMode;

        [Tooltip("A reference to the Text component used as the Skill effect description.")]
        public Text effectDescription;

        protected bool m_visible = true;

        protected CanvasGroup m_group;
        protected Skill m_skill;

        protected virtual void InitializeCanvasGroup()
        {
            if (!TryGetComponent(out m_group))
                m_group = gameObject.AddComponent<CanvasGroup>();

            m_group.blocksRaycasts = false;
        }

        /// <summary>
        /// Shows the inspector with the information from a given Skill.
        /// </summary>
        /// <param name="skill">The Skill you want inspect.</param>
        public virtual void Show(Skill skill)
        {
            if (!skill || gameObject.activeSelf) return;

            m_skill = skill;
            gameObject.SetActive(true);
            UpdateAll();
            FadIn();
        }

        /// <summary>
        /// Hides the inspector.
        /// </summary>
        public virtual void Hide()
        {
            if (!gameObject.activeSelf) return;

            gameObject.SetActive(false);
        }

        protected virtual void UpdateAll()
        {
            UpdateSkillName();
            UpdateManaCost();
            UpdateBloodCost();
            UpdateDamage();
            UpdateEffect();
        }

        protected virtual void UpdateSkillName() => skillName.text = m_skill.name;

        protected virtual void UpdateManaCost()
        {
            manaCost.gameObject.SetActive(m_skill.useMana);

            if (manaCost.gameObject.activeSelf)
                manaCost.text = $"Mana Cost: {m_skill.manaCost.ToString()}";
        }

        protected virtual void UpdateBloodCost()
        {
            bloodCost.gameObject.SetActive(m_skill.useBlood);

            if (bloodCost.gameObject.activeSelf)
                bloodCost.text = $"Blood Cost: {m_skill.bloodCost}";
        }

        protected virtual void UpdateDamage()
        {
            damage.gameObject.SetActive(m_skill.IsAttack());
            damageMode.gameObject.SetActive(m_skill.IsAttack());

            if (damage.gameObject.activeSelf)
                damage.text = $"Damage: {m_skill.AsAttack().minDamage} ~ {m_skill.AsAttack().maxDamage}";

            if (damageMode.gameObject.activeSelf)
                damageMode.text = $"Damage Mode: {m_skill.AsAttack().damageMode.ToString()}";
        }

        protected virtual void UpdateEffect()
        {
            effectDescription.gameObject.SetActive(m_skill.useHealing);

            if (effectDescription.gameObject.activeSelf)
                effectDescription.text = $"Increases Health by {m_skill.healingAmount}";
        }

        protected virtual void Start()
        {
            InitializeCanvasGroup();
            Hide();
        }
    }
}
