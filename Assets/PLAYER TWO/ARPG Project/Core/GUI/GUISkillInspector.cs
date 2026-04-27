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
            if (!skill || gameObject.activeSelf)
                return;

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
            if (!gameObject.activeSelf)
                return;

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
                damage.text =
                    $"Damage: {m_skill.AsAttack().minDamage} ~ {m_skill.AsAttack().maxDamage}";

            if (damageMode.gameObject.activeSelf)
                damageMode.text = $"Damage Mode: {m_skill.AsAttack().damageMode.ToString()}";
        }

        protected virtual void UpdateEffect()
        {
            var hasSelfEffects = m_skill.selfEffects != null && m_skill.selfEffects.Length > 0;
            var hasTargetEffects =
                m_skill.targetEffects != null && m_skill.targetEffects.Length > 0;
            var hasEffect = m_skill.useHealing || hasSelfEffects || hasTargetEffects;
            effectDescription.gameObject.SetActive(hasEffect);

            if (!effectDescription.gameObject.activeSelf)
                return;

            var lines = new System.Text.StringBuilder();

            if (m_skill.useHealing)
                lines.AppendLine($"Increases Health by {m_skill.healingAmount}");

            if (hasSelfEffects)
                foreach (var effect in m_skill.selfEffects)
                    lines.AppendLine(FormatEffectLine("self", effect, m_skill.selfEffectChance));

            if (hasTargetEffects)
                foreach (var effect in m_skill.targetEffects)
                    lines.AppendLine(
                        FormatEffectLine("target", effect, m_skill.targetEffectChance)
                    );

            effectDescription.text = lines.ToString().TrimEnd();
        }

        /// <summary>
        /// Formats a single effect line.
        /// Debuffs use "Causes &lt;name&gt; to &lt;scope&gt;" at 100% chance,
        /// or "X% chance of causing &lt;name&gt; to &lt;scope&gt;" otherwise.
        /// Buffs display the full modifier list, prefixed with "X% chance of applying effects:"
        /// when the chance is below 100%.
        /// </summary>
        protected virtual string FormatEffectLine(string scope, EntityEffect effect, float chance)
        {
            if (effect is EntityDebuff)
                return chance < 1f
                    ? $"{Mathf.RoundToInt(chance * 100)}% chance of causing {effect.effectName} to {scope}"
                    : $"Causes {effect.effectName} to {scope}";

            var modifiers = effect.GetModifiersText();

            return chance < 1f
                ? $"{Mathf.RoundToInt(chance * 100)}% chance of applying effects:\n{modifiers}"
                : modifiers;
        }

        protected virtual void Start()
        {
            InitializeCanvasGroup();
            Hide();
        }
    }
}
