using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;


namespace ProjectDoors
{
    [ExecuteAlways]
    #region Enums
    public enum PositionPreset
    {
        Center,
        TopLeft,
        TopMiddle,
        TopRight,
        RightMiddle,
        RightBottom,
        BottomMiddle,
        BottomLeft,
        MiddleLeft
    }

    public enum GeometryAlignment
    {
        XYZ,
        XZY,
        YXZ
    }
    #endregion
    public class Door : MonoBehaviour
    {
        #region DOOR_PUBLIC_API_AND_FIELDS
        public void Open(Vector3 userPosition) => OpenDoor(false, userPosition);
        public void ForceOpen(Vector3 userPosition) => OpenDoor(true, userPosition);

        // If your old code used ForceOpen() with no userPosition:
        public void ForceOpen() => OpenDoor(true, Vector3.zero);
        public void Close() => CloseDoor(false);
        public void ForceClose() => CloseDoor(true);

        [SerializeField] public DoorEventChannel doorEventChannel;
        [SerializeField] public string pivotName = "Pivot";
        [SerializeField] public Transform pivot;
        [HideInInspector] public bool IsPivotAdjustmentEnabled = false;
        [SerializeField] public bool EnableGridSnapping = true;
        [SerializeField] public float GridSize = 0.2f;
        [SerializeField] public bool limitPivotToBounds = false; // Default: Disabled
        [SerializeField] public GameObject doorObject;
        public DoorPivotObject pivotObject;// = new DoorPivotObject();
        [System.Serializable]
        public class DoorPivotObject
        {
            [HideInInspector]
            public GameObject targetObject;

            [HideInInspector]
            public bool isLocked = false;
        }
        public PositionPreset positionPreset;
        public GeometryAlignment objectGeometryAlignment;
        [SerializeField] private float openDelay = 0f;
        [SerializeField] private float closeDelay = 0f;

        public float OpenDelay
        {
            get => openDelay;
            set => openDelay = Mathf.Max(0f, value);
        }

        public float CloseDelay
        {
            get => closeDelay;
            set => closeDelay = Mathf.Max(0f, value);
        }

        [SerializeField] public GameObject Lever; // Store the lever instance in the Door script
        [SerializeField] public bool IsLeverControlled = false; // New flag to restrict control to levers
        [SerializeField] public bool isDualDoor = false;
        [SerializeField] public Door secondDoor;
        [SerializeField]
        public bool disableColliderWhileMoving = true;
        [SerializeField] public bool disablenavmeshobstacle = true;
        [SerializeField] public Axis rotationAxis = Axis.Y;
        public enum Axis { X, Y, Z }
        public bool IsOpen { get; private set; }
        [SerializeField] public bool activateSliding = false;
        [SerializeField] public float Speed = 1f;
        [SerializeField] public float RotationAmount = 90f;
        [SerializeField] public float ForwardDirection = 0;
        [SerializeField] public float gizmoXRotation = 0f;
        [SerializeField] public float gizmoZRotation = 90f;
        [SerializeField] public float gizmoYOffset = 0f;
        [SerializeField] public Vector3 SlideDirection = Vector3.back;
        [SerializeField] public float SlideAmount = 1.9f;
        [SerializeField][HideInInspector] public bool rotateWhileSliding = false;
        [SerializeField][HideInInspector] public Vector3 rotationAxisDuringSlide = Vector3.up;
        [SerializeField][HideInInspector] public float rotationSpeedDuringSlide = 90f;
        [SerializeField] public bool invertRotationOnClose = false;
        [SerializeField] public bool allowCustomRotationSpeed = false;
        [SerializeField] public AnimationCurve doorAnimationOnOpen = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] public AnimationCurve doorAnimationOnClose = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private List<AudioClip> OpenSounds = new List<AudioClip>();
        [SerializeField] private List<AudioClip> CloseSounds = new List<AudioClip>();
        [SerializeField] public bool matchSpeedToSound = true;

        [SerializeField, Range(0.5f, 1.5f)] public float pitch = 1f;      // fixed pitch
        [SerializeField] public bool useRandomPitch = false;               // random-pitch flag
        [SerializeField, Range(0.5f, 1.5f)] public float randomPitchMin = 0.8f;
        [SerializeField, Range(0.5f, 1.5f)] public float randomPitchMax = 1.2f;

        [Range(0f, 1f)][SerializeField] public float volume = 1f;
        [SerializeField] public Texture2D customCursor;
        [SerializeField] public Vector2 cursorHotspot = Vector2.zero;
        [SerializeField] private List<ComponentConfig> componentConfigs = new List<ComponentConfig>();
        [SerializeField] private List<ObjectConfig> objectConfigs = new List<ObjectConfig>();
        [SerializeField] private UnityEvent onDoorOpen;
        [SerializeField] private UnityEvent onDoorClose;
        public bool disableAtRuntime = true;
        public GameObject door;
        public List<DoorAttachedObject> attachedObjects = new List<DoorAttachedObject>();
        public Vector3 autoMinBounds;
        public Vector3 autoMaxBounds;
        private Vector3 manualMinBounds;
        private Vector3 manualMaxBounds;
        public float xLeeway = 0.1f;
        public Color BoundsGizmoColor = Color.cyan;
        [SerializeField] public bool limitKnobToBounds = false;
        [SerializeField] public bool knobEnableGridSnapping = false;
        [SerializeField, Min(0.01f)]
        public float knobGridSize = 0.25f;
        [SerializeField] public ModeSelect mode = ModeSelect.MaterialBrightness;
        public enum ModeSelect
        {
            None,
            MaterialChanger,
            MaterialBrightness
        }

        public ModeSelect Mode
        {
            get => mode;
            set
            {
                mode = value;
                OnValidateMode();
            }
        }

        [SerializeField] public List<GameObject> targetObjects = new List<GameObject>();
        public Material highlightMaterial;
        [Range(0.1f, 2.0f)] public float brightnessFactor = 1.25f;
        [Range(0.1f, 1.0f)] public float fadeDuration = 1f;
        public bool loopBrightnessEffect = false;

        private bool UseRotation => !activateSliding;
        private bool useMaterialChanger = false;
        private bool useMaterialBrightness = true;
        private bool isFadingOut = false;
        private bool isMouseOver = false;
        private float fadeAmount = 1;
        private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        private Dictionary<Renderer, MaterialPropertyBlock> materialBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();
        private Camera mainCamera;
        private float raycastCooldown = 0.1f;
        private float lastRaycastTime = 0f;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private Vector3 lastScale;
#if UNITY_EDITOR
        private Vector3 lastDoorObjectPosition;
        private Quaternion lastDoorObjectRotation;
        private Vector3 lastDoorObjectScale;

        private Vector3 lastEditorPosition;
        private Quaternion lastEditorRotation;
        private Vector3 lastEditorScale;

        private Vector3 lastDoorPosition;
        private Quaternion lastDoorRotation;
        private Vector3 lastDoorScale;
#endif
        public LayerMask raycastLayerMask = 1 << 6;
        public float raycastRange = 100f; // Limit the raycast range

