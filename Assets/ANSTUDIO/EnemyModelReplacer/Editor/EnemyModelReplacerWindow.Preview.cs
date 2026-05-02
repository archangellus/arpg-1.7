using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class EnemyModelReplacerWindow
    {
        private PreviewRenderUtility previewRenderUtility;
        private GameObject previewInstance;
        private GameObject previewSourcePrefab;
        private Bounds previewFramingBounds;
        private bool previewFramingBoundsValid;

        private void DrawPrefabPreviewPanel(float height)
        {
            var rect = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.05f, 0.055f, 0.065f, 1f));

            if (newModelPrefab == null)
            {
                var icon = FindEditorIcon("Prefab Icon");
                if (icon != null)
                    UnityEngine.GUI.DrawTexture(new Rect(rect.center.x - 24f, rect.center.y - 42f, 48f, 48f), icon, ScaleMode.ScaleToFit, true);

                EditorGUI.LabelField(
                    new Rect(rect.x + 12f, rect.center.y + 10f, rect.width - 24f, 22f),
                    "Assign a prefab in Setup to preview it here.",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EnsurePrefabPreviewInstance();
            HandlePrefabPreviewInput(rect);
            RenderPrefabPreview(rect);

            var overlay = new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 22f);
            EditorGUI.DropShadowLabel(overlay, $"{newModelPrefab.name}  |  Right Mouse Drag: Rotate  |  Yaw: {previewYaw:0}°");
        }

        private void HandlePrefabPreviewInput(Rect rect)
        {
            var currentEvent = Event.current;
            if (!rect.Contains(currentEvent.mousePosition))
                return;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1)
            {
                GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 1)
            {
                previewYaw -= currentEvent.delta.x * 0.6f;
                previewYaw = Mathf.Repeat(previewYaw, 360f);
                Repaint();
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 1)
            {
                GUIUtility.hotControl = 0;
                currentEvent.Use();
            }
        }

        private void EnsurePrefabPreviewInstance()
        {
            if (previewRenderUtility == null)
            {
                previewRenderUtility = new PreviewRenderUtility(true)
                {
                    cameraFieldOfView = 30f
                };

                previewRenderUtility.camera.nearClipPlane = 0.01f;
                previewRenderUtility.camera.farClipPlane = 1000f;

                if (previewRenderUtility.lights != null && previewRenderUtility.lights.Length >= 2)
                {
                    previewRenderUtility.lights[0].intensity = 1.25f;
                    previewRenderUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
                    previewRenderUtility.lights[1].intensity = 1.0f;
                    previewRenderUtility.lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 177f);
                }
            }

            if (previewSourcePrefab == newModelPrefab && previewInstance != null)
                return;

            DestroyPrefabPreviewInstance();

            previewSourcePrefab = newModelPrefab;
            if (previewSourcePrefab == null)
                return;

            previewInstance = Instantiate(previewSourcePrefab);
            previewInstance.hideFlags = HideFlags.HideAndDontSave;
            SetHideFlagsRecursively(previewInstance, HideFlags.HideAndDontSave);
            previewFramingBoundsValid = false;
            previewRenderUtility.AddSingleGO(previewInstance);
        }

        private void RenderPrefabPreview(Rect rect)
        {
            if (Event.current.type != EventType.Repaint || previewRenderUtility == null || previewInstance == null)
                return;

            var bounds = GetStablePreviewFramingBounds();
            var center = bounds.center;
            var radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            var distance = Mathf.Max(2f, radius * 2.35f);
            var previewRotation = Quaternion.Euler(0f, previewYaw, 0f);

            previewInstance.transform.localScale = Vector3.one;
            previewInstance.transform.rotation = previewRotation;
            previewInstance.transform.position = center - previewRotation * center;

            var camera = previewRenderUtility.camera;
            camera.transform.position = center + new Vector3(0f, radius * 0.25f, -distance);
            camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, Vector3.up);
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0.05f, 0.055f, 0.065f, 1f);

            previewRenderUtility.BeginPreview(rect, GUIStyle.none);
            previewRenderUtility.Render(true);
            var texture = previewRenderUtility.EndPreview();
            UnityEngine.GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        private Bounds GetStablePreviewFramingBounds()
        {
            if (previewFramingBoundsValid)
                return previewFramingBounds;

            var previousPosition = previewInstance.transform.position;
            var previousRotation = previewInstance.transform.rotation;
            var previousScale = previewInstance.transform.localScale;

            previewInstance.transform.position = Vector3.zero;
            previewInstance.transform.rotation = Quaternion.identity;
            previewInstance.transform.localScale = Vector3.one;

            previewFramingBounds = CalculatePreviewBounds(previewInstance);
            previewFramingBoundsValid = true;

            previewInstance.transform.position = previousPosition;
            previewInstance.transform.rotation = previousRotation;
            previewInstance.transform.localScale = previousScale;

            return previewFramingBounds;
        }

        private Bounds CalculatePreviewBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                return bounds;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            if (transforms.Length > 0)
            {
                var bounds = new Bounds(transforms[0].position, Vector3.one);
                for (var i = 1; i < transforms.Length; i++)
                    bounds.Encapsulate(transforms[i].position);

                return bounds;
            }

            return new Bounds(Vector3.zero, Vector3.one);
        }

        private void InvalidatePrefabPreview()
        {
            DestroyPrefabPreviewInstance();
            previewSourcePrefab = null;
        }

        private void DestroyPrefabPreviewInstance()
        {
            if (previewInstance == null)
                return;

            DestroyImmediate(previewInstance);
            previewInstance = null;
            previewFramingBoundsValid = false;
        }

        private void CleanupPrefabPreview()
        {
            DestroyPrefabPreviewInstance();
            previewSourcePrefab = null;

            if (previewRenderUtility == null)
                return;

            previewRenderUtility.Cleanup();
            previewRenderUtility = null;
        }

        private static void SetHideFlagsRecursively(GameObject root, HideFlags hideFlags)
        {
            if (root == null)
                return;

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = hideFlags;
                var components = transform.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component != null)
                        component.hideFlags = hideFlags;
                }
            }
        }
    }
}
