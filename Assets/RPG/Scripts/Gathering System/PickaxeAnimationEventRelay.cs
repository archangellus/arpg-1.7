using UnityEngine;

namespace ShatterStone
{
    public class PickaxeAnimationEventRelay : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        [Header("Runtime Search")]
        [Tooltip("Optional. If empty, this uses transform.root.")]
        [SerializeField] private Transform searchRoot;

        [Tooltip("If true, searches all active PickaxeEventController objects if no local/current one is found.")]
        [SerializeField] private bool allowGlobalSceneSearch = true;

        private PickaxeEventController cachedPickaxe;
        /*
        private void Awake()
        {
            Log("Relay Awake on: " + name);
        }
        */
        public void AnimationEvent_BeginSwing()
        {
            //Log("AnimationEvent_BeginSwing received.");
            GetPickaxe()?.BeginSwing();
        }

        public void AnimationEvent_OpenHitWindow()
        {
            //Log("AnimationEvent_OpenHitWindow received.");
            GetPickaxe()?.OpenHitWindow();
        }

        public void AnimationEvent_Hit()
        {
            //Log("AnimationEvent_Hit received.");
            GetPickaxe()?.PerformHit();
        }

        public void AnimationEvent_CloseHitWindow()
        {
            //Log("AnimationEvent_CloseHitWindow received.");
            GetPickaxe()?.CloseHitWindow();
        }

        public void AnimationEvent_EndSwing()
        {
            //Log("AnimationEvent_EndSwing received.");
            GetPickaxe()?.EndSwing();
        }

        private PickaxeEventController GetPickaxe()
        {
            if (cachedPickaxe != null && cachedPickaxe.isActiveAndEnabled)
            {
                //Log("Using cached pickaxe: " + cachedPickaxe.name);
                return cachedPickaxe;
            }

            cachedPickaxe = FindPickaxeController();

            if (cachedPickaxe == null)
            {
                LogWarning("No active PickaxeEventController found.");
                return null;
            }

            Log("Found pickaxe controller: " + cachedPickaxe.name);
            return cachedPickaxe;
        }

        private PickaxeEventController FindPickaxeController()
        {
            Transform root = searchRoot != null ? searchRoot : transform.root;

            //Log("Searching local root: " + root.name);

            PickaxeEventController[] localPickaxes =
                root.GetComponentsInChildren<PickaxeEventController>(true);

            foreach (PickaxeEventController pickaxe in localPickaxes)
            {
                if (pickaxe != null && pickaxe.isActiveAndEnabled)
                    return pickaxe;
            }

            if (PickaxeEventController.Current != null &&
                PickaxeEventController.Current.isActiveAndEnabled)
            {
                //Log("Using PickaxeEventController.Current.");
                return PickaxeEventController.Current;
            }

            if (!allowGlobalSceneSearch)
                return null;

            //Log("Searching entire active scene for PickaxeEventController.");

#if UNITY_2023_1_OR_NEWER
            PickaxeEventController[] allPickaxes =
                Object.FindObjectsByType<PickaxeEventController>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None
                );
#else
            PickaxeEventController[] allPickaxes =
                Object.FindObjectsOfType<PickaxeEventController>();
#endif

            PickaxeEventController closest = null;
            float closestDistance = float.MaxValue;

            foreach (PickaxeEventController pickaxe in allPickaxes)
            {
                if (pickaxe == null || !pickaxe.isActiveAndEnabled)
                    continue;

                float distance = Vector3.Distance(transform.position, pickaxe.transform.position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = pickaxe;
                }
            }

            return closest;
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.Log("[PickaxeAnimationEventRelay] " + message, this);
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.LogWarning("[PickaxeAnimationEventRelay] " + message, this);
        }
    }
}