        private List<Renderer> cachedKeys;

        [System.Serializable]
        public class ComponentConfig
        {
            public Component component;

            [Header("Disable/Enable on Open")]
            public bool disableOnOpen = false;
            public float disableOnOpenDelay = 1f;
            public bool enableOnOpen = false;
            public float enableOnOpenDelay = 1f;

            [Header("Disable/Enable on Close")]
            public bool disableOnClose = false;
            public float disableOnCloseDelay = 1f;
            public bool enableOnClose = false;
            public float enableOnCloseDelay = 1f;
        }


        [System.Serializable]
        public class ObjectConfig
        {
            public GameObject gameObject;

            [Header("Disable/Enable on Open")]
            public bool disableOnOpen = false;
            public float disableOnOpenDelay = 1f;
            public bool enableOnOpen = false;
            public float enableOnOpenDelay = 1f;

            [Header("Disable/Enable on Close")]
            public bool disableOnClose = false;
            public float disableOnCloseDelay = 1f;
            public bool enableOnClose = false;
            public float enableOnCloseDelay = 1f;
        }

        [System.Serializable]
        public class DoorAttachedObject
        {
            public GameObject targetObject;

            public Vector3 positionOffset = Vector3.zero;

            public bool isLocked = false;
        }

        private bool isRuntime;
        private AudioSource audioSource;
        private Collider doorCollider;
        private UnityEngine.AI.NavMeshObstacle navMeshObstacle;
        private Vector3 StartRotation;
        private Vector3 StartPosition;
        private Vector3 Forward;
        private Coroutine AnimationCoroutine;
        private bool isCursorOverDoor = false;

        private IEnumerator ModifyStateAfterDelay(
        bool useSoundDuration,
        List<AudioClip> soundClips,
        float manualDelay,
        bool enable,
        List<Component> components,
        List<GameObject> gameObjects
    )
        {
            float delay = useSoundDuration && soundClips.Count > 0
                ? PlayRandomSound(soundClips)
                : manualDelay;

            yield return new WaitForSeconds(delay);

            // Modify state of components
            foreach (var component in components)
            {
                if (component is Behaviour behaviour)
                {
                    behaviour.enabled = enable;
                }
                else if (component is Collider collider)
                {
                    collider.enabled = enable;
                }
                else if (component is Renderer renderer)
                {
                    renderer.enabled = enable;
                }
            }

            // Modify state of GameObjects
            foreach (var obj in gameObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(enable);
                }
            }
        }
        #endregion
        #region Unity Lifecycle
        private void OnValidate()
        {
            // Automatically assign the door field to this GameObject
            if (door == null)
            {
                door = gameObject;
            }

            // Check and handle TargetObjects
            for (int i = 0; i < targetObjects.Count; i++)
            {
                if (targetObjects[i] == null)
                {
                    DoorLogger.Log($"Removing null target object at index {i}.");
                    targetObjects.RemoveAt(i);
                    i--; // Adjust index due to removal
                }
            }

            // Add this GameObject to TargetObjects if it's empty
            if (targetObjects.Count == 0)
            {
                DoorLogger.Log("Adding this GameObject to TargetObjects as the list is empty.");
                targetObjects.Add(this.gameObject);
            }

            if (mode == ModeSelect.MaterialChanger && highlightMaterial == null)
            {
                DoorLogger.LogWarning("Highlight Material is missing! Assigning default highlight material.");

#if UNITY_EDITOR
                // If this is not a prefab asset (or prefab being edited), schedule the asset creation
                if (!PrefabUtility.IsPartOfPrefabAsset(gameObject))
                {
                    // Delay the creation so it happens after OnValidate has completed.
                    EditorApplication.delayCall += CreateDefaultHighlightMaterial;
                }
                else
                {
                    // If in prefab mode, avoid modifying the prefab and simply try to load the asset.
                    highlightMaterial = Resources.Load<Material>("DefaultHighlightMaterial");
                }
#else
            // In a runtime build, just load from Resources.
            highlightMaterial = Resources.Load<Material>("DefaultHighlightMaterial");
#endif
            }


            // Additional OnValidate logic
            FindPivot();
            audiosourceVolume();
            OnValidateMode();
            soundsPitch();

        }

        void soundsPitch()
        {
            pitch = Mathf.Clamp(pitch, 0.5f, 1.5f);
            randomPitchMin = Mathf.Clamp(randomPitchMin, 0.5f, 1.5f);
            randomPitchMax = Mathf.Clamp(randomPitchMax, 0.5f, 1.5f);
            if (randomPitchMin > randomPitchMax)
                (randomPitchMin, randomPitchMax) = (randomPitchMax, randomPitchMin);
        }


#if UNITY_EDITOR
        private void CreateDefaultHighlightMaterial()
        {
            // 1. Check that this component still exists (the delay may have caused the object to be unloaded).
            if (this == null)
                return;

            // 2. Ensure that the Resources folder exists.
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
                DoorLogger.LogWarning("Resources folder was missing and has been created.");
            }

            // 3. Try to load the default highlight material from the Resources folder.
            Material defaultMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/DefaultHighlightMaterial.mat");

            // 4. If the material doesn't exist, create it with the correct shader for the current pipeline.
            if (defaultMaterial == null)
            {
                Shader chosenShader = GetAppropriatePipelineShader();
                if (chosenShader == null)
                {
                    // Fallback if we somehow can’t find any of the pipeline shaders
                    chosenShader = Shader.Find("Standard");
                }

                defaultMaterial = new Material(chosenShader);
                AssetDatabase.CreateAsset(defaultMaterial, "Assets/Resources/DefaultHighlightMaterial.mat");
                AssetDatabase.SaveAssets();
                DoorLogger.LogWarning("Default highlight material was missing and has been created.");
            }

            // 5. Assign the newly created (or found) material to 'highlightMaterial'.
            highlightMaterial = defaultMaterial;
        }

        /// <summary>
        /// Detects which render pipeline is active and returns the appropriate highlight shader.
        /// 
        ///  - URP: "Universal Render Pipeline/Lit"
        ///  - HDRP: "HDRP/Lit"
        ///  - Built-in/Standard: "Standard" (fallback if no pipeline is recognized)
        /// </summary>
        private Shader GetAppropriatePipelineShader()
        {
            // Check which pipeline is currently in use
            RenderPipelineAsset currentPipeline = GraphicsSettings.currentRenderPipeline;

            if (currentPipeline != null)
            {
                string pipelineType = currentPipeline.GetType().Name;

                // URP detection
                if (pipelineType == "UniversalRenderPipelineAsset")
                {
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpShader != null)
                        return urpShader;
                }
                // HDRP detection
                else if (pipelineType == "HDRenderPipelineAsset")
                {
                    Shader hdrpShader = Shader.Find("HDRP/Lit");
                    if (hdrpShader != null)
                        return hdrpShader;
                }
            }

            // If we get here, assume built-in pipeline
            Shader builtInShader = Shader.Find("Standard");
            return builtInShader;
        }
