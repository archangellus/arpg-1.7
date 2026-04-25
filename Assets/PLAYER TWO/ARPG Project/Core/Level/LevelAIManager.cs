using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Level/Level AI Manager")]
    public class LevelAIManager : Singleton<LevelAIManager>
    {
        [Header("Frame Rate Settings")]
        [Tooltip("The frame rate of the AI culling routine.")]
        public int cullingFrameRate = 30;

        [Tooltip("The frame rate of the AI behavior routine.")]
        public int behaviorFrameRate = 15;

        [Header("Culling Settings")]
        [Tooltip("If true, the AIs will not become visible again after death.")]
        public bool cullCompletelyAfterDeath = true;

        [Tooltip("The size of the bounding box that represents " +
            "the visibility volume around the Player.")]
        public Vector3 boundsSize = new(50, 10, 50);

        protected Camera m_camera;
        protected Bounds m_cullingBounds;

        protected List<EntityAI> m_AIs = new();

        protected WaitForSeconds m_cullingWait;
        protected WaitForSeconds m_behaviorWait;

        /// <summary>
        /// An array of colliders used to store the results of the Physics methods.
        /// </summary>
        public static Collider[] SightBuffer = new Collider[128];

        protected virtual void InitializeCamera() => m_camera = Camera.main;

        protected virtual void InitializeCullingBounds() =>
            m_cullingBounds = new Bounds(Vector3.zero, boundsSize);

        protected virtual void InitializeWaits()
        {
            m_cullingWait = new WaitForSeconds(1f / cullingFrameRate);
            m_behaviorWait = new WaitForSeconds(1f / behaviorFrameRate);
        }

        protected virtual void InitializeRoutines()
        {
            StartCoroutine(CullingRoutine());
            StartCoroutine(BehaviorRoutine());
        }

        /// <summary>
        /// Registers an AI to the manager. The manager controls the
        /// AI's visibility (culling) and behavior update.
        /// </summary>
        /// <param name="ai">The AI you want to register.</param>
        public virtual void RegisterAI(EntityAI ai)
        {
            if (ai && !m_AIs.Contains(ai))
                m_AIs.Add(ai);
        }

        protected virtual void HandleCulling(EntityAI ai)
        {
            if (!ai || ai.ignoreCulling)
                return;

            m_cullingBounds.center = Level.instance.player.transform.position;
            var visible = m_cullingBounds.Intersects(ai.bounds);

            if (ai.gameObject.activeSelf != visible)
            {
                if (visible && ai.isDead && cullCompletelyAfterDeath)
                    return;

                ai.gameObject.SetActive(visible);
            }
        }

        protected virtual IEnumerator CullingRoutine()
        {
            while (true)
            {
                for (int i = 0; i < m_AIs.Count; i++)
                    HandleCulling(m_AIs[i]);

                yield return m_cullingWait;
            }
        }

        protected virtual IEnumerator BehaviorRoutine()
        {
            while (true)
            {
                for (int i = 0; i < m_AIs.Count; i++)
                    m_AIs[i].AIUpdate();

                yield return m_behaviorWait;
            }
        }

        protected virtual void Start()
        {
            InitializeCamera();
            InitializeCullingBounds();
            InitializeWaits();
            InitializeRoutines();
        }
    }
}
