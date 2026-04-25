using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Waypoints Window")]
    public class GUIWaypointsWindow : MonoBehaviour
    {
        [Header("GUI References")]
        [Tooltip("A reference to the transform used as the waypoints container.")]
        public RectTransform waypointsContainer;

        [Tooltip("The prefab you want to use as Waypoints.")]
        public GUIWaypoint waypointPrefab;

        [Header("Scene Navigation")]
        [Tooltip("References the text component used to display the scene name.")]
        public Text sceneNameText;

        [Tooltip("References the button used to select the next scene.")]
        public Button nextSceneButton;

        [Tooltip("References the button used to select the previous scene.")]
        public Button previousSceneButton;

        protected List<CharacterScenes.Scene> m_characterScenes = new();
        protected List<GUIWaypoint> m_buttons = new();

        protected int m_currentSceneIndex = 0;

        protected LevelWaypoints m_levelWaypoints => LevelWaypoints.instance;
        protected string m_currentSceneName => Level.instance.currentScene.name;

        protected CharacterInstance m_character;

        protected virtual void Awake()
        {
            InitializeCharacter();
            InitializeSceneNavigation();
            RefreshNavigation();
        }

        protected virtual void OnEnable()
        {
            RefreshWaypointButtons();
        }

        protected virtual void InitializeCharacter()
        {
            m_character = Game.instance.currentCharacter;
            m_character.scenes.UpdateScene(Level.instance);
        }

        protected virtual void InitializeSceneNavigation()
        {
            m_characterScenes.Clear();
            m_characterScenes = m_character.scenes.scenes.ToList();
            m_currentSceneIndex = m_characterScenes.FindIndex(s =>
                s.name.Equals(Level.instance.currentScene.name)
            );

            nextSceneButton.onClick.AddListener(() =>
            {
                if (m_currentSceneIndex >= m_characterScenes.Count - 1)
                    return;

                m_currentSceneIndex++;
                RefreshNavigation();
                RefreshWaypointButtons();
            });

            previousSceneButton.onClick.AddListener(() =>
            {
                if (m_currentSceneIndex <= 0)
                    return;

                m_currentSceneIndex--;
                RefreshNavigation();
                RefreshWaypointButtons();
            });
        }

        protected virtual void RefreshNavigation()
        {
            if (m_characterScenes.Count == 0)
                return;

            nextSceneButton.interactable = m_currentSceneIndex < m_characterScenes.Count - 1;
            previousSceneButton.interactable = m_currentSceneIndex > 0;

            var currentScene = m_characterScenes[m_currentSceneIndex];
            sceneNameText.text = currentScene.name;
        }

        protected virtual void RefreshWaypointButtons()
        {
            DestroyAllButtons();

            if (m_characterScenes.Count == 0)
                return;

            var currentScene = m_characterScenes[m_currentSceneIndex];

            foreach (var waypoint in currentScene.waypoints)
            {
                if (!waypoint.active)
                    continue;

                var button = Instantiate(waypointPrefab, waypointsContainer);
                button.title.text = waypoint.title;
                button.sceneName = currentScene.name;
                button.button.interactable = !IsCurrentWaypoint(button);
                m_buttons.Add(button);
            }
        }

        protected virtual void DestroyAllButtons()
        {
            if (m_buttons.Count > 0)
            {
                for (int i = m_buttons.Count - 1; i >= 0; i--)
                {
                    Destroy(m_buttons[i].gameObject);
                    m_buttons.RemoveAt(i);
                }
            }
        }

        protected bool IsCurrentWaypoint(GUIWaypoint button) =>
            button
            && button.sceneName == m_currentSceneName
            && button.title.text == m_levelWaypoints.currentWaypoint.title;
    }
}