#endif

        private void OnValidateMode()
        {
            switch (mode)
            {
                case ModeSelect.MaterialChanger:
                    useMaterialChanger = true;
                    useMaterialBrightness = false;
                    break;

                case ModeSelect.MaterialBrightness:
                    useMaterialChanger = false;
                    useMaterialBrightness = true;
                    break;

                default:
                    useMaterialChanger = false;
                    useMaterialBrightness = false;
                    break;
            }
        }
        /* Testing *//*
        #if unityeditor
                private void _OnValidate()
                {
                    if (this == null) return;
                    if (doorObject != null)
                    {
                        FindPivot();
                        EditorUpdate();
                    }

                }


                private void OnTransformParentChanged()
                {
                    UpdateDoorBounds();
                    SceneView.RepaintAll();
                }
        #endif
        */
        private void EditorUpdate()
        {
            if (doorObject == null)
            {
                doorObject = gameObject; // Fallback to the object itself
            }
            FindPivot();
            EnsureAttachedObject();
            ApplyPositionPreset();
        }

        private void Update()
        {
            DetectMouseHover();
            HandleRaycastCooldown();
            UpdateMaterialBrightness();

            if (Application.isPlaying && disableAtRuntime)
            {
                return;
            }

            isRuntime = Application.isPlaying;

            if (HasTransformChanged())
            {
                UpdateDoorBounds();
            }
            FindPivot();
            UpdateAttachedObjects();
        }


        private void Start()
        {
            TargetObjects();
            UpdateCachedKeys();

            // Cache initial transform properties
            if (door != null)
            {
                lastPosition = door.transform.position;
                lastRotation = door.transform.rotation;
                lastScale = door.transform.localScale;
            }
        }

        private void Awake()
        {
            // Automatically find the pivot by name in the hierarchy
            FindPivot();

            if (pivot == null)
            {
                //Debug.LogWarning($"Pivot with name '{pivotName}' not found. Using object's transform as fallback.");
                pivot = transform; // Fallback to the object's own transform
            }

            StartRotation = transform.rotation.eulerAngles;
            Forward = transform.right;
            StartPosition = transform.position;

            if ((OpenSounds.Count > 0 || CloseSounds.Count > 0) && !TryGetComponent(out audioSource))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            if (audioSource != null)
            {
                audioSource.volume = volume;
            }

            doorCollider = GetComponent<Collider>();
            navMeshObstacle = GetComponent<UnityEngine.AI.NavMeshObstacle>();

            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                DoorLogger.LogError("Main Camera is not assigned or found in the scene. Please ensure a camera is tagged as 'MainCamera'.");
            }
            cachedKeys = new List<Renderer>();
        }

        private bool ShouldSkipUpdate()
        {
            return !Application.isPlaying;
        }

        private void OnEnable()
        {
            if (doorEventChannel != null)
            {
                doorEventChannel.OnOpenRequest += OnDoorEventOpen;
                doorEventChannel.OnCloseRequest += OnDoorEventClose;
            }
            else
            {
                DoorLogger.LogError($"DoorEventChannel is not assigned on door: '{gameObject.name}'. The door will not function.");
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update += TrackManualTransformChanges;

                // Track the transform that has this Door script
                lastEditorPosition = transform.position;
                lastEditorRotation = transform.rotation;
                lastEditorScale = transform.localScale;

                // NEW: also cache the doorObject�s transform if it exists
                if (doorObject != null)
                {
                    lastDoorObjectPosition = doorObject.transform.position;
                    lastDoorObjectRotation = doorObject.transform.rotation;
                    lastDoorObjectScale = doorObject.transform.localScale;
                }

                // If "door" is assigned
                if (door != null)
                {
                    lastDoorPosition = door.transform.position;
                    lastDoorRotation = door.transform.rotation;
                    lastDoorScale = door.transform.localScale;
                }
            }
#endif
        }

        private void OnDisable()
        {
            if (doorEventChannel != null)
            {
                doorEventChannel.OnOpenRequest -= OnDoorEventOpen;
                doorEventChannel.OnCloseRequest -= OnDoorEventClose;
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update -= TrackManualTransformChanges;
            }
#endif
        }

        private bool HasTransformChanged()
        {
            if (door == null) return false;

            // Compare current transform properties with cached ones
            var currentPosition = door.transform.position;
            var currentRotation = door.transform.rotation;
            var currentScale = door.transform.localScale;

            if (currentPosition != lastPosition || currentRotation != lastRotation || currentScale != lastScale)
            {
                lastPosition = currentPosition;
                lastRotation = currentRotation;
                lastScale = currentScale;

                //UpdateDoorBounds();  // Ensure bounds update on change
                return true; // Transform has changed
            }
            return false;
        }

        public Vector3 ClampToColliderBounds(Vector3 position)
        {
            if (door.TryGetComponent<Collider>(out Collider collider))
            {
                Bounds bounds = collider.bounds;
                position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
                position.y = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
                position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            }
            else
            {
                DoorLogger.LogWarning("Door object does not have a Collider. Bounds clamping skipped.");
            }

            return position;
        }
        #endregion
        #region Position Presets
        public void ApplyPositionPreset()
        {
            if (pivotObject == null || pivotObject.targetObject == null || doorObject == null)
                return;

            if (pivotObject.isLocked)
            {
                pivotObject.isLocked = false; // Unlock before applying preset
            }

            Transform originalParent = doorObject.transform.parent;
            doorObject.transform.parent = null; // Temporarily unparent the door

            Vector3 minBounds = autoMinBounds;
            Vector3 maxBounds = autoMaxBounds;

            Vector3 presetPosition = CalculatePresetPosition(minBounds, maxBounds);

            Vector3 targetPosition = AdjustForGeometryAlignment(presetPosition);

            targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);
            targetPosition.z = Mathf.Clamp(targetPosition.z, minBounds.z, maxBounds.z);

            pivotObject.targetObject.transform.position = targetPosition;

            pivotObject.targetObject.transform.parent = originalParent; // Maintain the hierarchy
            doorObject.transform.parent = pivotObject.targetObject.transform; // Reparent the door to the pivot

            pivotObject.isLocked = true; // Lock after applying preset
        }

        private Vector3 CalculatePresetPosition(Vector3 minBounds, Vector3 maxBounds)
        {
            float x = doorObject.transform.position.x;
            float y = 0f, z = 0f;

            switch (positionPreset)
            {
                case PositionPreset.Center:
                    y = (minBounds.y + maxBounds.y) / 2;
                    z = (minBounds.z + maxBounds.z) / 2;
                    x = (minBounds.x + maxBounds.x) / 2;
                    break;
                case PositionPreset.TopLeft:
                    y = maxBounds.y;
                    z = maxBounds.z;
                    x = minBounds.x;
                    break;
                case PositionPreset.TopMiddle:
                    y = maxBounds.y;
                    z = (minBounds.z + maxBounds.z) / 2;
                    x = (minBounds.x + maxBounds.x) / 2;
                    break;
                case PositionPreset.TopRight:
                    y = maxBounds.y;
                    z = minBounds.z;
                    x = maxBounds.x;
                    break;
                case PositionPreset.RightMiddle:
                    y = (minBounds.y + maxBounds.y) / 2;
                    x = minBounds.x;
                    z = minBounds.z;
                    break;
                case PositionPreset.RightBottom:
                    y = minBounds.y;
                    z = minBounds.z;
                    x = minBounds.x;
                    break;
                case PositionPreset.BottomMiddle:
                    y = minBounds.y;
                    z = (minBounds.z + maxBounds.z) / 2;
                    x = (minBounds.x + maxBounds.x) / 2;
                    break;
                case PositionPreset.BottomLeft:
                    y = minBounds.y;
                    z = maxBounds.z;
                    x = (minBounds.x + maxBounds.x) / 2;
                    break;
                case PositionPreset.MiddleLeft:
                    y = (minBounds.y + maxBounds.y) / 2;
                    z = maxBounds.z;
                    x = maxBounds.x;
                    break;
                default:
                    DoorLogger.LogWarning("Unknown position preset. Defaulting to Center.");
                    x = (minBounds.x + maxBounds.x) / 2;
                    y = (minBounds.y + maxBounds.y) / 2;
                    z = (minBounds.z + maxBounds.z) / 2;
                    break;
            }

            return new Vector3(x, y, z);
        }

        private Vector3 AdjustForGeometryAlignment(Vector3 presetPosition)
        {
            switch (objectGeometryAlignment)
            {
                case GeometryAlignment.XYZ:
                    return presetPosition;
                case GeometryAlignment.XZY:
                    return new Vector3(presetPosition.x, presetPosition.z, presetPosition.y);
                case GeometryAlignment.YXZ:
                    return new Vector3(presetPosition.y, presetPosition.x, presetPosition.z);
                default:
                    return presetPosition;
            }
        }
        #endregion
        #region Pivot

        public void FindPivot()
        {
            // Automatically search for a pivot by name in the object's hierarchy
            if (!string.IsNullOrEmpty(pivotName))
            {
                Transform foundPivot = transform.Find(pivotName);

                if (foundPivot != null)
                {
                    pivot = foundPivot;
                    //Debug.Log($"Custom pivot '{pivotName}' found and assigned automatically.");
                }
                else
                {
                    //Debug.LogWarning($"Custom pivot '{pivotName}' not found in the hierarchy.");
                }
            }
            else
            {
                //Debug.LogWarning("Pivot name is empty. Please specify a name to search for a custom pivot.");
            }
        }

        private string GenerateUniquePivotName()
        {
            // Generate a random 8-character alphanumeric string
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            System.Text.StringBuilder result = new System.Text.StringBuilder(8);
            System.Random random = new System.Random();

            for (int i = 0; i < 8; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }

            return $"Pivot-{result}";
        }

        public bool HasParentError { get; private set; } = false;

        public void EnsureAttachedObject()
        {
            // If the doorObject is at the root (has no parent), do not create a pivot automatically.
            if (doorObject != null && doorObject.transform.parent == null)
            {
                DoorLogger.Log("Door object is in the root; automatic pivot creation is skipped.");
                return;
            }

            HasParentError = false; // Reset error state

            if (pivotObject == null || pivotObject.targetObject == null)
            {
                // Generate a unique pivot name.
                string uniquePivotName = GenerateUniquePivotName();

                // Create a new GameObject with the unique name and set its parent.
                GameObject newPivot = new GameObject(uniquePivotName);
                newPivot.transform.SetParent(doorObject.transform.parent, false); // Requires a parent to work correctly

                // Find the newly created pivot by name and assign it.
                Transform foundPivot = transform.root.Find(uniquePivotName);
                if (foundPivot != null)
                {
                    pivot = foundPivot;
                    pivotObject = new DoorPivotObject { targetObject = foundPivot.gameObject };
                    pivotName = uniquePivotName;
                    DoorLogger.Log($"Created and assigned pivot with unique name: {uniquePivotName}");
                }
                else
                {
                    DoorLogger.LogError($"Failed to find the created pivot: {uniquePivotName}");
                    HasParentError = true; // Set error flag
                }
            }
        }

        private Transform FindInHierarchy(string name)
        {
            // Search the hierarchy for the pivot
            if (transform.name == name)
            {
                return transform;
            }

            Transform parent = transform.parent;
            while (parent != null)
            {
                if (parent.name == name)
                {
                    return parent;
                }

                parent = parent.parent;
            }

            return GameObject.Find(name)?.transform;
        }

        public void RotateAroundPivot(Vector3 eulerAngles)
        {
            if (pivot != null)
            {
                Vector3 pivotPosition = pivot.position;
                transform.RotateAround(pivotPosition, Vector3.up, eulerAngles.y);
            }
            else
            {
                DoorLogger.LogWarning("No pivot assigned. Ensure the pivot is set correctly.");
            }
        }

        public void SlideRelativeToPivot(Vector3 direction, float amount)
        {
            if (pivot != null)
            {
                Vector3 slideOffset = direction.normalized * amount;
                transform.position = pivot.position + slideOffset;
            }
            else
            {
                DoorLogger.LogWarning("No pivot assigned. Ensure the pivot is set correctly.");
            }
        }

        public Transform GetPivot()
        {
            return pivot;
        }

        public void SetPivotName(string newPivotName)
        {
            pivotName = newPivotName;
            FindPivot();
        }

        private void ResetToStart()
        {
            if (pivot != null)
            {
                pivot.position = StartPosition;
                pivot.rotation = Quaternion.Euler(StartRotation);
            }
            else
            {
                DoorLogger.LogWarning("No pivot assigned. Cannot reset to start position.");
            }
        }
        #endregion
        #region Bounds
        private void UpdateAttachedObjects()
        {
            foreach (var attachedObject in attachedObjects)
            {
                if (attachedObject != null && attachedObject.targetObject != null)
                {
                    UpdateObjectPosition(attachedObject);
                }
            }
        }

        public void UpdateDoorBounds()
        {

            // Reset
            autoMinBounds = Vector3.zero;
            autoMaxBounds = Vector3.zero;
            Bounds? combinedBounds = null;

            if (door != null)
            {
                Renderer[] doorRenderers = door.GetComponentsInChildren<Renderer>();
                foreach (Renderer rend in doorRenderers)
                {
                    if (rend == null) continue;
                    if (combinedBounds == null)
                        combinedBounds = rend.bounds;
                    else
                        combinedBounds.Value.Encapsulate(rend.bounds);
                }
            }

            // 2) Collect from 'doorObject'
            if (doorObject != null)
            {
                Renderer[] doorObjectRenderers = doorObject.GetComponentsInChildren<Renderer>();
                foreach (Renderer rend in doorObjectRenderers)
                {
                    if (rend == null) continue;
                    if (combinedBounds == null)
                        combinedBounds = rend.bounds;
                    else
                        combinedBounds.Value.Encapsulate(rend.bounds);
                }
            }

            // 3) If combined bounds, set autoMin/Max
            if (combinedBounds.HasValue)
            {
                autoMinBounds = combinedBounds.Value.min;
                autoMaxBounds = combinedBounds.Value.max;
            }
            else
            {
                // Fallback or zero-based
                autoMinBounds = Vector3.zero;
                autoMaxBounds = Vector3.zero;
            }



            var renderer = door.GetComponent<Renderer>();

            if (renderer != null)
            {
                // Calculate automatic bounds
                autoMinBounds = renderer.bounds.min;
                autoMaxBounds = renderer.bounds.max;
            }

            // Adjust the collider size to match new bounds
            if (door.TryGetComponent<BoxCollider>(out var collider))
            {
                Vector3 newSize = autoMaxBounds - autoMinBounds;
                Vector3 newCenter = (autoMaxBounds + autoMinBounds) / 2;
                collider.size = newSize;
                collider.center = door.transform.InverseTransformPoint(newCenter);
            }

            // Ensure Scene View updates
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneView.RepaintAll();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
            /*
            else
            {
                Debug.LogWarning($"Door GameObject '{gameObject.transform.root.name}' does not have a Renderer component. Bounds cannot be calculated.");

            }
            */
        }

        private void UpdateObjectPosition(DoorAttachedObject attachedObject)
        {
            if (attachedObject.isLocked) return;

            Vector3 clampedPosition = ClampToColliderBounds(attachedObject.positionOffset);
            attachedObject.targetObject.transform.position = clampedPosition;
        }

        private void ValidateManualBounds()
        {
            if (manualMinBounds.x > manualMaxBounds.x ||
                manualMinBounds.y > manualMaxBounds.y ||
                manualMinBounds.z > manualMaxBounds.z)
            {
                DoorLogger.LogWarning("Manual bounds are invalid. Ensure that min bounds are less than max bounds.");
            }
        }

