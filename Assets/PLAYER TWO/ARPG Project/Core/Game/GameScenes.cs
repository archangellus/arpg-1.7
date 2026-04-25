using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Game/Game Scenes")]
    public class GameScenes : Singleton<GameScenes>
    {
        [Header("Loading Settings")]
        [Tooltip("The Game Object that represents the loading screen.")]
        public GameObject loadingScreen;

        [Tooltip("The Slider component that represents the loading progress.")]
        public Slider loadingSlider;

        [Tooltip("The Image component that represents the loading image.")]
        public Image loadingImage;

        [Header("Loading Settings")]
        [Tooltip("An Audio Clip that plays when the loading starts.")]
        public AudioClip loadStartClip;

        [Tooltip("An Audio Cip that plays when the loading finishes.")]
        public AudioClip loadFinishClip;

        [Space(10)]
        public UnityEvent onSceneLoadTriggered;

        protected (Vector3 position, Quaternion rotation)? m_nextSceneCoordinates;

        protected Sprite m_defaultLoadingSprite;

        protected virtual void Start()
        {
            m_defaultLoadingSprite = loadingImage.sprite;
        }

        /// <summary>
        /// Sets the Player's position and rotation for the next scene.
        /// </summary>
        /// <param name="position">The position in world space.</param>
        /// <param name="rotation">The rotation in world space.</param>
        public virtual void SetNextSceneCoordinates(Vector3 position, Vector3 rotation)
        {
            m_nextSceneCoordinates = new()
            {
                position = position,
                rotation = Quaternion.Euler(rotation),
            };
        }

        /// <summary>
        /// Clears the data of the next scene coordinates.
        /// </summary>
        public virtual void ClearNextSceneCoordinates() => m_nextSceneCoordinates = null;

        /// <summary>
        /// Teleports the Player to the next scene coordinates.
        /// </summary>
        protected virtual void TeleportPlayer()
        {
            if (m_nextSceneCoordinates == null)
                return;

            var position = (Vector3)m_nextSceneCoordinates?.position;
            var rotation = (Quaternion)m_nextSceneCoordinates?.rotation;

            if (Physics.Raycast(position, Vector3.down, out var hit))
                Level.instance.player.Teleport(hit.point + Vector3.up, rotation);
        }

        /// <summary>
        /// Sets the loading screen sprite.
        /// </summary>
        /// <param name="sprite">The sprite to set.</param>
        public virtual void SetLoadingScreen(Sprite sprite)
        {
            if (sprite == null)
            {
                loadingImage.sprite = m_defaultLoadingSprite;
                return;
            }

            loadingImage.sprite = sprite;
        }

        /// <summary>
        /// Checks if a scene exists in the build settings.
        /// </summary>
        /// <param name="sceneName">The name of the scene to check.</param>
        /// <returns>True if the scene exists, false otherwise.</returns>
        public static bool IsSceneValid(string sceneName) =>
            SceneUtility.GetBuildIndexByScenePath(sceneName) >= 0;

        /// <summary>
        /// Loads a given scene by its name from the build settings.
        /// This method will set the character's current scene to the loaded scene.
        /// </summary>
        /// <param name="scene">The name of the scene you want to load.</param>
        /// <param name="loadingSprite">The sprite to display on the loading screen.</param>
        public virtual void LoadScene(string scene, Sprite loadingSprite) =>
            LoadScene(scene, true, loadingSprite);

        /// <summary>
        /// Loads a given scene by its name from the build settings.
        /// </summary>
        /// <param name="scene">The name of the scene you want to load.</param>
        /// <param name="setAsCharacterScene">If true, the scene will be set as the character's current scene.</param>
        /// <param name="loadingSprite">The sprite to display on the loading screen.</param>
        public virtual void LoadScene(
            string scene,
            bool setAsCharacterScene = false,
            Sprite loadingSprite = null
        )
        {
            if (!IsSceneValid(scene))
            {
                Debug.LogError($"Scene '{scene}' not found in build settings.");
                return;
            }

            onSceneLoadTriggered.Invoke();
            GameSave.instance.Save();
            Game.instance.ReloadGameData();
            GameAudio.instance.PlayEffect(loadStartClip);
            Fader.instance.FadeOut(
                () => StartCoroutine(LoadSceneRoutine(scene, setAsCharacterScene))
            );
            SetLoadingScreen(loadingSprite);
        }

        protected virtual IEnumerator LoadSceneRoutine(string scene, bool setAsCharacterScene)
        {
            var operation = SceneManager.LoadSceneAsync(scene);

            loadingScreen.SetActive(true);

            while (!operation.isDone)
            {
                loadingSlider.value = operation.progress;
                yield return null;
            }

            if (setAsCharacterScene)
                Game.instance.currentCharacter.currentScene = scene;

            TeleportPlayer();
            ClearNextSceneCoordinates();
            loadingScreen.SetActive(false);
            Fader.instance.FadeIn();
            GameAudio.instance.PlayEffect(loadFinishClip);
        }
    }
}
