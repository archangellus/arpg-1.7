using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Entity))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Skill Manager")]
    public class EntitySkillManager : MonoBehaviour
    {
        public UnityEvent<Skill[]> onUpdatedSkills;
        public UnityEvent<Skill[]> onUpdatedEquippedSkills;
        public UnityEvent<Skill> onChanged;
        public UnityEvent<SkillInstance> onPerform;

        [Tooltip("List of available Skills.")]
        public List<Skill> skills;

        [Tooltip("If true, the first Skill from the list will be automatically equipped.")]
        public bool autoEquipFirstSkill = true;

        [Tooltip(
            "If true, the current selected Skill cool down will not be considered to perform it."
        )]
        public bool ignoreCoolDown;

        [Header("Casting Origins")]
        [Tooltip("A transform that will be used as reference to the hands when casting objects.")]
        public Transform hands;

        [Tooltip("The layer mask to be considered as ground for Skills that cast from the ground.")]
        public LayerMask skillGroundLayer;

        protected const float k_maxFloorDistance = 10f;
        protected const float k_floorOffset = 0.05f;
        protected const float k_mouseCenterOffset = 1f;

        protected Entity m_entity;

        /// <summary>
        /// Returns a Skill from the available Skills list by its index.
        /// </summary>
        public Skill this[int index] => skills[index];

        /// <summary>
        /// Returns the amount of available Skills.
        /// </summary>
        public int Count => skills.Count;

        /// <summary>
        /// The current selected Skill.
        /// </summary>
        public Skill current => currentInstance?.data;

        /// <summary>
        /// The index of the current selected Skill.
        /// </summary>
        public int index { get; protected set; }

        /// <summary>
        /// The Skill instance of the current selected Skill.
        /// </summary>
        public SkillInstance currentInstance { get; protected set; }

        /// <summary>
        /// The list of equipped Skills.
        /// </summary>
        public List<SkillInstance> equipped { get; set; }

        protected Dictionary<Skill, SkillInstance> m_instancesCache = new();

        protected virtual void InitializeEntity() => m_entity = GetComponent<Entity>();

        protected virtual void InitializeSkills()
        {
            if (autoEquipFirstSkill)
            {
                EquipSkill(0);
            }
        }

        /// <summary>
        /// Get the skill list as an array of skills.
        /// </summary>
        public virtual Skill[] ToArray() => skills.ToArray();

        /// <summary>
        /// Returns true if the current selected skill can be used to attack.
        /// </summary>
        public virtual bool IsAttack() => current is SkillAttack;

        /// <summary>
        /// Returns true if the Entity can use the current selected skill.
        /// </summary>
        public virtual bool CanUseSkill() =>
            current
            && (ignoreCoolDown || currentInstance.CanPerform())
            && (!current.useMana || m_entity.stats.mana >= current.manaCost)
            && (!current.useBlood || m_entity.stats.health >= current.bloodCost)
            && ValidRequiredWeapon();

        /// <summary>
        /// Returns true if the current selected skill requires an assigned target to work.
        /// </summary>
        public virtual bool RequireTarget() => current && current.requireTarget;

        /// <summary>
        /// Returns true if the current skill requires the Entity to face an interesting direction.
        /// </summary>
        public virtual bool CanFaceDirection() => current && current.faceInterestDirection;

        /// <summary>
        /// Returns the current skill as a Skill Attack.
        /// </summary>
        public virtual SkillAttack GetSkillAttack() => (SkillAttack)current;

        /// <summary>
        /// Performs the current selected skill, consuming the mana, and applying its effects.
        /// It casts the Skill's casting object, if any, or activates the Entity's Hitbox, if
        /// the Skill is configured to to use it.
        /// </summary>
        public virtual void PerformSkill()
        {
            if (!CanUseSkill())
                return;

            ConsumeMana();
            ConsumeBlood();
            HandleHealing();
            Cast();

            if (IsAttack() && current.AsAttack().useMeleeHitbox)
            {
                var skillDamage = m_entity.stats.GetSkillDamage(
                    m_entity.skills.current,
                    out var skillCritical
                );
                m_entity.hitbox.SetDamage(skillDamage, skillCritical);
                m_entity.hitbox.Toggle();
            }

            currentInstance.Perform();
            onPerform.Invoke(currentInstance);
        }

        /// <summary>
        /// Consumes the Stats's current mana points based on the current skill settings.
        /// </summary>
        protected virtual void ConsumeMana()
        {
            if (current.useMana)
            {
                m_entity.stats.mana = Mathf.Max(0, m_entity.stats.mana - current.manaCost);
            }
        }

        /// <summary>
        /// Consumes the health points from the Stats based on the current skill settings.
        /// </summary>
        protected virtual void ConsumeBlood()
        {
            if (current.useBlood)
            {
                m_entity.stats.health = Mathf.Max(1, m_entity.stats.health - current.bloodCost);
            }
        }

        /// <summary>
        /// Increases the Stats's current health points based on the current skill settings.
        /// </summary>
        protected virtual void HandleHealing()
        {
            if (current.useHealing)
            {
                m_entity.stats.health += current.healingAmount;
            }
        }

        /// <summary>
        /// Casts the current skill's casting object, calculating its damage points if it's a attacking object.
        /// </summary>
        protected virtual GameObject Cast()
        {
            if (!current)
                return null;

            var spacePoint = GetCastOrigin();
            return current.Cast(m_entity, spacePoint.position, spacePoint.rotation);
        }

        /// <summary>
        /// Tries adding a new Skill to the available Skills List.
        /// </summary>
        /// <param name="itemSkill">The Skill you want to add.</param>
        /// <returns>Returns true if the Entity was able to learn the Skill.</returns>
        public virtual bool TryLearnSkill(Skill skill)
        {
            if (skills.Contains(skill))
                return false;

            skills.Add(skill);
            onUpdatedSkills.Invoke(skills.ToArray());

            return true;
        }

        /// <summary>
        /// Tries adding a new Skill, from the Item Skill, to the available Skills List.
        /// It takes in consideration the Entity's current stats point.
        /// </summary>
        /// <param name="itemSkill">The Skill you want to add.</param>
        /// <returns>Returns true if the Entity was able to learn the Skill.</returns>
        public virtual bool TryLearnSkill(ItemSkill itemSkill)
        {
            if (
                !itemSkill.SafeGet(i => i.skill)
                || m_entity.stats.level < itemSkill.requiredLevel
                || m_entity.stats.strength < itemSkill.requiredStrength
                || m_entity.stats.energy < itemSkill.requiredEnergy
            )
                return false;

            return TryLearnSkill(itemSkill.skill);
        }

        /// <summary>
        /// Equips a Skill from the available Skills List based on its index.
        /// </summary>
        /// <param name="index">The index of the Skill you want to equip.</param>
        public virtual void EquipSkill(int index)
        {
            if (skills.IsInvalidOrNullAt(index))
                return;

            if (equipped == null)
                equipped = new List<SkillInstance>();

            if (!equipped.Any((e) => e != null && e.data == skills[index]))
                equipped.Add(GetSkillInstance(skills[index]));

            ChangeTo(equipped.FindIndex(e => e != null && e.data == skills[index]));
        }

        /// <summary>
        /// Sets the list of equipped skills.
        /// </summary>
        /// <param name="skills">The array of Skills you want to equip.</param>
        public virtual void SetEquippedSkills(Skill[] skills)
        {
            var list = new SkillInstance[skills.Length];

            for (int i = 0; i < skills.Length; i++)
                list[i] = skills[i] ? GetSkillInstance(skills[i]) : null;

            equipped = new List<SkillInstance>(list);
            index = equipped.FindIndex(e => e != null && e.data == current);

            if (index >= 0)
                ChangeTo(index);
            else
            {
                //current = null;
                currentInstance = null;
            }

            onUpdatedEquippedSkills?.Invoke(skills);
        }

        /// <summary>
        /// Changes the current select Skill to another based on the Skill data.
        /// </summary>
        /// <param name="skill">The skill data you want to select.</param>
        public virtual void ChangeTo(Skill skill)
        {
            var index = equipped.FindIndex(e => e != null && e.data == skill);
            ChangeTo(index);
        }

        /// <summary>
        /// Changes the current selected Skill based on the equipped skills list index.
        /// </summary>
        /// <param name="index">The index of the Skill you want to select.</param>
        public virtual void ChangeTo(int index)
        {
            if (equipped.IsInvalidOrNullAt(index))
                return;

            this.index = index;
            currentInstance = equipped[index];
            //current = equipped[index].data;
            onChanged?.Invoke(current);
        }

        /// <summary>
        /// Returns an array of the equipped Skills.
        /// </summary>
        public virtual Skill[] GetEquippedSkills() => equipped.Select(e => e?.data).ToArray();

        /// <summary>
        /// Returns the Skill Instance of a given Skill.
        /// If the Skill Instance doesn't exist, it creates a new one.
        /// </summary>
        /// <param name="skill">The Skill you want to get the instance from.</param>
        /// <returns>The Skill Instance of the given Skill.</returns>
        public virtual SkillInstance GetSkillInstance(Skill skill)
        {
            if (!skill)
                return null;

            if (m_instancesCache.TryGetValue(skill, out var instance))
                return instance;

            instance = new SkillInstance(skill);
            m_instancesCache[skill] = instance;
            return instance;
        }

        /// <summary>
        /// Returns the casting position and rotation in world space of the current Skill.
        /// </summary>
        protected virtual SpacePoint GetCastOrigin()
        {
            var position = transform.position;
            var rotation = transform.rotation;

            switch (current.castingOrigin)
            {
                default:
                case Skill.CastingOrigin.Center:
                    break;
                case Skill.CastingOrigin.Hands:
                    position = hands.transform.position;
                    rotation = hands.transform.rotation;
                    break;
                case Skill.CastingOrigin.Floor:
                    position = GetFloorPoint(transform.position);
                    break;
                case Skill.CastingOrigin.TargetCenter:
                    if (!CanReadTarget())
                        break;

                    if (m_entity.target)
                    {
                        position = m_entity.target.transform.position;
                        rotation = m_entity.target.transform.rotation;
                    }
                    else
                        position = GetMousePosition();
                    break;
                case Skill.CastingOrigin.TargetFloor:
                    if (!CanReadTarget())
                        break;

                    if (m_entity.target)
                    {
                        position = GetFloorPoint(m_entity.target.position);
                        rotation = m_entity.target.transform.rotation;
                    }
                    else
                        position = GetFloorPoint(GetMousePosition());
                    break;
            }

            return new(position, rotation);
        }

        /// <summary>
        /// Returns the position in world space of the floor from a given point.
        /// </summary>
        /// <param name="from">The point you want to find the floor from.</param>
        protected virtual Vector3 GetFloorPoint(Vector3 from)
        {
            if (Physics.Raycast(from, Vector3.down, out var hit, k_maxFloorDistance))
                return hit.point + Vector3.up * k_floorOffset;

            return from;
        }

        /// <summary>
        /// Return the mouse world space position on the Skill ground layer.
        /// </summary>
        protected virtual Vector3 GetMousePosition()
        {
            if (m_entity.inputs.MouseRaycast(out var hit, skillGroundLayer))
                return hit.point + Vector3.up * k_mouseCenterOffset;

            return m_entity.position;
        }

        /// <summary>
        /// Returns true if the manager can read from the current assigned target from the Entity component.
        /// </summary>
        protected virtual bool CanReadTarget() => m_entity.target || CompareTag(GameTags.Player);

        /// <summary>
        /// Returns true if the Entity is equipping the required weapon to perform the current selected Skill.
        /// </summary>
        protected virtual bool ValidRequiredWeapon()
        {
            if (!IsAttack())
                return true;

            switch (current.AsAttack().requiredWeapon)
            {
                default:
                case SkillAttack.RequiredWeapon.None:
                    return true;
                case SkillAttack.RequiredWeapon.Blade:
                    return m_entity.items.IsUsingBlade();
                case SkillAttack.RequiredWeapon.Bow:
                    return m_entity.items.IsUsingBow();
            }
        }

        protected virtual void Awake()
        {
            InitializeEntity();
            InitializeSkills();
        }
    }
}