#if UNITY_EDITOR
        // Detect manual changes in the Scene View
#if UNITY_EDITOR
        private void TrackManualTransformChanges()
        {
            if (Application.isPlaying) return;

            // 1) The transform with the Door script
            if (transform.position != lastEditorPosition
                || transform.rotation != lastEditorRotation
                || transform.localScale != lastEditorScale)
            {
                lastEditorPosition = transform.position;
                lastEditorRotation = transform.rotation;
                lastEditorScale = transform.localScale;

                UpdateDoorBounds();
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            // 2) The 'door' object (knobs)
            if (door != null)
            {
                Vector3 pos = door.transform.position;
                Quaternion rot = door.transform.rotation;
                Vector3 scale = door.transform.localScale;
                if (pos != lastDoorPosition || rot != lastDoorRotation || scale != lastDoorScale)
                {
                    lastDoorPosition = pos;
                    lastDoorRotation = rot;
                    lastDoorScale = scale;

                    UpdateDoorBounds();
                    SceneView.RepaintAll();
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
            }

            // 3) The 'doorObject' (pivot)
            if (doorObject != null)
            {
                Vector3 pos = doorObject.transform.position;
                Quaternion rot = doorObject.transform.rotation;
                Vector3 scale = doorObject.transform.localScale;
                if (pos != lastDoorObjectPosition
                    || rot != lastDoorObjectRotation
                    || scale != lastDoorObjectScale)
                {
                    lastDoorObjectPosition = pos;
                    lastDoorObjectRotation = rot;
                    lastDoorObjectScale = scale;

                    UpdateDoorBounds();
                    SceneView.RepaintAll();
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
            }
        }
#endif

#endif

        #endregion
        #region Highlighting
        private void TargetObjects()
        {
            if (targetObjects.Count == 0)
            {
                targetObjects.Add(this.gameObject);
            }

            foreach (GameObject target in targetObjects)
            {
                if (target.GetComponent<Renderer>() == null)
                {
                    DoorLogger.LogWarning($"No Renderer found on {target.name}. Adding a default MeshRenderer.");
                    target.AddComponent<MeshRenderer>();
                }

                AddChildrenWithRenderers(target.transform);
            }
        }


        private void UpdateCachedKeys()
        {
            cachedKeys.Clear();
            cachedKeys.AddRange(materialBlocks.Keys);
        }

        private void HandleRaycastCooldown()
        {
            if (Time.time - lastRaycastTime > raycastCooldown)
            {
                lastRaycastTime = Time.time;
                bool currentMouseOver = IsMouseOverAnyChild();

                if (currentMouseOver != isMouseOver)
                {
                    isMouseOver = currentMouseOver;
                    HandleMouseOverState();
                }
            }
        }

        private void HandleMouseOverState()
        {
            if (isMouseOver)
            {
                if (useMaterialChanger)
                {
                    ApplyHighlightToAll();
                }
            }
            else
            {
                RestoreMaterials();
                if (useMaterialBrightness)
                {
                    isFadingOut = true;
                }
            }
        }

        private void UpdateMaterialBrightness()
        {
            if (useMaterialBrightness && (isMouseOver || isFadingOut))
            {
                MaterialBrightnessFunctionality();
            }
        }

        private bool IsMouseOverAnyChild()
        {
            if (mainCamera == null)
            {
                DoorLogger.LogWarning("Main camera is not assigned. Raycast cannot be performed.");
                return false;
            }

            RaycastHit hit;
            if (Physics.Raycast(mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue()), out hit, raycastRange, raycastLayerMask)) //new input system
            {
                GameObject hitObject = hit.transform.gameObject;
                return targetObjects.Exists(target => target == hitObject || hitObject.transform.IsChildOf(target.transform));
            }
            return false;
        }

        private void MaterialBrightnessFunctionality()
        {
            fadeAmount = UpdateFadeAmount(fadeAmount, isFadingOut, isMouseOver);

            if (cachedKeys.Count != materialBlocks.Count)
            {
                UpdateCachedKeys();
            }

            foreach (Renderer rend in cachedKeys)
            {
                if (rend && rend.enabled && rend.gameObject.activeInHierarchy)
                {
                    AdjustBrightness(rend, fadeAmount);
                }
            }
        }

        private float UpdateFadeAmount(float currentFade, bool fadingOut, bool mouseOver)
        {
            float fadeSpeed = (fadingOut ? -1 : 1) * Time.deltaTime / fadeDuration;
            currentFade += fadeSpeed;

            if (currentFade > brightnessFactor)
            {
                currentFade = brightnessFactor;
                if (loopBrightnessEffect && mouseOver)
                    isFadingOut = true;
                else if (!mouseOver)
                    isFadingOut = false;
            }
            else if (currentFade < 1)
            {
                currentFade = 1;
                if (!mouseOver)
                    isFadingOut = false;
                else if (loopBrightnessEffect && mouseOver)
                    isFadingOut = false;
            }

            return currentFade;
        }
        private void AdjustBrightness(Renderer rend, float factor)
        {
            if (!materialBlocks.TryGetValue(rend, out MaterialPropertyBlock block))
            {
                block = new MaterialPropertyBlock();
                materialBlocks[rend] = block;
                rend.GetPropertyBlock(block);
            }

            if (originalMaterials.ContainsKey(rend) && originalMaterials[rend] != null && originalMaterials[rend].Length > 0)
            {
                foreach (Material mat in originalMaterials[rend])
                {
                    if (mat != null)
                    {
                        // Check for the property in this order: _BaseColor, then _Color.
                        string colorProp = mat.HasProperty("_BaseColor") ? "_BaseColor" : (mat.HasProperty("_Color") ? "_Color" : null);
                        if (!string.IsNullOrEmpty(colorProp))
                        {
                            Color originalColor = mat.GetColor(colorProp);
                            block.SetColor(colorProp, originalColor * factor);
                        }
                    }
                }
                rend.SetPropertyBlock(block);
            }
        }


        private void ApplyHighlightToAll()
        {
            foreach (Renderer rend in originalMaterials.Keys)
            {
                if (rend && highlightMaterial)
                {
                    Material[] mats = rend.materials;
                    int matsLength = mats.Length;
                    for (int i = 0; i < matsLength; i++)
                    {
                        mats[i] = highlightMaterial;
                    }
                    rend.materials = mats;
                }
            }
        }

        private void RestoreMaterials()
        {
            foreach (var entry in originalMaterials)
            {
                if (entry.Key)
                {
                    entry.Key.materials = entry.Value;
                    entry.Key.SetPropertyBlock(materialBlocks[entry.Key]);
                }
            }
        }

        private void AddChildrenWithRenderers(Transform parent)
        {
            if (parent == null)
            {
                DoorLogger.LogWarning("Parent Transform is null. Skipping AddChildrenWithRenderers.");
                return;
            }

            Renderer[] renderers = parent.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                if (rend == null)
                {
                    DoorLogger.LogWarning($"Renderer is null on GameObject '{parent.name}'. Skipping this renderer.");
                    continue;
                }

                if (!originalMaterials.ContainsKey(rend))
                {
                    originalMaterials[rend] = rend.sharedMaterials;
                    materialBlocks[rend] = new MaterialPropertyBlock();
                    rend.GetPropertyBlock(materialBlocks[rend]);
                }
            }
        }
        #endregion
        #region Enable/Disable Components & Objects
        private void ProcessComponentConfigs(bool enable, bool isOpen)
        {
            foreach (var config in componentConfigs)
            {
                if (config.component == null) continue;

                bool shouldModify = isOpen
                    ? (enable ? config.enableOnOpen : config.disableOnOpen)
                    : (enable ? config.enableOnClose : config.disableOnClose);

                float delay = isOpen
                    ? (enable ? config.enableOnOpenDelay : config.disableOnOpenDelay)
                    : (enable ? config.enableOnCloseDelay : config.disableOnCloseDelay);

                if (shouldModify)
                {
                    StartCoroutine(ModifyComponentStateAfterDelay(config.component, enable, delay));
                }
            }
        }


        private void ProcessObjectConfigs(bool enable, bool isOpen)
        {
            foreach (var config in objectConfigs)
            {
                if (config.gameObject == null) continue;

                bool shouldModify = isOpen
                    ? (enable ? config.enableOnOpen : config.disableOnOpen)
                    : (enable ? config.enableOnClose : config.disableOnClose);

                float delay = isOpen
                    ? (enable ? config.enableOnOpenDelay : config.disableOnOpenDelay)
                    : (enable ? config.enableOnCloseDelay : config.disableOnCloseDelay);

                if (shouldModify)
                {
                    StartCoroutine(ModifyObjectStateAfterDelay(config.gameObject, enable, delay));
                }
            }
        }


        private IEnumerator ModifyComponentStateAfterDelay(Component component, bool enable, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (component is Behaviour behaviour)
            {
                behaviour.enabled = enable;
            }
            else if (component is Collider collider)
            {
                collider.enabled = enable;
            }
            else if (component is Renderer renderer)
            {
                renderer.enabled = enable;
            }
        }

        private IEnumerator ModifyObjectStateAfterDelay(GameObject obj, bool enable, float delay)
        {
            yield return new WaitForSeconds(delay);
            obj.SetActive(enable);
        }
        #endregion
        #region Events & Event Handling
        public DoorEventChannel GetEventChannel()
        {
            return doorEventChannel;
        }

        public void OnDoorEventOpen(Vector3 userPosition, bool force)
        {
            if (force)
            {
                ForceOpen(userPosition);
            }
            else
            {
                Open(userPosition);
            }
        }

        public void OnDoorEventClose(bool force)
        {
            if (force)
            {
                ForceClose();
            }
            else
            {
                Close();
            }
        }

        #endregion
        #region Custom Cursor
        private void DetectMouseHover()
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    if (!isCursorOverDoor)
                    {
                        OnMouseEnterReplacement();
                    }
                    isCursorOverDoor = true;
                    return;
                }
            }

            if (isCursorOverDoor)
            {
                OnMouseExitReplacement();
            }
            isCursorOverDoor = false;
        }

        private void OnMouseEnterReplacement()
        {
            Cursor.SetCursor(customCursor, cursorHotspot, CursorMode.Auto);
            //Debug.Log("Mouse entered the door area.");
        }

        private void OnMouseExitReplacement()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            //Debug.Log("Mouse exited the door area.");
        }
        #endregion
        #region Door Open/Close Methods

        // 1) Single "OpenDoor" method that can handle both normal & forced opens.
        public void OpenDoor(bool force, Vector3 userPosition)
        {
            // 1. Check for the channel
            if (doorEventChannel == null)
            {
                DoorLogger.LogWarning($"[{name}] DoorEventChannel is not assigned for the door: '{gameObject.name}'. Cannot open.", this);
                return;
            }

            // 2. If not forced and lever-controlled, block direct user calls
            if (!force && IsLeverControlled)
            {
                DoorLogger.LogWarning("This door is lever-controlled and cannot be opened directly.", this);
                return;
            }

            // 3. If already open, skip (or show warning)
            if (IsOpen)
            {
                //DoorLogger.LogWarning("Door is already open.", this);
                return;
            }

            // 4. Start the new unified coroutine
            StartCoroutine(OpenDoorRoutine(force, userPosition));
        }

        // 2) unified coroutine that does the actual open delay, sound, animation
        private IEnumerator OpenDoorRoutine(bool force, Vector3 userPosition)
        {
            // Use the normal openDelay unless forced
            float actualDelay = force ? 0f : openDelay;
            yield return new WaitForSeconds(actualDelay);

            // From here on, it's basically your old OpenWithDelay logic:
            IsOpen = true;

            if (AnimationCoroutine != null)
            {
                StopCoroutine(AnimationCoroutine);
            }

            float animationDuration = 1f / Speed;
            if (OpenSounds.Count > 0)
            {
                float soundDuration = PlayRandomSound(OpenSounds);
                if (matchSpeedToSound) animationDuration = soundDuration;
            }

            if (disableColliderWhileMoving && doorCollider != null)
            {
                doorCollider.enabled = false;
            }
            if (disablenavmeshobstacle && navMeshObstacle != null)
            {
                navMeshObstacle.enabled = false;
            }

            // Rotating or Sliding?
            if (UseRotation)
            {
                float dot = Vector3.Dot(Forward, (userPosition - pivot.position).normalized);
                AnimationCoroutine = StartCoroutine(DoRotationOpen(dot, animationDuration));
            }
            else
            {
                AnimationCoroutine = StartCoroutine(DoSlidingOpen(animationDuration));
            }

            // If dual door, open the second one (recursively pass 'force')
            if (isDualDoor && secondDoor != null)
            {
                secondDoor.OpenDoor(force, userPosition);
            }

            // Trigger open events
            ProcessComponentConfigs(true, true);
            ProcessComponentConfigs(false, true);
            ProcessObjectConfigs(true, true);
            ProcessObjectConfigs(false, true);

            onDoorOpen?.Invoke();
        }

        // 3) Single "CloseDoor" method handling normal & forced closes.
        public void CloseDoor(bool force)
        {
            if (doorEventChannel == null)
            {
                DoorLogger.LogError($"[{name}] DoorEventChannel is not assigned for the door: '{gameObject.name}'. Cannot close.", this);
                return;
            }

            if (!force && IsLeverControlled)
            {
                DoorLogger.LogWarning("This door is lever-controlled and cannot be closed directly.", this);
                return;
            }

            if (!IsOpen)
            {
                //DoorLogger.LogWarning("Door is already closed.", this);
                return;
            }

            StartCoroutine(CloseDoorRoutine(force));
        }

        // 4) unified coroutine for close logic
        private IEnumerator CloseDoorRoutine(bool force)
        {
            float actualDelay = force ? 0f : closeDelay;
            yield return new WaitForSeconds(actualDelay);

            IsOpen = false;

            if (AnimationCoroutine != null)
            {
                StopCoroutine(AnimationCoroutine);
            }

            float animationDuration = 1f / Speed;
            if (CloseSounds.Count > 0)
            {
                float soundDuration = PlayRandomSound(CloseSounds);
                if (matchSpeedToSound) animationDuration = soundDuration;
            }

            if (disableColliderWhileMoving && doorCollider != null)
            {
                doorCollider.enabled = false;
            }
            if (disablenavmeshobstacle && navMeshObstacle != null)
            {
                navMeshObstacle.enabled = false;
            }

            if (UseRotation)
            {
                AnimationCoroutine = StartCoroutine(DoRotationClose(animationDuration));
            }
            else
            {
                AnimationCoroutine = StartCoroutine(DoSlidingClose(animationDuration));
            }

            if (isDualDoor && secondDoor != null)
            {
                secondDoor.CloseDoor(force);
            }

            ProcessComponentConfigs(true, false);
            ProcessComponentConfigs(false, false);
            ProcessObjectConfigs(true, false);
            ProcessObjectConfigs(false, false);

            onDoorClose?.Invoke();
        }
        public void ToggleLeverControl(bool leverControlState)
        {
            IsLeverControlled = leverControlState;
        }
        #endregion
        #region Door Actions
        private IEnumerator DoRotationOpen(float ForwardAmount, float duration)
        {
            Quaternion startRotation = pivot.rotation;
            Quaternion endRotation;

            switch (rotationAxis)
            {
                case Axis.X:
                    endRotation = Quaternion.Euler(new Vector3(StartRotation.x + (ForwardAmount >= ForwardDirection ? RotationAmount : -RotationAmount), StartRotation.y, StartRotation.z));
                    break;
                case Axis.Y:
                default:
                    endRotation = Quaternion.Euler(new Vector3(StartRotation.x, StartRotation.y + (ForwardAmount >= ForwardDirection ? RotationAmount : -RotationAmount), StartRotation.z));
                    break;
                case Axis.Z:
                    endRotation = Quaternion.Euler(new Vector3(StartRotation.x, StartRotation.y, StartRotation.z + (ForwardAmount >= ForwardDirection ? RotationAmount : -RotationAmount)));
                    break;
            }

            IsOpen = true;

            float time = 0;
            while (time < 1)
            {
                float curveValue = doorAnimationOnOpen.Evaluate(time);
                pivot.rotation = Quaternion.Slerp(startRotation, endRotation, curveValue);
                yield return null;
                time += Time.deltaTime / duration;
            }

            RestoreCollidersAndNavMesh();
        }


        private IEnumerator DoSlidingOpen(float duration)
        {
            Vector3 endPosition = StartPosition + SlideAmount * SlideDirection.normalized;
            Vector3 startPosition = transform.position;

            float time = 0;
            IsOpen = true;

            // Determine duration based on sound length if matchSpeedToSound is enabled
            if (matchSpeedToSound && OpenSounds.Count > 0)
            {
                float soundDuration = PlayRandomSound(OpenSounds);
                duration = soundDuration;

                if (rotateWhileSliding && !allowCustomRotationSpeed)
                {
                    rotationSpeedDuringSlide = 360f / duration; // Auto-calculate rotation speed
                }
            }

            // Sliding animation with optional rotation
            while (time < 1)
            {
                float curveValue = doorAnimationOnOpen.Evaluate(time);
                transform.position = Vector3.Lerp(startPosition, endPosition, curveValue);

                if (rotateWhileSliding)
                {
                    transform.Rotate(rotationAxisDuringSlide, rotationSpeedDuringSlide * Time.deltaTime, Space.Self);
                }

                yield return null;
                time += Time.deltaTime / duration;
            }

            transform.position = endPosition;

            // Re-enable components
            if (disableColliderWhileMoving && doorCollider != null)
                doorCollider.enabled = true;

            if (disablenavmeshobstacle && navMeshObstacle != null)
                navMeshObstacle.enabled = true;
        }



        private IEnumerator DoRotationClose(float duration)
        {
            Quaternion startRotation = pivot.rotation;
            Quaternion endRotation = Quaternion.Euler(StartRotation);

            IsOpen = false;

            float time = 0;
            while (time < 1)
            {
                float curveValue = doorAnimationOnClose.Evaluate(time);
                pivot.rotation = Quaternion.Slerp(startRotation, endRotation, curveValue);
                yield return null;
                time += Time.deltaTime / duration;
            }

            RestoreCollidersAndNavMesh();
        }

        private void RestoreCollidersAndNavMesh()
        {
            if (disableColliderWhileMoving && doorCollider != null)
            {
                doorCollider.enabled = true;
            }

            if (disablenavmeshobstacle && navMeshObstacle != null)
            {
                navMeshObstacle.enabled = true;
            }
        }

        private IEnumerator DoSlidingClose(float duration)
        {
            Vector3 endPosition = StartPosition;
            Vector3 startPosition = transform.position;

            float time = 0;
            IsOpen = false;

            // Determine duration based on sound length if matchSpeedToSound is enabled
            if (matchSpeedToSound && CloseSounds.Count > 0)
            {
                float soundDuration = PlayRandomSound(CloseSounds);
                duration = soundDuration;

                if (rotateWhileSliding && !allowCustomRotationSpeed)
                {
                    rotationSpeedDuringSlide = 360f / duration; // Auto-calculate rotation speed
                }
            }

            // Sliding animation with optional rotation
            while (time < 1)
            {
                float curveValue = doorAnimationOnClose.Evaluate(time);
                transform.position = Vector3.Lerp(startPosition, endPosition, curveValue);

                if (rotateWhileSliding)
                {
                    float rotationDirection = invertRotationOnClose ? -1 : 1;
                    transform.Rotate(rotationAxisDuringSlide, rotationSpeedDuringSlide * rotationDirection * Time.deltaTime, Space.Self);
                }

                yield return null;
                time += Time.deltaTime / duration;
            }

            transform.position = endPosition;

            // Re-enable components
            if (disableColliderWhileMoving && doorCollider != null)
                doorCollider.enabled = true;

            if (disablenavmeshobstacle && navMeshObstacle != null)
                navMeshObstacle.enabled = true;
        }

        #endregion
        #region Audio
        private float PlayRandomSound(List<AudioClip> clips)
        {
            if (clips == null)
            {
                DoorLogger.LogWarning($"{gameObject.name} - PlayRandomSound: The audio clip list is null.");
                return 1f / Speed;
            }

            if (clips.Count == 0)
            {
                DoorLogger.LogWarning($"{gameObject.name} - PlayRandomSound: The audio clip list is empty.");
                return 1f / Speed;
            }

            if (audioSource == null)
            {
                DoorLogger.LogWarning($"{gameObject.name} - PlayRandomSound: The AudioSource is null.");
                return 1f / Speed;
            }

            int index = Random.Range(0, clips.Count);
            AudioClip clip = clips[index];

            float chosenPitch = useRandomPitch
            ? Random.Range(randomPitchMin, randomPitchMax)
            : pitch;

            audioSource.pitch = chosenPitch;

            if (clip == null)
            {
                DoorLogger.LogWarning($"{gameObject.name} - PlayRandomSound: The AudioClip at index {index} is null.");
                return 1f / Speed;
            }

            audioSource.PlayOneShot(clip);
            return clip.length / chosenPitch;
        }


        private void audiosourceVolume()
        {
            if (audioSource != null)
            {
                audioSource.volume = volume;
            }

            // Validate Knob manual bounds when fields are updated
            if (door != null && !Application.isPlaying)
            {
                UpdateDoorBounds();
                ValidateManualBounds(); // Validate the manual bounds when fields are updated

                foreach (var attachedObject in attachedObjects)
                {
                    if (attachedObject != null && attachedObject.targetObject != null)
                    {
                        UpdateObjectPosition(attachedObject);
                    }
                }
            }
        }
        #endregion
        #region Gizmos
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;

            // Adjust the gizmo position for Y offset
            Vector3 gizmoPosition = transform.position + Vector3.up * gizmoYOffset;

            // Apply X rotation manually
            Quaternion xRotation = Quaternion.Euler(gizmoXRotation, 0, 0);
            Vector3 adjustedSlideDirection = xRotation * SlideDirection.normalized * SlideAmount;

            // Apply Y rotation manually
            Quaternion yRotation = Quaternion.Euler(0, StartRotation.y, 0);
            adjustedSlideDirection = yRotation * adjustedSlideDirection;

            // Apply Z rotation manually (we�ll rotate the final direction vector around Z axis)
            float zRadians = gizmoZRotation * Mathf.Deg2Rad;
            float cosZ = Mathf.Cos(zRadians);
            float sinZ = Mathf.Sin(zRadians);
            Vector3 finalDirection = new Vector3(
                adjustedSlideDirection.x * cosZ - adjustedSlideDirection.y * sinZ,
                adjustedSlideDirection.x * sinZ + adjustedSlideDirection.y * cosZ,
                adjustedSlideDirection.z
            );

            // Show movement direction for sliding doors
            if (!UseRotation)
            {
                Gizmos.DrawLine(gizmoPosition, gizmoPosition + finalDirection);
                Gizmos.DrawSphere(gizmoPosition + finalDirection, 0.1f);
            }

            // Show rotation arc for rotating doors based on ForwardDirection
            if (UseRotation)
            {
                Vector3 startRotation = Vector3.forward;

                // Apply the same rotation adjustments to the start rotation
                startRotation = xRotation * startRotation;
                startRotation = yRotation * startRotation;

                // Apply Z rotation directly as we did for the slide direction
                startRotation = new Vector3(
                    startRotation.x * cosZ - startRotation.y * sinZ,
                    startRotation.x * sinZ + startRotation.y * cosZ,
                    startRotation.z
                );

                Vector3 endRotationClockwise = Quaternion.Euler(0, RotationAmount, 0) * startRotation;
                Vector3 endRotationCounterClockwise = Quaternion.Euler(0, -RotationAmount, 0) * startRotation;

                // Draw only the relevant rotation arc based on ForwardDirection
                int segments = 20;
                if (ForwardDirection > 0)
                {
                    // Draw rotation arc away from the player
                    Gizmos.DrawLine(gizmoPosition, gizmoPosition + startRotation);
                    Gizmos.DrawLine(gizmoPosition, gizmoPosition + endRotationClockwise);
                    DrawArc(gizmoPosition, startRotation, RotationAmount, segments);
                }
                else if (ForwardDirection < 0)
                {
                    // Draw rotation arc towards the player
                    Gizmos.DrawLine(gizmoPosition, gizmoPosition + startRotation);
                    Gizmos.DrawLine(gizmoPosition, gizmoPosition + endRotationCounterClockwise);
                    DrawArc(gizmoPosition, startRotation, -RotationAmount, segments);
                }
                else
                {
                    // Draw both arcs if ForwardDirection == 0
                    Gizmos.DrawLine(gizmoPosition, gizmoPosition + startRotation);
                    Gizmos.DrawLine(gizmoPosition, gizmoPosition + endRotationClockwise);
                    Gizmos.DrawLine(gizmoPosition, gizmoPosition + endRotationCounterClockwise);
                    DrawArc(gizmoPosition, startRotation, RotationAmount, segments);
                    DrawArc(gizmoPosition, startRotation, -RotationAmount, segments);
                }
            }

            if (door == null || (isRuntime && disableAtRuntime)) return;

            // Select bounds to visualize
            Vector3 minBounds = autoMinBounds;
            Vector3 maxBounds = autoMaxBounds;

            // Add leeway to the X-axis for visualization
            minBounds.x -= xLeeway;
            maxBounds.x += xLeeway;

            // Draw the bounds as a box
            Gizmos.color = BoundsGizmoColor;
            Vector3 center = (minBounds + maxBounds) / 2;
            Vector3 size = maxBounds - minBounds;
            Gizmos.DrawWireCube(center, size);
        }

        // Helper method to draw an arc for rotation visualization
        private void DrawArc(Vector3 center, Vector3 startDirection, float angle, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float angleA = i * (angle / segments);
                float angleB = (i + 1) * (angle / segments);

                Vector3 pointA = Quaternion.Euler(0, angleA, 0) * startDirection;
                Vector3 pointB = Quaternion.Euler(0, angleB, 0) * startDirection;
                Gizmos.DrawLine(center + pointA, center + pointB);
            }
        }

        #endregion
    }
}

