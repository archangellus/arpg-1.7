using UnityEngine;
using System;
namespace ProjectDoors
{
    [CreateAssetMenu(menuName = "ProjectDoors/EventChannels/ProximityEventChannel")]
    public class ProximityEventChannel : ScriptableObject
    {
        public event Action<Vector3> OnEnterProximity;
        public event Action<Vector3> OnExitProximity;

        public void RaiseEnterProximity(Vector3 position)
        {
            if (OnEnterProximity != null)
            {
                OnEnterProximity.Invoke(position);
            }
            /*
            else
            {
                Debug.LogWarning("OnEnterProximity event is not subscribed.");
            }
            */
        }

        public void RaiseExitProximity(Vector3 position)
        {
            if (OnExitProximity != null)
            {
                OnExitProximity.Invoke(position);
            }
            /*
            else
            {
                Debug.LogWarning("OnExitProximity event is not subscribed.");
            }
            */
        }

        public bool HasEnterProximityListeners()
        {
            return OnEnterProximity != null;
        }

        public bool HasExitProximityListeners()
        {
            return OnExitProximity != null;
        }
    }
}