using UnityEngine;
using System;
namespace ProjectDoors
{
    [CreateAssetMenu(menuName = "ProjectDoors/EventChannels/DoorEventChannel")]
    public class DoorEventChannel : ScriptableObject
    {
        public event Action<Vector3, bool> OnOpenRequest;
        public event Action<bool> OnCloseRequest;
        public bool IsDoorOpen { get; private set; }

        public void RaiseOpen(Vector3 userPosition, bool force = false)
        {
            IsDoorOpen = true;
            OnOpenRequest?.Invoke(userPosition, force);
        }

        public void RaiseClose(bool force = false)
        {
            IsDoorOpen = false;
            OnCloseRequest?.Invoke(force);
        }
    }
}
