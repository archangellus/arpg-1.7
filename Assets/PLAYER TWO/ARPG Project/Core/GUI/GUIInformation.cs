using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(GUIWindow))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Information")]
    public class GUIInformation : MonoBehaviour
    {
        [Tooltip("A reference to the Text component that represents the Information title.")]
        public Text title;

        [Tooltip("A reference to the Text component that represents the Information text.")]
        public Text text;

        /// <summary>
        /// Sets the information data.
        /// </summary>
        /// <param name="title">The title of the information.</param>
        /// <param name="text">The text of the information.</param>
        public virtual void SetInformation(string title, string text)
        {
            this.title.text = title.ToUpper();
            this.text.text = text;
        }
    }
}
