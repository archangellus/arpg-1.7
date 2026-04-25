using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    public abstract class GUISlot : MonoBehaviour
    {
        [Header("Slot Properties")]
        public Text keyText;

        protected virtual void Awake()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (keyText)
                keyText.gameObject.SetActive(false);
#endif
        }
    }
}
