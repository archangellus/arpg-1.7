using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Effect Manager")]
    public class EntityEffectManager : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip(
            "The debuff types this entity is immune to. Any debuff whose type overlaps "
                + "this mask will be silently rejected."
        )]
        public EntityDebuffTypeMask immuneTo;

        [Header("Events")]
        public UnityEvent<EntityEffectInstance> onEffectApplied;
        public UnityEvent<EntityEffectInstance> onEffectRemoved;
        public UnityEvent onEffectsChanged;

        protected Entity m_entity;
        protected List<EntityEffectInstance> m_activeEffects = new();
        protected bool m_dirty;

        /// <summary>
        /// The combined physical damage multiplier from all active effects.
        /// Buffs and debuffs use additive delta stacking within their group,
        /// then the two groups are multiplied together.
        /// </summary>
        public float damageMultiplier { get; protected set; } = 1f;

        /// <summary>
        /// The combined magic damage multiplier from all active effects.
        /// Buffs and debuffs use additive delta stacking within their group,
        /// then the two groups are multiplied together.
        /// </summary>
        public float magicDamageMultiplier { get; protected set; } = 1f;

        /// <summary>
        /// The combined attack speed multiplier from all active effects.
        /// </summary>
        public float attackSpeedMultiplier { get; protected set; } = 1f;

        /// <summary>
        /// The combined defense multiplier from active debuff effects.
        /// </summary>
        public float defenseMultiplier { get; protected set; } = 1f;

        /// <summary>
        /// The combined damage-taken multiplier from active buff effects.
        /// Values below 1 indicate a reduction (e.g., 0.7 = 30% less damage taken).
        /// </summary>
        public float damageTakenMultiplier { get; protected set; } = 1f;

        /// <summary>
        /// The combined move speed multiplier from all active effects.
        /// </summary>
        public float moveSpeedMultiplier { get; protected set; } = 1f;

        /// <summary>
        /// The combined accuracy rating multiplier from all active effects.
        /// </summary>
        public float accuracyMultiplier { get; protected set; } = 1f;

        /// <summary>
        /// The combined evasion rating multiplier from all active effects.
        /// </summary>
        public float evasionMultiplier { get; protected set; } = 1f;

        /// <summary>
        /// Read-only view of all currently active effect instances.
        /// Subscribe to <see cref="onEffectsChanged"/> to react to changes.
        /// </summary>
        public IReadOnlyList<EntityEffectInstance> activeEffects => m_activeEffects;

        protected virtual void Awake()
        {
            m_entity = GetComponent<Entity>();
            m_entity.onDie.AddListener(ClearAll);
            damageMultiplier = 1f;
            magicDamageMultiplier = 1f;
            defenseMultiplier = 1f;
            damageTakenMultiplier = 1f;
            attackSpeedMultiplier = 1f;
            moveSpeedMultiplier = 1f;
            accuracyMultiplier = 1f;
            evasionMultiplier = 1f;
        }

        /// <summary>
        /// Applies an effect to this entity.
        /// Buffs with the same asset refresh their duration.
        /// Debuffs are subject to immunity and type-uniqueness rules.
        /// </summary>
        /// <param name="effect">The effect asset to apply.</param>
        /// <param name="source">The entity responsible for applying this effect.</param>
        public virtual void Apply(EntityEffect effect, Entity source)
        {
            if (effect == null || m_entity.isDead)
                return;

            if (effect is EntityDebuff debuff)
                ApplyDebuff(debuff, source);
            else if (effect is EntityBuff buff)
                ApplyBuff(buff, source);
        }

        /// <summary>
        /// Removes the active instance that uses the given effect asset, if any.
        /// </summary>
        public virtual void Remove(EntityEffect effect)
        {
            for (int i = m_activeEffects.Count - 1; i >= 0; i--)
            {
                if (m_activeEffects[i].data != effect)
                    continue;

                var removed = m_activeEffects[i];
                m_activeEffects.RemoveAt(i);
                StopEffectParticle(removed);
                onEffectRemoved?.Invoke(removed);
                m_dirty = true;
                return;
            }
        }

        /// <summary>
        /// Removes all currently active effects.
        /// </summary>
        public virtual void ClearAll()
        {
            foreach (var instance in m_activeEffects)
                StopEffectParticle(instance);

            m_activeEffects.Clear();
            m_dirty = true;
        }

        /// <summary>
        /// Returns true if an effect using the given asset is currently active.
        /// </summary>
        public virtual bool HasEffect(EntityEffect effect)
        {
            foreach (var instance in m_activeEffects)
                if (instance.data == effect)
                    return true;

            return false;
        }

        protected virtual void Update()
        {
            var deltaTime = Time.deltaTime;

            for (int i = m_activeEffects.Count - 1; i >= 0; i--)
            {
                var instance = m_activeEffects[i];

                if (instance.ShouldTickDot(deltaTime))
                {
                    ApplyDotTick(instance);

                    if (m_entity.isDead)
                        break;
                }

                if (instance.Tick(deltaTime))
                {
                    StopEffectParticle(instance);
                    onEffectRemoved?.Invoke(instance);
                    m_activeEffects.RemoveAt(i);
                    m_dirty = true;
                }
            }

            if (m_dirty)
            {
                RecalculateModifiers();
                onEffectsChanged?.Invoke();
                m_dirty = false;
            }
        }

        protected virtual void ApplyDotTick(EntityEffectInstance instance)
        {
            if (m_entity.isDead || instance.data is not EntityDebuff debuff)
                return;

            var amount = (int)(m_entity.stats.maxHealth * debuff.dotDamagePercent / 100f);
            amount = Mathf.Max(amount, 1);

            m_entity.Damage(
                instance.source,
                new EntityDamageInfo(
                    amount,
                    false,
                    canStun: false,
                    canBlock: false,
                    canDefend: false,
                    damageMode: DamageMode.Passive,
                    damageType: GetDamageType(debuff.debuffType)
                )
            );
        }

        protected static DamageType GetDamageType(EntityDebuffType debuffType) =>
            debuffType switch
            {
                EntityDebuffType.Fire => DamageType.Fire,
                EntityDebuffType.Ice => DamageType.Ice,
                EntityDebuffType.Poison => DamageType.Poison,
                _ => DamageType.Normal,
            };

        /// <summary>
        /// Recalculates all combined stat multipliers from active effects.
        /// Buffs and debuffs use additive delta stacking within their group,
        /// then the two groups are multiplied together.
        /// </summary>
        protected virtual void RecalculateModifiers()
        {
            var buffDamageDelta = 0f;
            var buffMagicDamageDelta = 0f;
            var buffDamageTakenDelta = 0f;
            var buffAttackSpeedDelta = 0f;
            var buffMoveSpeedDelta = 0f;
            var buffAccuracyDelta = 0f;
            var buffEvasionDelta = 0f;

            var debuffDamageDelta = 0f;
            var debuffMagicDamageDelta = 0f;
            var debuffDefenseDelta = 0f;
            var debuffAttackSpeedDelta = 0f;
            var debuffMoveSpeedDelta = 0f;
            var debuffAccuracyDelta = 0f;
            var debuffEvasionDelta = 0f;

            foreach (var instance in m_activeEffects)
            {
                if (instance.data is EntityBuff buff)
                {
                    buffDamageDelta += buff.DamageMultiplier - 1f;
                    buffMagicDamageDelta += buff.MagicDamageMultiplier - 1f;
                    buffDamageTakenDelta += buff.DamageTakenMultiplier - 1f;
                    buffAttackSpeedDelta += buff.AttackSpeedMultiplier - 1f;
                    buffMoveSpeedDelta += buff.MoveSpeedMultiplier - 1f;
                    buffAccuracyDelta += buff.AccuracyMultiplier - 1f;
                    buffEvasionDelta += buff.EvasionMultiplier - 1f;
                }
                else if (instance.data is EntityDebuff debuff)
                {
                    debuffDamageDelta += debuff.DamageMultiplier - 1f;
                    debuffMagicDamageDelta += debuff.MagicDamageMultiplier - 1f;
                    debuffDefenseDelta += debuff.DefenseMultiplier - 1f;
                    debuffAttackSpeedDelta += debuff.AttackSpeedMultiplier - 1f;
                    debuffMoveSpeedDelta += debuff.MoveSpeedMultiplier - 1f;
                    debuffAccuracyDelta += debuff.AccuracyMultiplier - 1f;
                    debuffEvasionDelta += debuff.EvasionMultiplier - 1f;
                }
            }

            damageMultiplier = (1f + buffDamageDelta) * (1f + debuffDamageDelta);
            magicDamageMultiplier = (1f + buffMagicDamageDelta) * (1f + debuffMagicDamageDelta);
            defenseMultiplier = 1f + debuffDefenseDelta;
            damageTakenMultiplier = 1f + buffDamageTakenDelta;
            attackSpeedMultiplier = (1f + buffAttackSpeedDelta) * (1f + debuffAttackSpeedDelta);
            moveSpeedMultiplier = (1f + buffMoveSpeedDelta) * (1f + debuffMoveSpeedDelta);
            accuracyMultiplier = (1f + buffAccuracyDelta) * (1f + debuffAccuracyDelta);
            evasionMultiplier = (1f + buffEvasionDelta) * (1f + debuffEvasionDelta);
        }

        protected virtual void ApplyDebuff(EntityDebuff debuff, Entity source)
        {
            if (IsImmuneToDebuffType(debuff.debuffType))
                return;

            // Non-Normal types are unique per entity; first application wins.
            if (
                debuff.debuffType != EntityDebuffType.Normal
                && HasActiveDebuffType(debuff.debuffType)
            )
                return;

            // Same asset on a Normal debuff (or re-application after removal): refresh duration.
            RemoveDuplicate(debuff);
            AddInstance(debuff, source);
        }

        protected virtual void ApplyBuff(EntityBuff buff, Entity source)
        {
            RemoveDuplicate(buff);
            AddInstance(buff, source);
        }

        protected virtual void AddInstance(EntityEffect effect, Entity source)
        {
            var instance = new EntityEffectInstance(effect, source);
            m_activeEffects.Add(instance);
            SpawnEffectParticle(instance);
            RecalculateModifiers();
            onEffectApplied?.Invoke(instance);
            onEffectsChanged?.Invoke();
        }

        protected virtual void RemoveDuplicate(EntityEffect effect)
        {
            for (int i = m_activeEffects.Count - 1; i >= 0; i--)
            {
                if (m_activeEffects[i].data != effect)
                    continue;

                StopEffectParticle(m_activeEffects[i]);
                onEffectRemoved?.Invoke(m_activeEffects[i]);
                m_activeEffects.RemoveAt(i);
                break;
            }
        }

        protected virtual void RemoveByDebuffType(EntityDebuffType debuffType)
        {
            for (int i = m_activeEffects.Count - 1; i >= 0; i--)
            {
                if (m_activeEffects[i].data is not EntityDebuff d || d.debuffType != debuffType)
                    continue;

                StopEffectParticle(m_activeEffects[i]);
                onEffectRemoved?.Invoke(m_activeEffects[i]);
                m_activeEffects.RemoveAt(i);
                m_dirty = true;
            }
        }

        protected virtual void SpawnEffectParticle(EntityEffectInstance instance)
        {
            if (instance.data.particlePrefab == null)
                return;

            instance.particleInstance = Instantiate(
                instance.data.particlePrefab,
                m_entity.transform
            );

            instance.particleInstance.transform.SetLocalPositionAndRotation(
                instance.data.particlePositionOffset,
                Quaternion.Euler(instance.data.particleRotationOffset)
            );
        }

        protected virtual void StopEffectParticle(EntityEffectInstance instance)
        {
            if (instance.particleInstance == null)
                return;

            var go = instance.particleInstance;
            instance.particleInstance = null;

            go.transform.SetParent(null);

            if (go.TryGetComponent(out ParticleSystem ps))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(go, ps.main.startLifetime.constantMax);
            }
            else
            {
                Destroy(go);
            }
        }

        /// <summary>
        /// Returns true if any active debuff has the given type.
        /// </summary>
        protected virtual bool HasActiveDebuffType(EntityDebuffType debuffType)
        {
            foreach (var instance in m_activeEffects)
                if (instance.data is EntityDebuff d && d.debuffType == debuffType)
                    return true;

            return false;
        }

        /// <summary>
        /// Returns true if the given type is covered by the immunity mask.
        /// Normal type is never blocked by immunity.
        /// </summary>
        protected virtual bool IsImmuneToDebuffType(EntityDebuffType debuffType)
        {
            if (debuffType == EntityDebuffType.Normal)
                return false;

            var mask = (EntityDebuffTypeMask)(1 << (int)debuffType);
            return (mask & immuneTo) != EntityDebuffTypeMask.None;
        }
    }
}
