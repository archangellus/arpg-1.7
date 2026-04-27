using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Handles all incoming damage processing for an Entity. This component is automatically
    /// added by Entity if not already present, allowing designers to attach a subclass to a
    /// prefab to override specific behavior without subclassing Entity.
    /// </summary>
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Damage Handler")]
    public class EntityDamageHandler : MonoBehaviour
    {
        protected Entity m_entity;
        protected float m_lastHitTime;
        protected List<Entity> m_damageFrom = new();

        protected const float k_maxHitRate = 0.15f;

        protected virtual void Awake()
        {
            m_entity = GetComponent<Entity>();
        }

        /// <summary>
        /// Runs the full damage pipeline. Called by <see cref="Entity.Damage"/>.
        /// </summary>
        public virtual void Process(Entity other, EntityDamageInfo info)
        {
            if (ShouldSkipHit())
                return;

            if (TryMiss(other, info))
                return;

            if (TryBlock(other, info))
                return;

            var total = ResolveAmount(info);

            if (TryImmune(other, info, total))
                return;

            var actualDamageDone = ApplyHit(other, ref info, total);
            ApplyPostHitEffects(other, info, total, actualDamageDone);
            EvaluateOutcome(other, info);
        }

        /// <summary>
        /// Notifies all entities that damaged this one of its defeat. Called by <see cref="Entity.Die"/>.
        /// </summary>
        public virtual void NotifyDefeat()
        {
            foreach (var entity in m_damageFrom)
            {
                if (!entity)
                    continue;

                if (entity.IsSummoned() && entity.TryGetComponent(out EntityAI aI))
                {
                    aI.leader.stats.OnDefeatEntity(m_entity);
                    continue;
                }

                entity.stats.OnDefeatEntity(m_entity);
            }
        }

        /// <summary>
        /// Returns true if the entity is dead or the minimum time between hits has not elapsed.
        /// </summary>
        protected virtual bool ShouldSkipHit() =>
            m_entity.isDead || Time.time <= m_lastHitTime + k_maxHitRate;

        /// <summary>
        /// Performs the accuracy roll for active-mode hits. Invokes <see cref="Entity.onMiss"/>
        /// and returns true if the attack misses.
        /// </summary>
        protected virtual bool TryMiss(Entity other, EntityDamageInfo info)
        {
            if (
                !Game.instance.canMiss
                || info.damageMode != DamageMode.Active
                || other == null
                || other.stats == null
                || m_entity.stats == null
                || other.stats.alwaysHit
            )
                return false;

            var hitChance = other.stats.GetHitChance(m_entity.stats);

            if (!m_entity.stats.alwaysEvade && Random.value <= hitChance)
                return false;

            info.sourcePosition = other.position;
            m_entity.onMiss?.Invoke(info);
            return true;
        }

        /// <summary>
        /// Checks whether the entity blocks the hit. Delegates to <see cref="Entity.Block"/>
        /// and returns true if blocked.
        /// </summary>
        protected virtual bool TryBlock(Entity other, EntityDamageInfo info)
        {
            if (
                !info.canBlock
                || m_entity.stats == null
                || Random.value > m_entity.stats.chanceToBlock
                || m_entity.isAttacking
                || m_entity.isWalking
                || !m_entity.IsSourceInFront(other.position)
            )
                return false;

            info.sourcePosition = other.position;
            m_entity.Block(other, info);
            return true;
        }

        /// <summary>
        /// Returns the final post-mitigation damage total for the given damage info.
        /// </summary>
        protected virtual int ResolveAmount(EntityDamageInfo info) => ResolveDamageAmount(info);

        /// <summary>
        /// If total is zero, invokes <see cref="Entity.onImmune"/> and returns true.
        /// </summary>
        protected virtual bool TryImmune(Entity other, EntityDamageInfo info, int total)
        {
            if (total > 0)
                return false;

            info.sourcePosition = other.position;
            m_entity.onImmune?.Invoke(info);
            return true;
        }

        /// <summary>
        /// Reduces the entity's health, records the hit time and attacker, updates info,
        /// and invokes <see cref="Entity.onDamage"/>. Returns the actual health lost.
        /// </summary>
        protected virtual int ApplyHit(Entity other, ref EntityDamageInfo info, int total)
        {
            var previousHealth = m_entity.stats != null ? m_entity.stats.health : 0;
            m_entity.stats.health -= total;
            var actualDamageDone =
                previousHealth - (m_entity.stats != null ? m_entity.stats.health : 0);
            m_lastHitTime = Time.time;

            if (!m_damageFrom.Contains(other))
                m_damageFrom.Add(other);

            info.amount = total;
            info.sourcePosition = other.position;
            m_entity.onDamage?.Invoke(info);

            return actualDamageDone;
        }

        /// <summary>
        /// Applies all secondary effects after a confirmed hit: on-hit bonuses, damage-to-mana
        /// conversion, leech, reflection, info effect, and attacker status effects.
        /// </summary>
        protected virtual void ApplyPostHitEffects(
            Entity other,
            EntityDamageInfo info,
            int total,
            int actualDamageDone
        )
        {
            ApplyOnHitBonuses(other);
            ApplyDamageToMana(total);
            ApplyLeech(other, actualDamageDone);

            if (!info.isReflected)
                ApplyReflection(other, total);

            if (
                info.effects != null
                && info.effects.Length > 0
                && m_entity.effects != null
                && Random.value <= info.effectChance
            )
                foreach (var effect in info.effects)
                    m_entity.effects.Apply(effect, other);

            if (info.damageMode == DamageMode.Active && other != null && other.stats != null)
                ApplyStatusEffectsFromAttacker(other);
        }

        /// <summary>
        /// Triggers death if health reached zero, or attempts to stun if <c>canStun</c> is set.
        /// </summary>
        protected virtual void EvaluateOutcome(Entity other, EntityDamageInfo info)
        {
            if (m_entity.stats.health == 0)
                m_entity.Die();
            else if (info.canStun && other.stats && Random.value <= other.stats.stunChance)
                m_entity.Stun();
        }

        /// <summary>
        /// Resolves the final damage from a hit by processing each damage layer independently.
        /// For the Normal layer, defense reduction is applied when <c>canDefend</c> is true.
        /// Elemental layers bypass defense but are subject to immunity, resistance, and weakness.
        /// <c>damageTakenMultiplier</c> is applied once to the combined total.
        /// Returns 0 if all layers are negated by immunity.
        /// </summary>
        protected virtual int ResolveDamageAmount(EntityDamageInfo info)
        {
            var defenseConstant = Game.instance.defenseConstant;
            var subtotal = 0;

            foreach (var layer in info.layers)
            {
                if (m_entity.stats != null && m_entity.stats.IsImmuneToElement(layer.type))
                    continue;

                var layerAmount =
                    layer.type == DamageType.Normal && info.canDefend && m_entity.stats != null
                        ? Mathf.Max(
                            1,
                            (int)(
                                layer.amount
                                * defenseConstant
                                / (m_entity.stats.effectiveDefense + defenseConstant)
                            )
                        )
                        : layer.amount;

                var resistMultiplier =
                    m_entity.stats != null
                        ? m_entity.stats.GetElementalResistMultiplier(layer.type)
                        : 1f;
                layerAmount = Mathf.Max(1, (int)(layerAmount * resistMultiplier));

                if (m_entity.stats != null && m_entity.stats.IsResistantToElement(layer.type))
                    layerAmount = Mathf.Max(1, layerAmount / 2);

                if (m_entity.stats != null && m_entity.stats.IsWeakToElement(layer.type))
                    layerAmount *= 2;

                subtotal += layerAmount;
            }

            if (subtotal == 0)
                return 0;

            if (m_entity.stats != null)
                subtotal = Mathf.Max(1, (int)(subtotal * m_entity.stats.damageTakenMultiplier));

            return subtotal;
        }

        /// <summary>
        /// Checks the attacker's status-effect chance attributes and applies matching effects
        /// from <see cref="GameDatabase"/> when the roll succeeds.
        /// </summary>
        protected virtual void ApplyStatusEffectsFromAttacker(Entity attacker)
        {
            TryApplyStatusEffect(
                attacker,
                attacker.stats.chanceToBleedPercent,
                GameDatabase.instance.bleedEffect
            );
            TryApplyStatusEffect(
                attacker,
                attacker.stats.chanceToBurnPercent,
                GameDatabase.instance.burnEffect
            );
            TryApplyStatusEffect(
                attacker,
                attacker.stats.chanceToFreezePercent,
                GameDatabase.instance.freezeEffect
            );
            TryApplyStatusEffect(
                attacker,
                attacker.stats.chanceToPoisonPercent,
                GameDatabase.instance.poisonEffect
            );
        }

        protected virtual void TryApplyStatusEffect(
            Entity attacker,
            int chancePercent,
            EntityEffect effect
        )
        {
            if (effect == null || m_entity.effects == null || chancePercent <= 0)
                return;

            if (Random.value <= chancePercent / 100f)
                m_entity.effects.Apply(effect, attacker);
        }

        /// <summary>
        /// Grants flat health and mana bonuses to the attacker for landing a hit.
        /// </summary>
        protected virtual void ApplyOnHitBonuses(Entity other)
        {
            if (other == null || other.stats == null)
                return;

            other.stats.health += other.stats.healthOnHit;
            other.stats.mana += other.stats.manaOnHit;
        }

        /// <summary>
        /// Converts a percentage of the damage taken into mana for this entity.
        /// </summary>
        protected virtual void ApplyDamageToMana(int damage)
        {
            if (m_entity.stats == null || m_entity.stats.damageToManaPercent <= 0)
                return;

            m_entity.stats.mana += Mathf.Max(
                1,
                Mathf.RoundToInt(damage * m_entity.stats.damageToManaPercent / 100f)
            );
        }

        /// <summary>
        /// Converts a percentage of the actual damage dealt into health and mana for the attacker.
        /// Uses the actual HP lost (clamped to target's remaining health) to prevent overkill leeching.
        /// </summary>
        protected virtual void ApplyLeech(Entity other, int damage)
        {
            if (m_entity.stats != null && m_entity.stats.immuneToLeech)
                return;

            if (other == null || other.stats == null)
                return;

            other.stats.health += Mathf.RoundToInt(damage * other.stats.healthLeechPercent / 100f);
            other.stats.mana += Mathf.RoundToInt(damage * other.stats.manaLeechPercent / 100f);
        }

        /// <summary>
        /// Reflects a percentage of the damage taken back to the attacker as unreducible passive damage.
        /// Guards against reflection loops via <see cref="EntityDamageInfo.isReflected"/>.
        /// </summary>
        protected virtual void ApplyReflection(Entity other, int damage)
        {
            if (other != null && other.stats != null && other.stats.immuneToReflection)
                return;

            if (m_entity.stats == null || m_entity.stats.damageReflectionPercent <= 0)
                return;

            var reflected = Mathf.Max(
                1,
                Mathf.RoundToInt(damage * m_entity.stats.damageReflectionPercent / 100f)
            );
            var reflectedInfo = new EntityDamageInfo(
                reflected,
                false,
                canStun: false,
                canBlock: false,
                canDefend: false,
                damageMode: DamageMode.Passive
            )
            {
                isReflected = true,
            };
            other.Damage(m_entity, reflectedInfo);
        }
    }
}
