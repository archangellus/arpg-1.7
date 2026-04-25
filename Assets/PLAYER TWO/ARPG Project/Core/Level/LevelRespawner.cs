using UnityEngine;
using System.Collections;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Game/Level Respawner")]
    public class LevelRespawner : Singleton<LevelRespawner>
    {
        [Header("General Settings")]
        public float fadeOutDelay;
        public float fadeInDelay;

        [Header("Respawn Settings")]
        public bool resetExperience;

        protected Coroutine m_deathRoutine;
        protected Coroutine m_respawnRoutine;

        protected WaitForSeconds m_waitForFadeOut;
        protected WaitForSeconds m_waitForFadeIn;

        public bool isRespawning { get; protected set; }

        protected Entity m_player => Level.instance.player;
        protected Transform m_origin => Level.instance.playerOrigin;
        protected LevelWaypoints m_waypoints => LevelWaypoints.instance;

        protected virtual void InitializeWaits()
        {
            m_waitForFadeOut = new WaitForSeconds(fadeOutDelay);
            m_waitForFadeIn = new WaitForSeconds(fadeInDelay);
        }

        protected virtual void InitializeCallbacks()
        {
            m_player.onDie.AddListener(OnPlayerDie);
        }

        protected virtual void OnPlayerDie()
        {
            if (m_deathRoutine != null) StopCoroutine(m_deathRoutine);
            if (m_respawnRoutine != null) StopCoroutine(m_respawnRoutine);

            m_deathRoutine = StartCoroutine(DeathRoutine());
        }

        protected virtual SpacePoint GetRespawnTransform()
        {
            if (m_waypoints.currentWaypoint)
                return m_waypoints.currentWaypoint.GetSpacePoint();

            return new(m_origin.position, m_origin.rotation);
        }

        protected IEnumerator DeathRoutine()
        {
            yield return m_waitForFadeOut;

            Fader.instance.FadeOut(() =>
            {
                m_respawnRoutine = StartCoroutine(RespawnRoutine());
            });
        }

        protected IEnumerator RespawnRoutine()
        {
            var spacePoint = GetRespawnTransform();

            isRespawning = true;
            m_player.Teleport(spacePoint.position, spacePoint.rotation);
            m_player.Revive();

            if (resetExperience)
                m_player.stats.ResetExperience();

            yield return m_waitForFadeIn;

            Fader.instance.FadeIn(() =>
            {
                m_player.inputs.enabled = true;
                isRespawning = false;
            });
        }

        protected virtual void Start()
        {
            InitializeWaits();
            InitializeCallbacks();
        }
    }
}
