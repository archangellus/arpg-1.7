using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneAudioManager))]
public class SceneAudioManagerEditor : Editor
{
    private SerializedProperty audioEntriesProp;

    private void OnEnable()
    {
        audioEntriesProp = serializedObject.FindProperty("audioEntries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            MonoScript script = MonoScript.FromMonoBehaviour((SceneAudioManager)target);
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        }

        EditorGUILayout.Space();

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Preview mode: Exact. Uses SceneAudioManager runtime settings.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("Preview mode: Random clip from the list. Exact volume/pitch/random pitch preview requires Play Mode.", MessageType.Info);
        }

        EditorGUILayout.Space();

        if (audioEntriesProp == null)
        {
            EditorGUILayout.HelpBox("Could not find audioEntries.", MessageType.Error);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.PropertyField(audioEntriesProp, new GUIContent("Audio Entries"), true);

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomPropertyDrawer(typeof(SceneAudioManager.AudioEntry))]
public class SceneAudioEntryDrawer : PropertyDrawer
{
    private const float ButtonWidth = 52f;
    private const float ButtonSpacing = 4f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        if (!property.isExpanded)
            return line;

        SerializedProperty clipsProp = property.FindPropertyRelative("clips");
        bool useRandomPitch = property.FindPropertyRelative("useRandomPitch").boolValue;

        float totalHeight = line;
        totalHeight += spacing + line; // id
        totalHeight += spacing + EditorGUI.GetPropertyHeight(clipsProp, true);
        totalHeight += spacing + line; // volume
        totalHeight += spacing + line; // pitch
        totalHeight += spacing + line; // useRandomPitch

        if (useRandomPitch)
        {
            totalHeight += spacing + line;
            totalHeight += spacing + line;
        }

        return totalHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty idProp = property.FindPropertyRelative("id");
        SerializedProperty clipsProp = property.FindPropertyRelative("clips");
        SerializedProperty volumeProp = property.FindPropertyRelative("volume");
        SerializedProperty pitchProp = property.FindPropertyRelative("pitch");
        SerializedProperty useRandomPitchProp = property.FindPropertyRelative("useRandomPitch");
        SerializedProperty randomPitchMinProp = property.FindPropertyRelative("randomPitchMin");
        SerializedProperty randomPitchMaxProp = property.FindPropertyRelative("randomPitchMax");

        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        Rect row = new Rect(position.x, position.y, position.width, line);

        string id = string.IsNullOrWhiteSpace(idProp.stringValue) ? label.text : idProp.stringValue;
        string clipSummary = GetClipSummary(clipsProp);
        string headerText = $"{id}  ({clipSummary})";

        Rect foldoutRect = new Rect(
            row.x,
            row.y,
            row.width - ((ButtonWidth * 2f) + ButtonSpacing + 8f),
            row.height);

        Rect playRect = new Rect(
            row.xMax - ((ButtonWidth * 2f) + ButtonSpacing),
            row.y,
            ButtonWidth,
            row.height);

        Rect stopRect = new Rect(
            row.xMax - ButtonWidth,
            row.y,
            ButtonWidth,
            row.height);

        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, headerText, true);

        AudioClip previewClip = GetPreviewClip(property);

        using (new EditorGUI.DisabledScope(previewClip == null))
        {
            if (GUI.Button(playRect, "Play"))
            {
                if (Application.isPlaying)
                {
                    SceneAudioManager manager = FindManager(property);
                    if (manager != null)
                    {
                        manager.PlayByName(idProp.stringValue);
                    }
                    else
                    {
                        Debug.LogWarning("[SceneAudioManagerEditor] No active SceneAudioManager found in the scene.");
                    }
                }
                else
                {
                    AudioPreviewUtility.PlayClip(previewClip);
                }
            }
        }

        if (GUI.Button(stopRect, "Stop"))
        {
            if (Application.isPlaying)
            {
                SceneAudioManager manager = FindManager(property);
                if (manager != null)
                {
                    manager.StopAudio();
                }
            }
            else
            {
                AudioPreviewUtility.StopAllClips();
            }
        }

        if (!property.isExpanded)
            return;

        EditorGUI.indentLevel++;

        row.y += line + spacing;
        EditorGUI.PropertyField(row, idProp);

