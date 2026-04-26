using UnityEngine;
using System;

namespace ProjectDoors
{
    [CreateAssetMenu(menuName = "ProjectDoors/EventChannels/InteractionEventChannel")]
    public class InteractionEventChannel : ScriptableObject
    {
        public static InteractionEventChannel Instance { get; private set; }
        public event Action<Vector3> OnInteractRequest;

        private void OnEnable()
        {
            if (Instance == null)
                Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        public void RaiseInteractRequest(Vector3 position, bool isOpen, Door door)
        {
            OnInteractRequest?.Invoke(position);
        }
    }
}
