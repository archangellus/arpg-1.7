using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Health Bar")]
    public class GUIHealthBar : MonoBehaviour
    {
        [Tooltip("Image that represents the health progression bar.")]
        public Image fillImage;

        protected Camera m_camera;

        /// <summary>
        /// The offset of the health bar from the target in screen space.
        /// </summary>
        public Vector2 offset { get; set; }

        /// <summary>
        /// The target transform to follow.
        /// </summary>
        public Transform target { get; set; }

        protected virtual void Awake()
        {
            m_camera = Camera.main;
        }

        /// <summary>
        /// Sets the health bar fill amount.
        /// </summary>
        /// <param name="value">A value between 0 and 1.</param>
        public void SetHealth(float value)
        {
            fillImage.fillAmount = value;
        }

        /// <summary>
        /// Updates the position of the health bar.
        /// </summary>
        public void UpdatePosition()
        {
            if (!target.gameObject.activeInHierarchy || !m_camera)
                return;

            transform.position = m_camera.WorldToScreenPoint(target.position) + (Vector3)offset;
        }
    }
}
