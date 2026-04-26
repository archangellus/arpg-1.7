using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PLAYERTWO.ARPGProject.Plugins.MouseCameraRotate
{
    [DisallowMultipleComponent]
    public sealed class MouseCameraRotateRunner : MonoBehaviour
    {
        [Header("Action Names (preferred)")]
        [Tooltip("Button action to hold while rotating (default: 'Rotate').")]
        public string rotateActionName = "Rotate";

        [Tooltip("Vector2 action providing pointer delta (default: 'Pointer Delta').")]
        public string pointerDeltaActionName = "Pointer Delta";

        [Header("Tuning")]
        [Tooltip("Degrees added per pixel of horizontal pointer movement (per frame).")]
        public float sensitivity = 0.2f;

        [Tooltip("Invert horizontal rotation input.")]
        public bool invertX = false;

        [Tooltip("Cap degrees applied each frame (0 = uncapped).")]
        public float maxDegreesPerFrame = 0f;

        [Header("Debug")]
        [Tooltip("Per-instance toggle (requires kEnableLogs = true).")]
        public bool debugLogs = false; // default: off

        // Global kill-switch for all logs in this runner.
        private const bool kEnableLogs = false; // set true to allow logs

        private InputAction _rotateAction;
        private InputAction _deltaAction;
        private bool _rotating;

        private System.Action<InputAction.CallbackContext> _onRotatePerformed;
        private System.Action<InputAction.CallbackContext> _onRotateCanceled;

        private PLAYERTWO.ARPGProject.EntityCamera _camera;

        private static readonly string[] FallbackRotateNames = { "Rotate", "Camera Rotate", "RotateHold" };
        private static readonly string[] FallbackDeltaNames  = { "Pointer Delta", "Look", "Mouse Delta", "Delta" };

        private bool IsLogEnabled => kEnableLogs && debugLogs;

        private void Log(string msg)
        {
            if (IsLogEnabled) Debug.Log(msg);
        }

        private void LogWarning(string msg)
        {
            if (IsLogEnabled) Debug.LogWarning(msg);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryBind();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnhookActions();
        }

        private void OnDestroy()
        {
            UnhookActions();
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            TryBind();
        }

        private void TryBind()
        {
#if UNITY_2023_1_OR_NEWER
            _camera = Object.FindAnyObjectByType<PLAYERTWO.ARPGProject.EntityCamera>(FindObjectsInactive.Exclude);
            if (_camera == null)
                _camera = Object.FindFirstObjectByType<PLAYERTWO.ARPGProject.EntityCamera>(FindObjectsInactive.Include);
#else
            _camera = Object.FindObjectOfType<PLAYERTWO.ARPGProject.EntityCamera>();
#endif
            if (_camera == null)
            {
                LogWarning("[MouseCameraRotate] EntityCamera not found in scene.");
                UnhookActions();
                return;
            }
            if (_camera.actions == null)
            {
                LogWarning("[MouseCameraRotate] EntityCamera.actions is null.");
                UnhookActions();
                return;
            }

            var asset = _camera.actions;

            // Clear previous hooks before rebinding.
            UnhookActions();

            _rotateAction = FindAction(asset, rotateActionName, FallbackRotateNames)
                            ?? AutoDetectRotate(asset);

            _deltaAction  = FindAction(asset, pointerDeltaActionName, FallbackDeltaNames)
                            ?? AutoDetectPointerDelta(asset);

            Log($"[MouseCameraRotate] Bind results → Rotate: {(_rotateAction != null ? _rotateAction.name : "NOT FOUND")} | Delta: {(_deltaAction != null ? _deltaAction.name : "NOT FOUND")}");

            if (_rotateAction != null)
            {
                _rotateAction.Enable();
                _onRotatePerformed = _ =>
                {
                    _rotating = true;
                    Log("[MouseCameraRotate] Rotate action: performed (holding).");
                };
                _onRotateCanceled  = _ =>
                {
                    _rotating = false;
                    Log("[MouseCameraRotate] Rotate action: canceled (released).");
                };
                _rotateAction.performed += _onRotatePerformed;
                _rotateAction.canceled  += _onRotateCanceled;
            }
            else
            {
                LogWarning("[MouseCameraRotate] Rotate action not found. Set 'rotateActionName' to a Button action (e.g., <Mouse>/middleButton with Hold).");
            }

            if (_deltaAction != null)
            {
                _deltaAction.Enable();
            }
            else
            {
                LogWarning("[MouseCameraRotate] Pointer Delta action not found. Set 'pointerDeltaActionName' to a Vector2 action bound to <Pointer>/delta.");
            }
        }

        private static InputAction FindAction(InputActionAsset asset, string preferred, string[] fallbacks)
        {
            if (asset == null) return null;

            if (!string.IsNullOrWhiteSpace(preferred))
            {
                var a = asset.FindAction(preferred, throwIfNotFound: false);
                if (a != null) return a;
            }

            if (fallbacks != null)
            {
                for (int i = 0; i < fallbacks.Length; i++)
                {
                    var name = fallbacks[i];
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var a = asset.FindAction(name, throwIfNotFound: false);
                    if (a != null) return a;
                }
            }
            return null;
        }

        private static InputAction AutoDetectRotate(InputActionAsset asset)
        {
            foreach (var map in asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    bool hasMouseButtonBinding = action.bindings.Any(b =>
                        b.path != null &&
                        (b.path.Contains("<Mouse>/middleButton") || b.path.Contains("<Mouse>/rightButton")));

                    bool likelyButton = string.IsNullOrEmpty(action.expectedControlType) ||
                                        action.expectedControlType == "Button";

                    if (likelyButton && hasMouseButtonBinding)
                        return action;
                }
            }
            return null;
        }

        private static InputAction AutoDetectPointerDelta(InputActionAsset asset)
        {
            foreach (var map in asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    bool likelyVector2 = string.IsNullOrEmpty(action.expectedControlType) ||
                                         action.expectedControlType == "Vector2";

                    bool hasDeltaBinding = action.bindings.Any(b =>
                        b.path != null &&
                        (b.path.Contains("<Pointer>/delta") || b.path.EndsWith("/delta")));

                    if (likelyVector2 && hasDeltaBinding)
                        return action;
                }
            }
            return null;
        }

        private void UnhookActions()
        {
            if (_rotateAction != null)
            {
                if (_onRotatePerformed != null) _rotateAction.performed -= _onRotatePerformed;
                if (_onRotateCanceled  != null) _rotateAction.canceled  -= _onRotateCanceled;
            }

            _onRotatePerformed = null;
            _onRotateCanceled  = null;
            _rotateAction      = null;
            _deltaAction       = null;
            _rotating          = false;
        }

        private void LateUpdate()
        {
            if (_camera == null || _rotateAction == null || _deltaAction == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (!_rotating) return;

            Vector2 delta = _deltaAction.ReadValue<Vector2>();
            float dx = invertX ? -delta.x : delta.x;
            if (Mathf.Approximately(dx, 0f)) return;

            float deg = dx * sensitivity;
            if (maxDegreesPerFrame > 0f)
                deg = Mathf.Clamp(deg, -maxDegreesPerFrame, maxDegreesPerFrame);

            Log($"[MouseCameraRotate] Publishing delta: {deg:0.###}");
            EventBus.Publish(EventBus.MouseCameraRotateDelta, deg);
        }
    }
}
