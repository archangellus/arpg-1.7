using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Waypoint")]
    public class GUIWaypoint : MonoBehaviour
    {
        [Tooltip("A reference to the Text component used as the Waypoint title.")]
        public Text title;

        /// <summary>
        /// References the Waypoint target scene name.
        /// </summary>
        public string sceneName { get; set; }

        /// <summary>
        /// The Button component of this GUI Waypoint.
        /// </summary>
        public Button button { get; protected set; }

        protected virtual void InitializeButton()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClick);
        }

        protected virtual void OnClick()
        {
            LevelWaypoints.instance.TravelTo(sceneName, title.text);
        }

        protected virtual void Awake() => InitializeButton();
    }
}
