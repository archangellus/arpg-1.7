using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Entity))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Animator")]
    public class EntityAnimator : MonoBehaviour
    {
        [Header("Default Settings")]
        [Tooltip("The default animator controller override.")]
        public AnimatorOverrideController defaultAnimations;

        [Header("Stance Override Settings")]
        [Tooltip(
            "If true, the animator will be overridden by the override correspondent to the equipped items."
        )]
        public bool useStanceOverride;

        [Tooltip("The animator override used when the Entity is equipped one handed blades.")]
        public AnimatorOverrideController oneHandSword;

        [Tooltip("The animator override used when the Entity is equipped two handed blades.")]
        public AnimatorOverrideController twoHandSword;

        [Tooltip(
            "The animator override used when the Entity is equipped with an one handed sword and a shield."
        )]
        public AnimatorOverrideController swordAndShield;

        [Tooltip(
            "The animator override used when the Entity is equipped with two one handed blades."
        )]
        public AnimatorOverrideController dualWield;

        [Tooltip("The animator override used when the Entity is equipped with a bow.")]
        public AnimatorOverrideController bow;

        [Tooltip("The animator override used when the Entity is equipped with a cross bow.")]
        public AnimatorOverrideController crossbow;

        [Header("Clip Names")]
        [Tooltip("Name of the first attack combo clip in the animator controller.")]
        public string attackClipName = "Attack Combo 1";

        [Tooltip(
            "Base name prefix for combo attack clips (e.g. 'Attack Combo ' becomes 'Attack Combo 1', 'Attack Combo 2', etc.)."
        )]
        public string comboBaseClipName = "Attack Combo ";

        [Tooltip("Name of the skill casting clip in the animator controller.")]
        public string skillClipName = "Skill Casting";

        [Tooltip("Name of the blocking clip in the animator controller.")]
        public string blockClipName = "Blocking";

        [Tooltip("Name of the stunned clip in the animator controller.")]
        public string stunClipName = "Stunned";

        [Header("Base Animation Speed Multipliers")]
        [Tooltip("Base attack speed multiplier. Increase to make attack animations faster.")]
        public float baseAttackSpeed = 1;

        [Tooltip("Base block speed multiplier. Increase to make block animation faster.")]
        public float baseBlockSpeed = 1;

        [Tooltip("The base stun speed multiplier. Increase to make stun animation faster.")]
        public float baseStunSpeed = 1;

        protected Entity m_entity;
        protected Animator m_animator;

        protected AnimatorOverrideController m_overrides;
        protected AnimatorOverrideController m_stanceOverrides;

        protected int m_speedHash;
        protected int m_onAttackHash;
        protected int m_onMagicAttackHash;
        protected int m_onDieHash;
        protected int m_onBlockHash;
        protected int m_onStunnedHash;
        protected int m_attackSpeedHash;
        protected int m_blockSpeedHash;
        protected int m_stunSpeedHash;
        protected int m_comboIndexHash;

        protected virtual void InitializeEntity() => m_entity = GetComponent<Entity>();

        protected virtual void InitializeAnimator() =>
            m_animator = GetComponentInChildren<Animator>();

        protected virtual void InitializeHashes()
        {
            m_speedHash = Animator.StringToHash("Speed");
            m_onAttackHash = Animator.StringToHash("On Attack");
            m_onMagicAttackHash = Animator.StringToHash("On Magic Attack");
            m_onDieHash = Animator.StringToHash("On Die");
            m_onBlockHash = Animator.StringToHash("On Block");
            m_onStunnedHash = Animator.StringToHash("On Stunned");
            m_attackSpeedHash = Animator.StringToHash("Attack Speed");
            m_blockSpeedHash = Animator.StringToHash("Block Speed");
            m_stunSpeedHash = Animator.StringToHash("Stun Speed");
            m_comboIndexHash = Animator.StringToHash("Combo Index");
        }

        protected virtual void InitializeTriggers()
        {
            m_entity.onAttack.AddListener(TriggerAttack);
            m_entity.onMagicAttack.AddListener(() => m_animator.SetTrigger(m_onMagicAttackHash));
            m_entity.onDie.AddListener(() => m_animator.SetTrigger(m_onDieHash));
            m_entity.onBlock.AddListener(() => m_animator.SetTrigger(m_onBlockHash));
            m_entity.onStunned.AddListener(() => m_animator.SetTrigger(m_onStunnedHash));
        }

        protected virtual void InitializeOverride()
        {
            m_overrides = new AnimatorOverrideController(m_animator.runtimeAnimatorController);
            m_stanceOverrides = new AnimatorOverrideController(
                m_animator.runtimeAnimatorController
            );
            m_animator.runtimeAnimatorController = m_overrides;
        }

        protected virtual void InitializeStance() => UpdateStance();

        protected virtual void InitializeSkill() => UpdateSkillOverride(m_entity.skills.current);

        protected virtual void InitializeCallbacks()
        {
            if (m_entity.items)
                m_entity.items.onChanged.AddListener(() =>
                {
                    UpdateStance();
                    UpdateSkillOverride(m_entity.skills.current);
                });

            m_entity.skills.onChanged.AddListener(UpdateSkillOverride);
            m_entity.onRevive.AddListener(ResetStateMachine);
        }

        /// <summary>
        /// Updates the animator override controller based on the current equipped items.
        /// </summary>
        public virtual void UpdateStance()
        {
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

            defaultAnimations.GetOverrides(overrides);

            if (!useStanceOverride || !m_entity.items)
            {
                SetAnimationOverride(overrides);
                return;
            }

            if (m_entity.items.IsUsingBlade())
            {
                oneHandSword.GetOverrides(overrides);

                if (m_entity.items.GetRightBlade().IsTwoHanded())
                {
                    twoHandSword.GetOverrides(overrides);
                }
                else if (m_entity.items.IsUsingShield())
                {
                    swordAndShield.GetOverrides(overrides);
                }
                else if (m_entity.items.IsUsingBladeLeft())
                {
                    dualWield.GetOverrides(overrides);
                }
            }
            else if (m_entity.items.IsUsingBow())
            {
                if (m_entity.items.GetBow().IsBow())
                {
                    bow.GetOverrides(overrides);
                }
                else if (m_entity.items.GetBow().IsCrossBow())
                {
                    crossbow.GetOverrides(overrides);
                }
            }

            SetAnimationOverride(overrides);
        }

        /// <summary>
        /// Updates the animation clip of the 'Skill' animation based on a given Skill.
        /// </summary>
        /// <param name="skill">The Skill tor read the animation from.</param>
        public virtual void UpdateSkillOverride(Skill skill)
        {
            if (!skill)
                return;

            if (skill.useRegularAttackClip)
            {
                m_overrides[skillClipName] = m_stanceOverrides[attackClipName];
            }
            else if (skill.overrideClip)
            {
                m_overrides[skillClipName] = skill.overrideClip;
            }
            else
            {
                m_overrides[skillClipName] = m_stanceOverrides[skillClipName];
            }
        }

        /// <summary>
        /// Resets the Animator State Machine to its initial state.
        /// </summary>
        public virtual void ResetStateMachine()
        {
            m_animator.Rebind();
            m_animator.Update(0);
        }

        /// <summary>
        /// Sets the animation clips of the manager's animator override controller.
        /// </summary>
        /// <param name="overrides">The list of animation clips overrides.</param>
        public virtual void SetAnimationOverride(
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides
        )
        {
            m_stanceOverrides.ApplyOverrides(overrides);
            m_overrides.ApplyOverrides(overrides);
        }

        /// <summary>
        /// Returns the current attack animation speed multiplier.
        /// </summary>
        protected virtual float GetAttackSpeed() =>
            baseAttackSpeed + m_entity.stats.GetAnimationAttackSpeed();

        /// <summary>
        /// Returns the current block animation speed multiplier.
        /// </summary>
        protected virtual float GetBlockSpeed() =>
            baseBlockSpeed + m_entity.stats.GetAnimationBlockSpeed();

        /// <summary>
        /// Returns the current stun animation speed multiplier.
        /// </summary>
        protected virtual float GetStunSpeed() =>
            baseStunSpeed + m_entity.stats.GetStunAnimationSpeed();

        /// <summary>
        /// Returns the length of the current attack animation clip.
        /// </summary>
        public virtual float GetAttackAnimationLength()
        {
            var comboId = m_entity.comboIndex + 1;
            return m_overrides[comboBaseClipName + comboId].length / GetAttackSpeed();
        }

        /// <summary>
        /// Returns the length of the current block animation clip.
        /// </summary>
        public virtual float GetBlockAnimationLength() =>
            m_overrides[blockClipName].length / GetBlockSpeed();

        /// <summary>
        /// Returns the length of the current skill animation clip.
        /// </summary>
        public virtual float GetSkillAnimationLength() =>
            m_overrides[skillClipName].length / GetAttackSpeed();

        /// <summary>
        /// Returns the length of the current stun animation.
        /// </summary>
        public virtual float GetStunAnimationLength() =>
            m_overrides[stunClipName].length / GetStunSpeed();

        /// <summary>
        /// Updates the parameters from the animator controller.
        /// </summary>
        protected virtual void HandleParameters()
        {
            m_animator.SetFloat(m_attackSpeedHash, GetAttackSpeed());
            m_animator.SetFloat(m_blockSpeedHash, GetBlockSpeed());
            m_animator.SetFloat(m_stunSpeedHash, GetStunSpeed());
            m_animator.SetFloat(m_speedHash, m_entity.lateralVelocity.magnitude);
        }

        /// <summary>
        /// Returns true if the animator override controller contains a clip with the given name.
        /// </summary>
        /// <param name="clipName">The name of the clip to look up.</param>
        protected virtual bool HasClip(string clipName)
        {
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            m_overrides.GetOverrides(overrides);
            return overrides.Exists(pair => pair.Key != null && pair.Key.name == clipName);
        }

        /// <summary>
        /// Logs an error for each expected clip name that is missing from the animator controller.
        /// </summary>
        protected virtual void ValidateClipNames()
        {
            var required = new[]
            {
                attackClipName,
                comboBaseClipName + "1",
                skillClipName,
                blockClipName,
                stunClipName,
            };

            foreach (var name in required)
            {
                if (!HasClip(name))
                    Debug.LogError(
                        $"[EntityAnimator] Clip '{name}' not found in the animator controller.",
                        this
                    );
            }
        }

        /// <summary>
        /// Triggers the attack animation and sets the combo index.
        /// </summary>
        protected virtual void TriggerAttack()
        {
            m_animator.SetTrigger(m_onAttackHash);
            m_animator.SetInteger(m_comboIndexHash, m_entity.comboIndex);
        }

        protected virtual void Start()
        {
            InitializeEntity();
            InitializeAnimator();
            InitializeHashes();
            InitializeTriggers();
            InitializeOverride();
            ValidateClipNames();
            InitializeStance();
            InitializeSkill();
            InitializeCallbacks();
        }

        protected virtual void LateUpdate()
        {
            HandleParameters();
        }
    }
}