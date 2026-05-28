using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace BLINK.WorldClusters
{
    [CustomEditor(typeof(Cluster))]
    public class ClusterEditor : Editor
    {
        private Cluster _ref;
        private GUISkin _skin;
        private WorldClustersEditorData _editorData;

        private string currentSearch;

        private void OnEnable()
        {
            _ref = (Cluster)target;
            _skin = Resources.Load<GUISkin>("EditorData/WorldClustersEditorSkin");
            _editorData = Resources.Load<WorldClustersEditorData>("EditorData/WorldClustersEditorData");
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        private void OnSelectionChanged()
        {
            if (_ref != null && Selection.activeGameObject == _ref.gameObject)
            {
                Repaint();
            }
        }

        private void OnHierarchyChanged()
        {
            if (_ref == null) return;
            Repaint();
        }

        private void OnUndoRedoPerformed()
        {
            if (_ref == null) return;
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            if (_skin == null) return;

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((Cluster)target), typeof(Cluster), false);
            GUI.enabled = true;
            EditorGUI.BeginChangeCheck();

            GUIStyle titleStyle = GetStyle("title");

            GUILayout.Space(10);
            EditorGUILayout.BeginVertical();
            GUILayout.Label("CLUSTER GROUP: " + _ref.clusterGroups.Count, titleStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(15);

            if (GUILayout.Button("+ ADD", _skin.GetStyle(_editorData.addButtonStyle), GUILayout.Height(30)))
            {
                Undo.RecordObject(_ref, "Add Cluster Group");
                _ref.clusterGroups.Add(new ClusterDATA());
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);

            if (_ref.clusterGroups.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Collapse All", _skin.GetStyle(_editorData.buttonOffStyle), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    foreach (var clusterGroup in _ref.clusterGroups)
                    {
                        foreach (var entry in clusterGroup.Entries)
                        {
                            entry.show = false;
                        }

                        clusterGroup.show = false;
                    }
                }

                GUILayout.Space(10);
                if (GUILayout.Button("Expand All", _skin.GetStyle(_editorData.buttonOffStyle), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    foreach (var clusterGroup in _ref.clusterGroups)
                    {
                        foreach (var entry in clusterGroup.Entries)
                        {
                            entry.show = true;
                        }

                        clusterGroup.show = true;
                    }
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("SEARCH:", GetStyle("text2"), GUILayout.ExpandWidth(false), GUILayout.MaxWidth(75));
                currentSearch = EditorGUILayout.TextField(currentSearch);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(20);
            }

            var clusterGroupList = serializedObject.FindProperty("clusterGroups");
            _ref.clusterGroups = GetTargetObjectOfProperty(clusterGroupList) as List<ClusterDATA>;

            if (_ref.clusterGroups != null)
            {
                for (var cGroup = 0; cGroup < _ref.clusterGroups.Count; cGroup++)
                {
                    if (!ShouldShowClusterGroup(_ref.clusterGroups[cGroup])) continue;

                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button("",
                            _ref.clusterGroups[cGroup].show
                                ? _skin.GetStyle(_editorData.openButtonStyle)
                                : _skin.GetStyle(_editorData.collapseButtonStyle),
                            GUILayout.Width(_editorData.removeButtonSize),
                            GUILayout.Height(_editorData.removeButtonSize)))
                    {
                        _ref.clusterGroups[cGroup].show = !_ref.clusterGroups[cGroup].show;
                        return;
                    }

                    GUILayout.Space(5);
                    _ref.clusterGroups[cGroup].clusterGroupName = EditorGUILayout.TextField(
                        GetGUIContent("", "The name of this cluster group"),
                        _ref.clusterGroups[cGroup].clusterGroupName,
                        _skin.GetStyle("TextField"),
                        GUILayout.ExpandWidth(true),
                        GUILayout.ExpandHeight(true));
                    GUILayout.Space(5);

                    if (GUILayout.Button("", _skin.GetStyle(_editorData.duplicateButtonStyle),
                            GUILayout.Width(_editorData.removeButtonSize),
                            GUILayout.Height(_editorData.removeButtonSize)))
                    {
                        Undo.RecordObject(_ref, "Duplicate Cluster Group");
                        _ref.clusterGroups.Insert(cGroup + 1, CloneClusterGroup(_ref.clusterGroups[cGroup]));
                        return;
                    }

                    if (GUILayout.Button("", _skin.GetStyle(_editorData.removeButtonStyle),
                            GUILayout.Width(_editorData.removeButtonSize),
                            GUILayout.Height(_editorData.removeButtonSize)))
                    {
                        Undo.RecordObject(_ref, "Remove Cluster Group");
                        _ref.clusterGroups.RemoveAt(cGroup);
                        return;
                    }

                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(7.5f);

                    if (!_ref.clusterGroups[cGroup].show) continue;

                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button("ENTRIES",
                            _ref.clusterGroups[cGroup].showEntries
                                ? _skin.GetStyle(_editorData.buttonSelectedStyle)
                                : _skin.GetStyle(_editorData.buttonOffStyle),
                            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    {
                        _ref.clusterGroups[cGroup].showEntries = true;
                        _ref.clusterGroups[cGroup].showOverrides = false;
                    }

                    GUILayout.Space(10);
                    if (GUILayout.Button("OVERRIDES",
                            _ref.clusterGroups[cGroup].showOverrides
                                ? _skin.GetStyle(_editorData.buttonSelectedStyle)
                                : _skin.GetStyle(_editorData.buttonOffStyle),
                            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    {
                        _ref.clusterGroups[cGroup].showEntries = false;
                        _ref.clusterGroups[cGroup].showOverrides = true;
                    }

                    EditorGUILayout.EndHorizontal();

                    if (_ref.clusterGroups[cGroup].showEntries)
                    {
                        DrawGroupEntries(cGroup);
                    }
                    else if (_ref.clusterGroups[cGroup].showOverrides)
                    {
                        DrawEntryOverrides(cGroup);
                    }
                }
            }

            if (!EditorGUI.EndChangeCheck()) return;
            EditorUtility.SetDirty(_ref);
            serializedObject.ApplyModifiedProperties();
        }

        private bool ShouldShowClusterGroup(ClusterDATA clusterGroup)
        {
            if (string.IsNullOrWhiteSpace(currentSearch)) return true;
            string search = currentSearch.ToLowerInvariant();
            if (ContainsSearch(clusterGroup.clusterGroupName, search)) return true;

            foreach (var entry in clusterGroup.Entries)
            {
                if (ContainsSearch(entry.Type.ToString(), search)) return true;
                if (HasMissingReference(entry, search)) return true;

                if (ContainsObjectNames(entry.GameObjectList, search)) return true;
                if (ContainsObjectNames(entry.LightList, search)) return true;
                if (ContainsObjectNames(entry.RendererList, search)) return true;
                if (ContainsObjectNames(entry.ParticleSystemList, search)) return true;
                if (ContainsObjectNames(entry.AudioSourceList, search)) return true;
                if (entry.soundClip != null && ContainsSearch(entry.soundClip.name, search)) return true;
                if (entry.soundOutputMixerGroup != null && ContainsSearch(entry.soundOutputMixerGroup.name, search)) return true;
                if (ContainsSearch(entry.action.enterSoundAction.ToString(), search)) return true;
                if (ContainsSearch(entry.action.exitSoundAction.ToString(), search)) return true;

                var conditions = entry.action.conditions;
                if (conditions == null)
                {
                    if (search.Contains("missing")) return true;
                    continue;
                }

                if (ContainsSearch(conditions.name, search)) return true;
                foreach (var condition in conditions.collisionConditions)
                {
                    if (ContainsSearch(condition.type.ToString(), search)) return true;
                    if (ContainsSearch(condition.requirementType.ToString(), search)) return true;
                    if (ContainsSearch(condition.gameObjectName, search)) return true;
                    if (ContainsSearch(condition.tagName, search)) return true;
                    if (ContainsSearch(LayerMask.LayerToName(condition.layer), search)) return true;
                    if (ContainsSearch(condition.layer.ToString(), search)) return true;
                }
            }

            return false;
        }

        private static bool ContainsSearch(string value, string search)
        {
            return !string.IsNullOrEmpty(value) && value.ToLowerInvariant().Contains(search);
        }

        private static bool ContainsObjectNames<T>(List<T> objects, string search) where T : UnityEngine.Object
        {
            foreach (var obj in objects)
            {
                if (obj == null)
                {
                    if (search.Contains("missing") || search.Contains("null") || search.Contains("none")) return true;
                    continue;
                }

                if (ContainsSearch(obj.name, search)) return true;
            }

            return false;
        }

        private static bool HasMissingReference(ClusterEntry entry, string search)
        {
            if (!search.Contains("missing") && !search.Contains("null") && !search.Contains("none")) return false;
            return entry.GameObjectList.Contains(null) ||
                   entry.LightList.Contains(null) ||
                   entry.RendererList.Contains(null) ||
                   entry.ParticleSystemList.Contains(null) ||
                   entry.AudioSourceList.Contains(null) ||
                   entry.action.conditions == null;
        }

        private void DrawEntryOverrides(int cGroup)
        {
            GUIStyle textStyle = GetStyle("text");
            GUIStyle textStyle2 = GetStyle("text2");
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();

            int count4 = _ref.clusterGroups[cGroup].overrides.Count;
            GUILayout.Label(count4 + (count4 > 1 ? " Overrides" : " Override"), textStyle, GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            if (GUILayout.Button("", _skin.GetStyle(_editorData.addSmallButtonStyle),
                    GUILayout.Width(_editorData.removeButtonSize),
                    GUILayout.Height(_editorData.removeButtonSize)))
            {
                Undo.RecordObject(_ref, "Add Override");
                _ref.clusterGroups[cGroup].overrides.Add(new ClusterOverride());
                _ref.clusterGroups[cGroup].showOverrides = true;
                return;
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(7.5f);

            if (_ref.clusterGroups[cGroup].showOverrides)
            {
                for (var cAction = 0; cAction < _ref.clusterGroups[cGroup].overrides.Count; cAction++)
                {
                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button("", _skin.GetStyle(_editorData.duplicateButtonStyle),
                            GUILayout.Width(_editorData.removeButtonSize),
                            GUILayout.Height(_editorData.removeButtonSize)))
                    {
                        Undo.RecordObject(_ref, "Duplicate Override");
                        _ref.clusterGroups[cGroup].overrides.Insert(cAction + 1, CloneOverride(_ref.clusterGroups[cGroup].overrides[cAction]));
                        return;
                    }

                    if (GUILayout.Button("", _skin.GetStyle(_editorData.removeButtonStyle),
                            GUILayout.Width(_editorData.removeButtonSize),
                            GUILayout.Height(_editorData.removeButtonSize)))
                    {
                        Undo.RecordObject(_ref, "Remove Override");
                        _ref.clusterGroups[cGroup].overrides.RemoveAt(cAction);
                        return;
                    }

                    GUILayout.Space(5);
                    GUILayout.Label("" + (cAction + 1) + ":", textStyle2, GUILayout.ExpandWidth(false));
                    _ref.clusterGroups[cGroup].overrides[cAction].type =
                        (ClUSTER_OVERRIDE_TYPE)EditorGUILayout.EnumPopup(
                            GetGUIContent("", "The type of this action event"),
                            _ref.clusterGroups[cGroup].overrides[cAction].type, GUILayout.MaxWidth(100));

                    string[] names = GetClusterGroupNames(_ref).ToArray();
                    var tempIndex2 = EditorGUILayout.Popup(_ref.clusterGroups[cGroup].overrides[cAction].clusterGroupIndex, names, GUILayout.MaxWidth(100));
                    if (names.Length > 0)
                        _ref.clusterGroups[cGroup].overrides[cAction].clusterGroupIndex = tempIndex2;
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(10);
                }
            }

            GUILayout.Space(10);
        }

        private List<string> GetClusterGroupNames(Cluster cluster)
        {
            List<string> names = new List<string>();
            foreach (var cGroup in cluster.clusterGroups)
            {
                names.Add(cGroup.clusterGroupName);
            }

            return names;
        }

        private void DrawGroupEntries(int cGroup)
        {
            GUIStyle textStyle = GetStyle("text");
            GUIStyle textStyle2 = GetStyle("text2");

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            int count1 = _ref.clusterGroups[cGroup].Entries.Count;
            GUILayout.Label(count1 + (count1 > 1 ? " Entries" : " Entry"), textStyle, GUILayout.ExpandWidth(false));
            GUILayout.Space(15);
            if (GUILayout.Button("", _skin.GetStyle(_editorData.addSmallButtonStyle),
                    GUILayout.Width(_editorData.removeButtonSize),
                    GUILayout.Height(_editorData.removeButtonSize)))
            {
                _ref.clusterGroups[cGroup].show = true;
                Undo.RecordObject(_ref, "Add Entry");
                _ref.clusterGroups[cGroup].Entries.Add(new ClusterEntry());
                return;
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(7.5f);

            for (var cEntry = 0; cEntry < _ref.clusterGroups[cGroup].Entries.Count; cEntry++)
            {
                var entry = _ref.clusterGroups[cGroup].Entries[cEntry];

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("",
                        entry.show ? _skin.GetStyle(_editorData.openButtonStyle) : _skin.GetStyle(_editorData.collapseButtonStyle),
                        GUILayout.Width(_editorData.removeButtonSize),
                        GUILayout.Height(_editorData.removeButtonSize)))
                {
                    entry.show = !entry.show;
                    return;
                }

                GUILayout.Space(5);
                GUILayout.Label("" + (cEntry + 1) + ":", textStyle2, GUILayout.ExpandWidth(false));
                GUILayout.Space(5);
                entry.Type = (ClUSTER_ENTRY_TYPE)EditorGUILayout.EnumPopup(GetGUIContent("", "The type of this entry"), entry.Type, GUILayout.ExpandWidth(false));
                GUILayout.Space(5);

                if (GUILayout.Button("", _skin.GetStyle(_editorData.duplicateButtonStyle),
                        GUILayout.Width(_editorData.removeButtonSize),
                        GUILayout.Height(_editorData.removeButtonSize)))
                {
                    Undo.RecordObject(_ref, "Duplicate Entry");
                    _ref.clusterGroups[cGroup].Entries.Insert(cEntry + 1, CloneEntry(entry));
                    return;
                }

                if (GUILayout.Button("", _skin.GetStyle(_editorData.removeButtonStyle),
                        GUILayout.Width(_editorData.removeButtonSize),
                        GUILayout.Height(_editorData.removeButtonSize)))
                {
                    Undo.RecordObject(_ref, "Remove Entry");
                    _ref.clusterGroups[cGroup].Entries.RemoveAt(cEntry);
                    return;
                }

                EditorGUILayout.EndHorizontal();

                if (!entry.show)
                {
                    GUILayout.Space(10);
                    continue;
                }

                GUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(45);
                EditorGUILayout.BeginVertical();

                var clusterReference = new SerializedObject(_ref);
                SerializedProperty clusterGroup = clusterReference.FindProperty("clusterGroups");
                clusterGroup = clusterGroup.GetArrayElementAtIndex(cGroup);
                SerializedProperty clusterEntry = clusterGroup.FindPropertyRelative("Entries");
                clusterEntry = clusterEntry.GetArrayElementAtIndex(cEntry);

                DrawEntrySpecificSettings(clusterReference, clusterEntry, entry);

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(45);
                EditorGUILayout.LabelField("Actions", textStyle, GUILayout.ExpandWidth(false));
                GUILayout.Space(10);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(5);

                DrawEntryActions(clusterReference, clusterEntry, entry);
                GUILayout.Space(10);
            }

            GUILayout.Space(10);
        }

        private void DrawEntrySpecificSettings(SerializedObject clusterReference, SerializedProperty clusterEntry, ClusterEntry entry)
        {
            switch (entry.Type)
            {
                case ClUSTER_ENTRY_TYPE.GameObject:
                    DrawPropertyList(clusterReference, clusterEntry.FindPropertyRelative("GameObjectList"));
                    break;
                case ClUSTER_ENTRY_TYPE.TagChange:
                    DrawPropertyList(clusterReference, clusterEntry.FindPropertyRelative("GameObjectList"));
                    break;
                case ClUSTER_ENTRY_TYPE.LayerChange:
                    DrawPropertyList(clusterReference, clusterEntry.FindPropertyRelative("GameObjectList"));
                    entry.layerAppliedOnChild = EditorGUILayout.Toggle("Layer Applied To Childs?", entry.layerAppliedOnChild);
                    break;
                case ClUSTER_ENTRY_TYPE.Light:
                    DrawPropertyList(clusterReference, clusterEntry.FindPropertyRelative("LightList"));
                    entry.lightTransition = DrawHorizontalToggle(
                        "Intensity Blend?",
                        "Should the light intensity lerp up or down instead of being instantly enabled or disabled?",
                        _editorData.labelHeight,
                        entry.lightTransition);
                    if (entry.lightTransition)
                    {
                        entry.lightTransitionTime = EditorGUILayout.FloatField("Transition Time", entry.lightTransitionTime);
                        entry.enableLightIntensityAmount = EditorGUILayout.FloatField("Intensity (Enabled)", entry.enableLightIntensityAmount);
                        entry.disableLightIntensityAmount = EditorGUILayout.FloatField("Intensity (Disabled)", entry.disableLightIntensityAmount);
                    }
                    break;
                case ClUSTER_ENTRY_TYPE.Renderer:
                    DrawPropertyList(clusterReference, clusterEntry.FindPropertyRelative("RendererList"));
                    entry.hideRendererNotDisable = DrawHorizontalToggle(
                        "Keep Shadows?",
                        "Should the shadow casted by these renderers still be rendered?",
                        _editorData.labelHeight,
                        entry.hideRendererNotDisable);
                    break;
                case ClUSTER_ENTRY_TYPE.MaterialChange:
                    DrawPropertyList(clusterReference, clusterEntry.FindPropertyRelative("RendererList"));
                    break;
                case ClUSTER_ENTRY_TYPE.ParticleSystem:
                    DrawPropertyList(clusterReference, clusterEntry.FindPropertyRelative("ParticleSystemList"));
                    break;
                case ClUSTER_ENTRY_TYPE.SoundPlay:
                    DrawSoundEntrySettings(clusterReference, clusterEntry, entry);
                    break;
            }
        }

        private void DrawPropertyList(SerializedObject clusterReference, SerializedProperty property)
        {
            clusterReference.Update();
            EditorGUILayout.PropertyField(property, true, GUILayout.ExpandWidth(false));
            clusterReference.ApplyModifiedProperties();
        }

        private void DrawSoundEntrySettings(SerializedObject clusterReference, SerializedProperty clusterEntry, ClusterEntry entry)
        {
            DrawPropertyList(clusterReference, clusterEntry.FindPropertyRelative("AudioSourceList"));

            GUILayout.Space(5);
            EditorGUILayout.LabelField("Sound Settings", GetStyle("text2"), GUILayout.ExpandWidth(false));
            entry.soundOverrideAudioSourceSettings = EditorGUILayout.Toggle("Override Source Settings?", entry.soundOverrideAudioSourceSettings);
            entry.soundOverrideClip = EditorGUILayout.Toggle("Override Clip?", entry.soundOverrideClip);

            if (entry.soundOverrideClip)
            {
                entry.soundClip = (AudioClip)EditorGUILayout.ObjectField("Clip", entry.soundClip, typeof(AudioClip), false);
            }

            entry.soundOutputMixerGroup = (AudioMixerGroup)EditorGUILayout.ObjectField("Mixer Group", entry.soundOutputMixerGroup, typeof(AudioMixerGroup), false);
            entry.soundMute = EditorGUILayout.Toggle("Mute", entry.soundMute);
            entry.soundLoop = EditorGUILayout.Toggle("Loop", entry.soundLoop);
            entry.soundPlayOnAwake = EditorGUILayout.Toggle("Play On Awake", entry.soundPlayOnAwake);
            entry.soundBypassEffects = EditorGUILayout.Toggle("Bypass Effects", entry.soundBypassEffects);
            entry.soundBypassListenerEffects = EditorGUILayout.Toggle("Bypass Listener Effects", entry.soundBypassListenerEffects);
            entry.soundBypassReverbZones = EditorGUILayout.Toggle("Bypass Reverb Zones", entry.soundBypassReverbZones);
            entry.soundIgnoreListenerPause = EditorGUILayout.Toggle("Ignore Listener Pause", entry.soundIgnoreListenerPause);
            entry.soundIgnoreListenerVolume = EditorGUILayout.Toggle("Ignore Listener Volume", entry.soundIgnoreListenerVolume);
            entry.soundVolume = EditorGUILayout.Slider("Volume", entry.soundVolume, 0f, 1f);
            entry.soundPitch = EditorGUILayout.Slider("Pitch", entry.soundPitch, -3f, 3f);
            entry.soundPriority = EditorGUILayout.IntSlider("Priority", entry.soundPriority, 0, 256);
            entry.soundStereoPan = EditorGUILayout.Slider("Stereo Pan", entry.soundStereoPan, -1f, 1f);
            entry.soundSpatialBlend = EditorGUILayout.Slider("Spatial Blend", entry.soundSpatialBlend, 0f, 1f);
            entry.soundReverbZoneMix = EditorGUILayout.FloatField("Reverb Zone Mix", entry.soundReverbZoneMix);
            entry.soundDopplerLevel = EditorGUILayout.FloatField("Doppler Level", entry.soundDopplerLevel);
            entry.soundSpread = EditorGUILayout.Slider("Spread", entry.soundSpread, 0f, 360f);
            entry.soundMinDistance = EditorGUILayout.FloatField("Min Distance", entry.soundMinDistance);
            entry.soundMaxDistance = EditorGUILayout.FloatField("Max Distance", entry.soundMaxDistance);
            entry.soundRolloffMode = (AudioRolloffMode)EditorGUILayout.EnumPopup("Rolloff Mode", entry.soundRolloffMode);
        }

        private void DrawEntryActions(SerializedObject clusterReference, SerializedProperty clusterEntry, ClusterEntry entry)
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(45);
            EditorGUI.BeginDisabledGroup(true);
            entry.action.ActionEventType1 = (ClUSTER_ACTION_EVENT_TYPE)EditorGUILayout.EnumPopup(
                GetGUIContent("", "The type of this action event"),
                entry.action.ActionEventType1,
                GUILayout.MaxWidth(100));
            EditorGUI.EndDisabledGroup();

            SerializedProperty clusterAction = clusterEntry.FindPropertyRelative("action");
            DrawActionValue(clusterReference, clusterAction, entry, true);

            entry.action.enterDelay = EditorGUILayout.FloatField(entry.action.enterDelay, GUILayout.MaxWidth(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(45);
            EditorGUI.BeginDisabledGroup(true);
            entry.action.ActionEventType2 = (ClUSTER_ACTION_EVENT_TYPE)EditorGUILayout.EnumPopup(
                GetGUIContent("", "The type of this action event"),
                entry.action.ActionEventType2,
                GUILayout.MaxWidth(100));
            EditorGUI.EndDisabledGroup();

            DrawActionValue(clusterReference, clusterAction, entry, false);

            entry.action.exitDelay = EditorGUILayout.FloatField(entry.action.exitDelay, GUILayout.MaxWidth(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(45);
            EditorGUILayout.LabelField("Conditions:", GUILayout.ExpandWidth(false), GUILayout.MaxWidth(100));
            entry.action.conditions = (ClusterConditions)EditorGUILayout.ObjectField(
                entry.action.conditions,
                typeof(ClusterConditions), false,
                GUILayout.ExpandWidth(false), GUILayout.MaxWidth(202));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawActionValue(SerializedObject clusterReference, SerializedProperty clusterAction, ClusterEntry entry, bool isEnter)
        {
            switch (entry.Type)
            {
                case ClUSTER_ENTRY_TYPE.GameObject:
                case ClUSTER_ENTRY_TYPE.Light:
                case ClUSTER_ENTRY_TYPE.Renderer:
                case ClUSTER_ENTRY_TYPE.ParticleSystem:
                    if (isEnter)
                        entry.action.enterActionType = (ClUSTER_ACTION_TYPE)EditorGUILayout.EnumPopup(GetGUIContent("", "The type of this action"), entry.action.enterActionType, GUILayout.MaxWidth(100));
                    else
                        entry.action.exitActionType = (ClUSTER_ACTION_TYPE)EditorGUILayout.EnumPopup(GetGUIContent("", "The type of this action"), entry.action.exitActionType, GUILayout.MaxWidth(100));
                    break;
                case ClUSTER_ENTRY_TYPE.MaterialChange:
                    if (isEnter)
                        entry.action.enterMaterial = (Material)EditorGUILayout.ObjectField(entry.action.enterMaterial, typeof(Material), false, GUILayout.MaxWidth(100));
                    else
                        entry.action.exitMaterial = (Material)EditorGUILayout.ObjectField(entry.action.exitMaterial, typeof(Material), false, GUILayout.MaxWidth(100));
                    break;
                case ClUSTER_ENTRY_TYPE.TagChange:
                    if (isEnter)
                        entry.action.enterTag = EditorGUILayout.TagField(entry.action.enterTag, GUILayout.MaxWidth(100));
                    else
                        entry.action.exitTag = EditorGUILayout.TagField(entry.action.exitTag, GUILayout.MaxWidth(100));
                    break;
                case ClUSTER_ENTRY_TYPE.LayerChange:
                    if (isEnter)
                        entry.action.enterLayer = EditorGUILayout.LayerField(entry.action.enterLayer, GUILayout.MaxWidth(100));
                    else
                        entry.action.exitLayer = EditorGUILayout.LayerField(entry.action.exitLayer, GUILayout.MaxWidth(100));
                    break;
                case ClUSTER_ENTRY_TYPE.UnityEvent:
                    SerializedProperty eventList = clusterAction.FindPropertyRelative(isEnter ? "enterEvents" : "exitEvents");
                    clusterReference.Update();
                    EditorGUILayout.PropertyField(eventList, true, GUILayout.ExpandWidth(false));
                    clusterReference.ApplyModifiedProperties();
                    break;
                case ClUSTER_ENTRY_TYPE.SoundPlay:
                    if (isEnter)
                        entry.action.enterSoundAction = (ClUSTER_SOUND_ACTION_TYPE)EditorGUILayout.EnumPopup(GetGUIContent("", "The sound action to execute"), entry.action.enterSoundAction, GUILayout.MaxWidth(100));
                    else
                        entry.action.exitSoundAction = (ClUSTER_SOUND_ACTION_TYPE)EditorGUILayout.EnumPopup(GetGUIContent("", "The sound action to execute"), entry.action.exitSoundAction, GUILayout.MaxWidth(100));
                    break;
            }
        }

        private static ClusterDATA CloneClusterGroup(ClusterDATA source)
        {
            var clone = new ClusterDATA
            {
                clusterGroupName = source.clusterGroupName + " Copy",
                show = source.show,
                showEntries = source.showEntries,
                showOverrides = source.showOverrides,
                showGizmos = source.showGizmos
            };

            foreach (var entry in source.Entries)
            {
                clone.Entries.Add(CloneEntry(entry));
            }

            foreach (var entryOverride in source.overrides)
            {
                clone.overrides.Add(CloneOverride(entryOverride));
            }

            return clone;
        }

        private static ClusterEntry CloneEntry(ClusterEntry source)
        {
            var clone = new ClusterEntry
            {
                Type = source.Type,
                action = CloneAction(source.action),
                hideRendererNotDisable = source.hideRendererNotDisable,
                lightTransition = source.lightTransition,
                lightTransitionTime = source.lightTransitionTime,
                enableLightIntensityAmount = source.enableLightIntensityAmount,
                disableLightIntensityAmount = source.disableLightIntensityAmount,
                soundOverrideAudioSourceSettings = source.soundOverrideAudioSourceSettings,
                soundOverrideClip = source.soundOverrideClip,
                soundClip = source.soundClip,
                soundOutputMixerGroup = source.soundOutputMixerGroup,
                soundMute = source.soundMute,
                soundLoop = source.soundLoop,
                soundPlayOnAwake = source.soundPlayOnAwake,
                soundBypassEffects = source.soundBypassEffects,
                soundBypassListenerEffects = source.soundBypassListenerEffects,
                soundBypassReverbZones = source.soundBypassReverbZones,
                soundIgnoreListenerPause = source.soundIgnoreListenerPause,
                soundIgnoreListenerVolume = source.soundIgnoreListenerVolume,
                soundVolume = source.soundVolume,
                soundPitch = source.soundPitch,
                soundPriority = source.soundPriority,
                soundStereoPan = source.soundStereoPan,
                soundSpatialBlend = source.soundSpatialBlend,
                soundReverbZoneMix = source.soundReverbZoneMix,
                soundDopplerLevel = source.soundDopplerLevel,
                soundSpread = source.soundSpread,
                soundMinDistance = source.soundMinDistance,
                soundMaxDistance = source.soundMaxDistance,
                soundRolloffMode = source.soundRolloffMode,
                show = source.show,
                layerAppliedOnChild = source.layerAppliedOnChild
            };
            clone.GameObjectList = new List<GameObject>(source.GameObjectList);
            clone.LightList = new List<Light>(source.LightList);
            clone.RendererList = new List<Renderer>(source.RendererList);
            clone.ParticleSystemList = new List<ParticleSystem>(source.ParticleSystemList);
            clone.AudioSourceList = new List<AudioSource>(source.AudioSourceList);
            return clone;
        }

        private static ClusterAction CloneAction(ClusterAction source)
        {
            return new ClusterAction
            {
                ActionEventType1 = source.ActionEventType1,
                ActionEventType2 = source.ActionEventType2,
                enterActionType = source.enterActionType,
                exitActionType = source.exitActionType,
                enterSoundAction = source.enterSoundAction,
                exitSoundAction = source.exitSoundAction,
                enterDelay = source.enterDelay,
                exitDelay = source.exitDelay,
                enterMaterial = source.enterMaterial,
                exitMaterial = source.exitMaterial,
                enterTag = source.enterTag,
                exitTag = source.exitTag,
                enterLayer = source.enterLayer,
                exitLayer = source.exitLayer,
                conditions = source.conditions,
                enterEvents = source.enterEvents,
                exitEvents = source.exitEvents
            };
        }

        private static ClusterOverride CloneOverride(ClusterOverride source)
        {
            return new ClusterOverride
            {
                type = source.type,
                clusterGroupIndex = source.clusterGroupIndex
            };
        }

        private GUIStyle GetStyle(string styleName)
        {
            var style = new GUIStyle();
            switch (styleName)
            {
                case "title":
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontSize = 22;
                    style.fontStyle = FontStyle.Bold;
                    style.normal.textColor = Color.white;
                    break;
                case "text":
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontSize = 16;
                    style.fontStyle = FontStyle.Bold;
                    style.normal.textColor = Color.white;
                    break;
                case "text2":
                    style.alignment = TextAnchor.UpperLeft;
                    style.fontSize = 16;
                    style.fontStyle = FontStyle.Bold;
                    style.normal.textColor = Color.white;
                    break;
                case "removeButton":
                    style.normal.textColor = Color.red;
                    style.fontSize = 30;
                    break;
                case "collapseGroup":
                    style.normal.textColor = Color.gray;
                    style.fontSize = 30;
                    break;
                case "openGroup":
                    style.normal.textColor = Color.green;
                    style.fontSize = 30;
                    break;
            }

            return style;
        }

        private GUIContent GetGUIContent(string name, string tooltip)
        {
            return new GUIContent(name, tooltip);
        }

        private bool DrawHorizontalToggle(string labelName, string tooltip, float smallFieldHeight, bool toggle)
        {
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(labelName))
                EditorGUILayout.LabelField(new GUIContent(labelName, tooltip), GUILayout.Width(_editorData.labelWidth), GUILayout.Height(smallFieldHeight));
            toggle = EditorGUILayout.Toggle(toggle, GUILayout.Height(smallFieldHeight));
            GUILayout.EndHorizontal();
            return toggle;
        }

        private object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null) return null;

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in elements)
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("[", StringComparison.Ordinal));
                    var index = Convert.ToInt32(element.Substring(element.IndexOf("[", StringComparison.Ordinal)).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }

            return obj;
        }

        private object GetValue_Imp(object source, string name)
        {
            if (source == null)
                return null;
            var type = source.GetType();

            while (type != null)
            {
                var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                    return f.GetValue(source);

                var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                    return p.GetValue(source, null);

                type = type.BaseType;
            }
            return null;
        }

        private object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();

            for (var i = 0; i <= index; i++)
                if (!enm.MoveNext()) return null;
            return enm.Current;
        }
    }
}
