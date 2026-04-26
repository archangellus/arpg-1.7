using UnityEngine;
using System;
namespace ProjectDoors
{

    [CreateAssetMenu(menuName = "ProjectDoors/EventChannels/MovementControlEventChannel")]
    public class MovementControlEventChannel : ScriptableObject
    {
        public static MovementControlEventChannel Instance { get; private set; }
        public event Action OnStopMovement;
        public event Action OnResumeMovement;
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

        public void RaiseStopMovement() => OnStopMovement?.Invoke();
        public void RaiseResumeMovement() => OnResumeMovement?.Invoke();
    }
}
