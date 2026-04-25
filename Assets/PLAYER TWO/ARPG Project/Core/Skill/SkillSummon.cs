using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Summon Skill", menuName = "PLAYER TWO/ARPG Project/Skills/Summon Skill")]
    public class SkillSummon : Skill
    {
        [Header("Summoning Settings")]
        [Tooltip("A list of entities' prefabs to be summoned when casting this skill.")]
        public Entity[] entitiesPrefabs;

        [Tooltip("The distance radius from the leader.")]
        public float distanceFromLeader;

        protected EntityAI m_tempAI;

        public override GameObject Cast(Entity caster, Vector3 position, Quaternion rotation)
        {
            if (entitiesPrefabs == null)
                return null;

            ClearSummons(caster);
            SummonEntities(caster, position, rotation);

            return null;
        }

        protected virtual void SummonEntities(Entity caster, Vector3 position, Quaternion rotation)
        {
            for (int i = 0; i < entitiesPrefabs.Length; i++)
            {
                var radians = 2 * Mathf.PI / entitiesPrefabs.Length * i;
                var vertical = Mathf.Sin(radians);
                var horizontal = Mathf.Cos(radians);
                var offset = new Vector3(horizontal, 0, vertical) * distanceFromLeader;

                var instance = Instantiate(entitiesPrefabs[i], position + offset, rotation);

                if (instance.TryGetComponent(out m_tempAI))
                {
                    m_tempAI.leader = caster;
                    m_tempAI.leaderOffset = offset;
                    m_tempAI.transform.position = position + offset;
                }

                caster.summonedEntities.Add(instance);
            }
        }

        protected virtual void ClearSummons(Entity caster)
        {
            if (caster.summonedEntities == null)
                return;

            foreach (var entity in caster.summonedEntities)
            {
                if (!entity)
                    continue;

                Destroy(entity.gameObject);
            }

            caster.summonedEntities.Clear();
        }
    }
}
