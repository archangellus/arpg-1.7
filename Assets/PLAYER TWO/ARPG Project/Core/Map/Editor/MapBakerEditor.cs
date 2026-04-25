using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PLAYERTWO.ARPGProject
{
    [CustomEditor(typeof(MapBaker))]
    public class MapBakerEditor : Editor
    {
        private MapBaker m_baker;
        private string m_path;

        private void OnEnable() => m_baker = (MapBaker)target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "The generated texture will be saved"
                    + "in the same folder of your scene, encoded to PNG, "
                    + "using the Capture Settings.",
                MessageType.Info
            );

            if (GUILayout.Button("Generate Map Texture"))
            {
                UpdatePath();
                CaptureShot();
            }
        }

        private void UpdatePath()
        {
            m_path = SceneManager.GetActiveScene().path;
            m_path = m_path.Replace(".unity", "");

            if (!Directory.Exists(m_path))
            {
                Directory.CreateDirectory(m_path);
            }
        }

        private void CaptureShot()
        {
            var mapCamera = new GameObject().AddComponent<Camera>();
            var originalLightCount = QualitySettings.pixelLightCount;

            QualitySettings.pixelLightCount = 9999;

            mapCamera.orthographic = true;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = m_baker.backgroundColor;
            mapCamera.cullingMask = m_baker.cullingMask;
            mapCamera.orthographicSize = m_baker.map.length * 0.5f;
            mapCamera.transform.eulerAngles = new Vector3(90, 0, 0);
            mapCamera.transform.position =
                m_baker.map.center + 0.5f * m_baker.map.height * Vector3.up;

            var cameraRender = new RenderTexture(m_baker.resolution.x, m_baker.resolution.y, 32);
            mapCamera.targetTexture = cameraRender;

            var renderTexture = RenderTexture.active;
            RenderTexture.active = mapCamera.targetTexture;

            mapCamera.Render();

            var targetTexture = mapCamera.targetTexture;

            Texture2D texture =
                new(targetTexture.width, targetTexture.height, TextureFormat.RGB24, false);

            texture.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
            texture.Apply();

            RenderTexture.active = renderTexture;

            mapCamera.targetTexture = null;

            var path = $"{m_path}/{m_baker.fileName}.png";

            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(path);
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.SaveAndReimport();

            m_baker.map.texture = (Texture)AssetDatabase.LoadAssetAtPath(path, typeof(Texture));

            Debug.Log($"Map image saved in {m_path}!");

            QualitySettings.pixelLightCount = originalLightCount;

            DestroyImmediate(mapCamera.gameObject);
            DestroyImmediate(renderTexture);
            DestroyImmediate(targetTexture);
            DestroyImmediate(cameraRender);
        }
    }
}