        row.y += line + spacing;
        float clipsHeight = EditorGUI.GetPropertyHeight(clipsProp, true);
        Rect clipsRect = new Rect(row.x, row.y, row.width, clipsHeight);
        EditorGUI.PropertyField(clipsRect, clipsProp, new GUIContent("Clips"), true);
        row.y += clipsHeight - line;

        row.y += line + spacing;
        EditorGUI.PropertyField(row, volumeProp);

        row.y += line + spacing;
        EditorGUI.PropertyField(row, pitchProp);

        row.y += line + spacing;
        EditorGUI.PropertyField(row, useRandomPitchProp);

        if (useRandomPitchProp.boolValue)
        {
            row.y += line + spacing;
            EditorGUI.PropertyField(row, randomPitchMinProp);

            row.y += line + spacing;
            EditorGUI.PropertyField(row, randomPitchMaxProp);
        }

        EditorGUI.indentLevel--;
    }

    private static string GetClipSummary(SerializedProperty clipsProp)
    {
        int validCount = 0;
        string firstName = null;

        for (int i = 0; i < clipsProp.arraySize; i++)
        {
            SerializedProperty element = clipsProp.GetArrayElementAtIndex(i);
            AudioClip clip = element.objectReferenceValue as AudioClip;
            if (clip == null)
                continue;

            validCount++;
            if (firstName == null)
            {
                firstName = clip.name;
            }
        }

        if (validCount == 0)
            return "No Clips";

        if (validCount == 1)
            return firstName;

        return $"{validCount} clips";
    }

    private static AudioClip GetPreviewClip(SerializedProperty property)
    {
        UnityEngine.Object targetObject = property.serializedObject.targetObject;
        if (targetObject is SceneAudioManager manager)
        {
            string path = property.propertyPath;
            string[] parts = path.Split('[', ']');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int index))
            {
                FieldInfo field = typeof(SceneAudioManager).GetField("audioEntries", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var entries = field.GetValue(manager) as System.Collections.IList;
                    if (entries != null && index >= 0 && index < entries.Count)
                    {
                        if (entries[index] is SceneAudioManager.AudioEntry entry)
                        {
                            entry.MigrateLegacyClipIfNeeded();
                            return entry.GetPreviewClip();
                        }
                    }
                }
            }
        }

        SerializedProperty clipsProp = property.FindPropertyRelative("clips");
        for (int i = 0; i < clipsProp.arraySize; i++)
        {
            AudioClip clip = clipsProp.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
            if (clip != null)
                return clip;
        }

        return null;
    }

    private static SceneAudioManager FindManager(SerializedProperty property)
    {
        UnityEngine.Object targetObject = property.serializedObject.targetObject;
        if (targetObject is SceneAudioManager directManager)
            return directManager;

#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindAnyObjectByType<SceneAudioManager>();
#else
        return UnityEngine.Object.FindObjectOfType<SceneAudioManager>();
#endif
    }

    private static class AudioPreviewUtility
    {
        private static readonly Type AudioUtilType =
            typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

#if UNITY_2020_2_OR_NEWER
        private const string PlayMethodName = "PlayPreviewClip";
        private const string StopMethodName = "StopAllPreviewClips";
#else
        private const string PlayMethodName = "PlayClip";
        private const string StopMethodName = "StopAllClips";
#endif

        private static readonly MethodInfo PlayMethod =
            AudioUtilType?.GetMethod(
                PlayMethodName,
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null)
            ?? AudioUtilType?.GetMethod(
                PlayMethodName,
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(AudioClip) },
                null);

        private static readonly MethodInfo StopMethod =
            AudioUtilType?.GetMethod(StopMethodName, BindingFlags.Static | BindingFlags.Public);

        public static void PlayClip(AudioClip clip)
        {
            if (clip == null)
                return;

            if (AudioUtilType == null || PlayMethod == null)
            {
                Debug.LogWarning("[SceneAudioManagerEditor] UnityEditor.AudioUtil preview method was not found.");
                return;
            }

            StopAllClips();

            ParameterInfo[] parameters = PlayMethod.GetParameters();
            if (parameters.Length == 3)
            {
                PlayMethod.Invoke(null, new object[] { clip, 0, false });
            }
            else if (parameters.Length == 1)
            {
                PlayMethod.Invoke(null, new object[] { clip });
            }
        }

        public static void StopAllClips()
        {
            if (StopMethod == null)
                return;

            StopMethod.Invoke(null, null);
        }
    }
}
