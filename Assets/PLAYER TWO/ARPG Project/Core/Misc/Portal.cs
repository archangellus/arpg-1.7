using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Portal")]
    public class Portal : MonoBehaviour
    {
        [Tooltip("The name of the scene to teleport the Entity.")]
        public string scene;

        [Header("Exit Coordinates")]
        [Tooltip("If true, the portal will set the next scene coordinates.")]
        public bool setNextSceneCoordinates;

        [Tooltip("The desired position of the player in the next scene.")]
        public Vector3 exitPosition;

        [Tooltip("The desired rotation of the player in the next scene.")]
        public Vector3 exitRotation;

        [Tooltip("The sprite that represents the loading screen. Left empty to use the default.")]
        public Sprite loadingSprite;

        protected Collider m_collider;

        protected virtual void Start()
        {
            m_collider = GetComponent<Collider>();
            m_collider.isTrigger = true;
        }

        protected virtual void OnTriggerEnter()
        {
            if (setNextSceneCoordinates)
                GameScenes.instance.SetNextSceneCoordinates(exitPosition, exitRotation);

            GameScenes.instance.LoadScene(scene, loadingSprite);
        }
    }
}
