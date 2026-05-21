using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Summon Skill", menuName = "PLAYER TWO/ARPG Project/Skills/Pet Summon Skill")]
    public class SkillSummonPet : Skill
    {
        [Header("Summoning Settings")]
        [Tooltip("A list of GameObject prefabs to be summoned when casting this skill.")]
        public GameObject[] petPrefabs;

        [Tooltip("The distance radius from the leader.")]
        public float distanceFromLeader = 1.5f;

        protected EntityAI m_tempAI;

        // Track non-Entity pets so toggle/clear still works.
        private static readonly Dictionary<int, List<GameObject>> s_activePetsByCaster = new();

        private void OnEnable()
        {
            requireTarget = false;
            faceInterestDirection = false;
        }

        public override GameObject Cast(Entity caster, Vector3 position, Quaternion rotation)
        {
            if (caster == null || petPrefabs == null || petPrefabs.Length == 0)
                return null;

            if (HasActivePets(caster))
            {
                ClearSummons(caster);
                PlaySkillSound();
                return null;
            }

            var firstSpawned = SummonEntities(caster, position, rotation);
            PlaySkillSound();
            return firstSpawned;
        }

        protected virtual GameObject SummonEntities(Entity caster, Vector3 position, Quaternion rotation)
        {
            var spawnedList = GetOrCreatePetList(caster);
            spawnedList.Clear();

            GameObject firstSpawned = null;
            int count = petPrefabs.Length;

            for (int i = 0; i < count; i++)
            {
                var prefab = petPrefabs[i];
                if (!prefab)
                    continue;

                var radians = 2 * Mathf.PI / Mathf.Max(1, count) * i;
                var offset = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * distanceFromLeader;

                var instance = Instantiate(prefab, position + offset, rotation);
                if (!firstSpawned)
                    firstSpawned = instance;

                var ownership = instance.GetComponent<PetSummonOwnership>();
                if (ownership == null)
                    ownership = instance.AddComponent<PetSummonOwnership>();

                ownership.ownerEntityId = caster.GetInstanceID();

                var summonOwnership = instance.GetComponent<SummonSkillOwnership>();
                if (summonOwnership == null)
                    summonOwnership = instance.AddComponent<SummonSkillOwnership>();

                summonOwnership.ownerEntityId = caster.GetInstanceID();
                summonOwnership.skillInstanceId = GetInstanceID();

                spawnedList.Add(instance);

                if (instance.TryGetComponent(out m_tempAI))
                {
                    m_tempAI.leader = caster;
                    m_tempAI.leaderOffset = offset;
                    m_tempAI.transform.position = position + offset;
                }

                if (instance.TryGetComponent<Entity>(out var summonedEntity))
                    caster.summonedEntities.Add(summonedEntity);
            }

            return firstSpawned;
        }

        protected virtual void ClearSummons(Entity caster)
        {
            if (caster == null)
                return;

            if (s_activePetsByCaster.TryGetValue(caster.GetInstanceID(), out var pets))
            {
                foreach (var pet in pets)
                {
                    if (pet)
                        Destroy(pet);
                }

                pets.Clear();
            }

            if (caster.summonedEntities == null)
            {
                ClearOwnedScenePets(caster.GetInstanceID());
                return;
            }

            for (int i = caster.summonedEntities.Count - 1; i >= 0; i--)
            {
                var entity = caster.summonedEntities[i];
                if (!entity)
                {
                    caster.summonedEntities.RemoveAt(i);
                    continue;
                }

                if (!IsSummonedByThisSkill(entity, caster.GetInstanceID()))
                    continue;

                Destroy(entity.gameObject);
                caster.summonedEntities.RemoveAt(i);
            }

            ClearOwnedScenePets(caster.GetInstanceID());
        }

        private bool IsSummonedByThisSkill(Entity entity, int ownerId)
        {
            if (!entity)
                return false;

            if (!entity.TryGetComponent<SummonSkillOwnership>(out var ownership) || ownership == null)
                return false;

            return ownership.ownerEntityId == ownerId && ownership.skillInstanceId == GetInstanceID();
        }

        private bool HasActivePets(Entity caster)
        {
            if (caster == null)
                return false;

            if (!s_activePetsByCaster.TryGetValue(caster.GetInstanceID(), out var pets) || pets == null)
            {
                return HasOwnedScenePets(caster.GetInstanceID());
            }

            for (int i = pets.Count - 1; i >= 0; i--)
            {
                if (!pets[i])
                    pets.RemoveAt(i);
            }

            return pets.Count > 0;
        }

        private bool HasOwnedScenePets(int ownerId)
        {
            var owners = Object.FindObjectsByType<PetSummonOwnership>(FindObjectsSortMode.None);

            foreach (var owner in owners)
            {
                if (owner != null && owner.ownerEntityId == ownerId)
                    return true;
            }

            return false;
        }

        private void ClearOwnedScenePets(int ownerId)
        {
            var owners = Object.FindObjectsByType<PetSummonOwnership>(FindObjectsSortMode.None);

            foreach (var owner in owners)
            {
                if (owner != null && owner.ownerEntityId == ownerId)
                    Destroy(owner.gameObject);
            }
        }

        private List<GameObject> GetOrCreatePetList(Entity caster)
        {
            int id = caster.GetInstanceID();
            if (!s_activePetsByCaster.TryGetValue(id, out var pets) || pets == null)
            {
                pets = new List<GameObject>();
                s_activePetsByCaster[id] = pets;
            }

            return pets;
        }

        private void PlaySkillSound()
        {
            if (sound != null && GameAudio.instance != null)
                GameAudio.instance.PlayEffect(sound);
        }
    }
}
