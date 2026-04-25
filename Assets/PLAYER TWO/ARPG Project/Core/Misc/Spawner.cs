using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Spawner")]
    public class Spawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("The amount of entities to spawn.")]
        public int count = 5;

        [Tooltip("The minimum distance from the spawn origin to spawn entities.")]
        public float minRadius = 8;

        [Tooltip("The maximum distance from the spawn origin to spawn entities.")]
        public float maxRadius = 10;

        [Tooltip("The duration in seconds between respawn.")]
        public float respawnDelay = 10f;

        [Tooltip("The list of all entities that can be spawned.")]
        public Entity[] entities;

        protected Entity m_tempEntity;
        protected List<Entity> m_entities = new();

        protected WaitForSeconds m_waitForRespawnDelay;

        /// <summary>
        /// Returns a random entity from the entities list.
        /// </summary>
        protected Entity GetRandomEntity() => entities[Random.Range(0, entities.Length)];

        protected virtual void InitializeWaits()
        {
            m_waitForRespawnDelay = new WaitForSeconds(respawnDelay);
        }

        protected virtual void InitializeEntities()
        {
            for (int i = 0; i < count; i++) Spawn();
        }

        protected virtual void Spawn()
        {
            var random = Random.insideUnitSphere;
            var radius = Random.Range(minRadius, maxRadius);
            var direction = new Vector3(random.x, 0, random.y);
            var position = transform.position + direction * radius;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);

            m_tempEntity = GetRandomEntity();
            position += Vector3.up * m_tempEntity.controller.height * 0.5f;

            var entity = Instantiate(m_tempEntity, position, rotation);

            m_entities.Add(entity);
            entity.onDie.AddListener(OnEntityDie);
        }

        protected virtual void OnEntityDie()
        {
            if (!gameObject.activeSelf) return;

            StartCoroutine(RespawnRoutine());
        }

        protected virtual IEnumerator RespawnRoutine()
        {
            yield return m_waitForRespawnDelay;

            Spawn();
        }

        protected virtual void Start()
        {
            InitializeWaits();
            InitializeEntities();
        }
    }
}
