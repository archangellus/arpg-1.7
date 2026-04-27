using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Skill", menuName = "PLAYER TWO/ARPG Project/Skills/Skill")]
    public class Skill : ScriptableObject
    {
        public enum CastingOrigin
        {
            Center,
            Hands,
            Floor,
            TargetCenter,
            TargetFloor,
        }

        [Header("General Settings")]
        [Tooltip("A small image to visually represent the Skill on the interface.")]
        public Sprite icon;

        [Tooltip("The audio that plays when this SKill is used.")]
        public AudioClip sound;

        [Tooltip("The minimum duration in seconds between each usage of this Skill.")]
        public float coolDown;

        [Header("Casting Settings")]
        [Tooltip("A prefab to automatically instantiate when this Skill is used.")]
        public GameObject castObject;

        [Tooltip("The origin of the cast object when instantiated.")]
        public CastingOrigin castingOrigin;

        [Header("Target Settings")]
        [Tooltip("If true, this Skill can only be used against a target.")]
        public bool requireTarget = true;

        [Tooltip("If true, the Skill caster will look at the casting direction.")]
        public bool faceInterestDirection = true;

        [Header("Animation Setting")]
        [Tooltip(
            "If true, the 'performing skill' animation will use the same Animation Clip of regular attacks."
        )]
        public bool useRegularAttackClip;

        [Tooltip(
            "An Animation Clip to override the 'performing skill' animation of the Skill caster."
        )]
        public AnimationClip overrideClip;

        [Header("Mana Settings")]
        [Tooltip(
            "If true, the usage of this Skill will decrease the amount of available mana points from the Skill caster."
        )]
        public bool useMana = true;

        [Tooltip("The amount of mana points this Skill will consume.")]
        public int manaCost;

        [Header("Blood Settings")]
        [Tooltip(
            "If true, the usage of this Skill will decrease the amount of health points from the Skill caster."
        )]
        public bool useBlood;

        [Tooltip("The amount of health this Skill will consume.")]
        public int bloodCost;

        [Header("Healing Settings")]
        [Tooltip("If true, using this Skill will increase the health points of the SKill caster.")]
        public bool useHealing;

        [Tooltip("The amount of health points to increase.")]
        public int healingAmount;

        [Header("Effect Settings")]
        [Tooltip(
            "Effects applied to the caster when this Skill is used. If the chance roll succeeds, all of them are applied."
        )]
        public EntityEffect[] selfEffects;

        [Tooltip("Chance (0 to 1) of applying all self effects. 0 = never, 1 = always.")]
        [Range(0f, 1f)]
        public float selfEffectChance = 1f;

        [Tooltip(
            "Effects applied to any Entity hit by this Skill. If the chance roll succeeds, all of them are applied."
        )]
        public EntityEffect[] targetEffects;

        [Tooltip("Chance (0 to 1) of applying all target effects on hit. 0 = never, 1 = always.")]
        [Range(0f, 1f)]
        public float targetEffectChance = 1f;

        /// <summary>
        /// Returns true if this Skill is a attacking skill.
        /// </summary>
        public virtual bool IsAttack() => this is SkillAttack;

        /// <summary>
        /// Returns the Skill casted to the Skill Attack type.
        /// </summary>
        public virtual SkillAttack AsAttack() => (SkillAttack)this;

        /// <summary>
        /// Instantiates and returns a reference of the casted object in a given
        /// position and rotation, or null, if it doesn't cast anything.
        /// </summary>
        /// <param name="caster">The Entity casting this Skill.</param>
        /// <param name="position">The position of the casted Game Object.</param>
        /// <param name="rotation">The rotation of the casted Game Object.</param>
        /// <returns>A reference to the casted Game Object or null.</returns>
        public virtual GameObject Cast(Entity caster, Vector3 position, Quaternion rotation)
        {
            if (selfEffects != null && selfEffects.Length > 0 && Random.value <= selfEffectChance)
                foreach (var effect in selfEffects)
                    if (caster.effects != null)
                        caster.effects.Apply(effect, caster);

            if (!castObject)
                return null;

            var instance = Instantiate(castObject, position, rotation);

            var skillDamageType = this is SkillAttack sa ? sa.damageType : DamageType.Normal;

            if (instance.TryGetComponent(out Projectile projectile))
            {
                var skillDamage = caster.stats.GetSkillDamage(
                    caster.skills.current,
                    out var critical
                );
                var skillLayers = new List<DamageLayer>
                {
                    new DamageLayer(skillDamageType, skillDamage),
                };
                projectile.SetDamage(caster, skillLayers, critical, caster.targetTags);
                projectile.SetEffect(targetEffects, targetEffectChance);
            }
            else if (instance.TryGetComponent(out SkillParticle hitbox))
            {
                hitbox.SetSkills(caster, caster.skills.current);
                hitbox.SetTargetEffect(targetEffects, targetEffectChance);
            }

            return instance;
        }
    }
}
