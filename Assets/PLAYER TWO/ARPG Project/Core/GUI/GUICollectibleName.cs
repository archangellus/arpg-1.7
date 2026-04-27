using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Collectible Name")]
    public class GUICollectibleName : MonoBehaviour
    {
        [Tooltip("A reference to the Text component.")]
        public Text itemName;

        [Tooltip("The offset applied this object is instantiated.")]
        public Vector3 offset = new Vector3(0, 0.5f, 0);

        protected Collectible m_target;
        protected Camera m_camera;

        protected virtual void InitializeCamera() => m_camera = Camera.main;

        protected virtual void InitializeParent()
        {
            if (GUI.instance && GUI.instance.collectiblesContainer)
                transform.SetParent(GUI.instance.collectiblesContainer);
        }

        /// <summary>
        /// Sets the Collectible and its text color.
        /// </summary>
        /// <param name="collectible">The Collectible you want to set.</param>
        /// <param name="color">The color of the text.</param>
        public virtual void SetCollectible(Collectible collectible, Color color)
        {
            if (!collectible) return;

            m_target = collectible;
            itemName.color = color;
            itemName.text = collectible.GetName();
        }

        protected virtual void Start()
        {
            InitializeCamera();
            InitializeParent();
        }

        protected virtual void LateUpdate()
        {
            if (!m_target)
            {
                Destroy(this.gameObject);
                return;
            }

            var position = m_target.transform.position + offset;
            var screenPos = m_camera.WorldToScreenPoint(position);
            transform.position = screenPos;
        }
    }
}
