using UnityEngine;
using UnityEngine.Events;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectDoors
{
    //[ExecuteAlways]
    [RequireComponent(typeof(Collider))]
    public class DoorTrigger : MonoBehaviour
    {
        [SerializeField] private Door controlledDoor;
        [SerializeField] private DoorEventChannel doorEventChannel;

        [SerializeField] private string[] allowedTags = new[] { "Untagged" };
        [SerializeField] private bool autoOpen = true;
        [SerializeField] private bool autoClose = true;
        [SerializeField] private float closeDelay = 2f;

        [Header("Unity Events")]
        [SerializeField] private UnityEvent onTriggerEnterEvent;
        [SerializeField] private UnityEvent onTriggerExitEvent;

        private int allowedCount;

        private void Reset() => AutoAssign();
        private void OnValidate() => AutoAssign();
        private void Awake() => AutoAssign();
        private void OnEnable() => AutoAssign();

        private void AutoAssign()
        {
#if UNITY_EDITOR
            // Skip in prefab asset edit mode
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject) && !Application.isPlaying) return;
#endif
            if (controlledDoor == null)
            {
                controlledDoor = FindNearestDoor();
                if (controlledDoor == null)
                    Debug.LogWarning($"[{name}] DoorTrigger: no Door found in parent group.");
            }

            if (controlledDoor != null && doorEventChannel == null)
            {
                doorEventChannel = controlledDoor.GetEventChannel();
                if (doorEventChannel == null)
                    Debug.LogWarning($"[{name}] Door has no DoorEventChannel assigned.");
            }

            // Ensure we have a trigger collider
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        // Only checks this object's parent subtree
        private Door FindNearestDoor()
        {
            // 1. If inside a Door already
            var parentDoor = GetComponentInParent<Door>();
            if (parentDoor != null) return parentDoor;

            // 2. Only search among siblings under the same immediate parent
            if (transform.parent != null)
            {
                var doors = transform.parent.GetComponentsInChildren<Door>(true);
                if (doors.Length > 0) return doors[0];
            }

            // 3. Fallback: scene-wide (rare)
            var all = Object.FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (controlledDoor == null || doorEventChannel == null) return;
            if (!System.Array.Exists(allowedTags, t => other.CompareTag(t))) return;

            allowedCount++;
            onTriggerEnterEvent?.Invoke();

            if (autoOpen && !controlledDoor.IsOpen)
            {
                if (controlledDoor.IsLeverControlled)
                    controlledDoor.Open(other.transform.position);
                else
                    doorEventChannel.RaiseOpen(other.transform.position);

                if (autoClose) StopAllCoroutines();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (controlledDoor == null || doorEventChannel == null) return;
            if (!System.Array.Exists(allowedTags, t => other.CompareTag(t))) return;

            allowedCount--;
            onTriggerExitEvent?.Invoke();

            if (allowedCount <= 0 && controlledDoor.IsOpen)
            {
                if (autoClose)
                    StartCoroutine(AutoClose());
                else if (controlledDoor.IsLeverControlled)
                    controlledDoor.Close();
                else
                    doorEventChannel.RaiseClose();
            }
        }

        private IEnumerator AutoClose()
        {
            yield return new WaitForSeconds(closeDelay);
            if (allowedCount <= 0 && controlledDoor.IsOpen)
            {
                if (controlledDoor.IsLeverControlled)
                    controlledDoor.Close();
                else
                    doorEventChannel.RaiseClose();
            }
        }
    }
}
