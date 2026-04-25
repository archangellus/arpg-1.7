using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/UI/UI Tab")]
    public class UITab : MonoBehaviour
    {
        [Tooltip("A reference to the Text component that represents the tab text.")]
        public Text text;

        [Tooltip("A reference to the tab's Toggle component.")]
        public Toggle toggle;
    }
}
