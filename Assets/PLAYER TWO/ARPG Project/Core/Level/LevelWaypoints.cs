using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Level/Level Waypoints")]
    public class LevelWaypoints : Singleton<LevelWaypoints>
    {
        [Tooltip("The list of all waypoints the Player can use in this Level.")]
        public List<Waypoint> waypoints;

        [Header("Audio Settings")]
        [Tooltip("An Audio Clip that plays whenever the Player uses a Waypoint.")]
        public AudioClip travelClip;

        protected WaitForSeconds m_waitForFadeInDelay;

        protected const float k_fadeInDelay = 0.2f;

        /// <summary>
        /// Returns true if the Player is traveling between waypoints.
        /// </summary>
        public bool traveling { get; protected set; }

        /// <summary>
        /// The current Waypoint the Player is using.
        /// </summary>
        public Waypoint currentWaypoint { get; set; }

        protected Entity m_player => Level.instance.player;

        protected GUIWindow m_waypointWindow => GUIWindowsManager.instance.waypointsWindow;

        /// <summary>
        /// Returns the index of the current Waypoint in the waypoints list.
        /// </summary>
        public int currentWaypointIndex =>
            currentWaypoint ? waypoints.IndexOf(currentWaypoint) : -1;

        protected virtual void InitializeWaits()
        {
            m_waitForFadeInDelay = new WaitForSeconds(k_fadeInDelay);
        }

        public virtual void SetCurrentWaypoint(Waypoint waypoint)
        {
            if (currentWaypoint == waypoint)
                return;

            currentWaypoint = waypoint;
        }

        /// <summary>
        /// Returns true if the given waypoint title is valid.
        /// </summary>
        /// <param name="waypointTitle">The title of the waypoint to check.</param>
        /// <returns>True if the waypoint title is valid; false otherwise.</returns>
        public virtual bool IsValidWaypoint(string waypointTitle) =>
            waypointTitle != null && waypoints.Exists(w => w.title.Equals(waypointTitle));

        /// <summary>
        /// Teleports the current Player to a given Waypoint title with no transition.
        /// If you want to have a transition effect, use TravelTo instead.
        /// </summary>
        /// <param name="waypointTitle">The title of the Waypoint you want to teleport the Player to.</param>
        public virtual void TeleportTo(string waypointTitle)
        {
            var waypoint = waypoints.Find(w => w.title.Equals(waypointTitle));

            if (waypoint)
            {
                var spacePoint = waypoint.GetSpacePoint();
                m_player.Teleport(spacePoint.position, spacePoint.rotation);
            }
        }

        /// <summary>
        /// Moves the current Player to a given Waypoint title in a given scene.
        /// This method handles fading and scene loading as needed.
        /// </summary>
        /// <param name="sceneName">The name of the scene where the Waypoint is located.</param>
        /// <param name="waypointTitle">The title of the Waypoint you want to move the Player to.</param>
        public virtual void TravelTo(string sceneName, string waypointTitle)
        {
            var sameScene = Level.instance.currentScene.name.Equals(sceneName);

            if (sameScene)
            {
                var waypoint = waypoints.Find(w => w.title.Equals(waypointTitle));

                if (waypoint)
                    TravelTo(waypoint);

                return;
            }

            Level.instance.currentCharacter.currentWaypoint = waypointTitle;
            GameScenes.instance.LoadScene(sceneName, true);
        }

        /// <summary>
        /// Moves the current Player to a given Waypoint.
        /// This method handles fading.
        /// </summary>
        /// <param name="waypoint">The Waypoint you want to move the Player to.</param>
        public virtual void TravelTo(Waypoint waypoint)
        {
            var spacePoint = waypoint.GetSpacePoint();

            traveling = true;
            m_player.controller.enabled = false;
            m_player.inputs.enabled = false;
            Level.instance.currentCharacter.currentWaypoint = waypoint.title;
            currentWaypoint = waypoint;
            m_waypointWindow.gameObject.SetActive(false);
            GameAudio.instance.PlayEffect(travelClip);

            StopAllCoroutines();

            Fader.instance.FadeOut(
                () => StartCoroutine(TravelRoutine(spacePoint.position, spacePoint.rotation))
            );
        }

        protected IEnumerator TravelRoutine(Vector3 position, Quaternion rotation)
        {
            m_player.Teleport(position, rotation);

            yield return m_waitForFadeInDelay;

            m_waypointWindow.gameObject.SetActive(false);

            Fader.instance.FadeIn(() =>
            {
                m_player.controller.enabled = true;
                m_player.inputs.enabled = true;
                traveling = false;
            });
        }

        protected virtual void Start()
        {
            InitializeWaits();
        }
    }
}
