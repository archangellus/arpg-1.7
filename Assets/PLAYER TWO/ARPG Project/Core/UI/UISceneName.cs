using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/UI/UI Scene Name")]
    public class UISceneName : MonoBehaviour
    {
        [Header("Scene Name UI Elements")]
        [Tooltip("Reference to the Text component displaying the scene name.")]
        public Text sceneNameText;

        protected virtual void Start()
        {
            if (!sceneNameText)
                sceneNameText = GetComponent<Text>();

            sceneNameText.text = Level.instance.currentScene.name;
        }
    }
}
