using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class EnemyModelReplacerWindow : EditorWindow
    {
        [Serializable]
        private class AnimatorOverrideClipField
        {
            public string originalClipName;
            public AnimationClip originalClip;
            public AnimationClip replacementClip;
        }

        private enum EnemyModelReplacerTab
        {
            Setup,
            Preview,
            Animations,
            Duplicates,
            Profiles
        }

        private const string k_menuPath = "Tools/ANSTUDIO/Enemy Model Replacer";

        [SerializeField] private EnemyModelReplacerTab selectedTab;
        [SerializeField] private Vector2 windowScrollPosition;

        [Header("Profiles")]
        [SerializeField] private EnemyModelReplacerProfile selectedProfile;
        [SerializeField] private string newProfileName = "Enemy Model Replacer Profile";
        [SerializeField] private string profileFolder = "Tools/ANSTUDIO/EnemyModelReplacer/Enemy Model Replacer Profiles/";

        [SerializeField] private GameObject enemyRoot;
        [SerializeField] private GameObject newModelPrefab;
        [SerializeField] private string modelContainerName = "Model";
        [SerializeField] private bool autoApplyToChildren = true;
        [SerializeField] private bool removePreviousModel = true;

        [Header("New Model Position")]
        [SerializeField] private bool forceNewModelY = false;
        [SerializeField] private float forcedNewModelLocalY = -1f;

        [Header("Animator Override Controller")]
        [SerializeField] private bool createAnimatorOverrideController = false;
        [SerializeField] private string animatorOverrideControllerName = "Enemy Animator Override";
        [SerializeField] private string animatorOverrideControllerFolder = "Tools/ANSTUDIO/EnemyModelReplacer/Animator Override Controllers/";
        [SerializeField] private RuntimeAnimatorController animatorOverrideMirrorSource;
        [SerializeField] private List<AnimatorOverrideClipField> animatorOverrideClipFields = new List<AnimatorOverrideClipField>();
        [SerializeField] private bool showAnimatorOverrideClipFields = true;
        [SerializeField] private Vector2 animatorOverrideClipFieldsScrollPosition;

        [Header("Standalone Enemy Duplicates")]
        [SerializeField] private bool createStandaloneDuplicates = false;
        [SerializeField] private int duplicateCount = 0;
        [SerializeField] private string duplicateNamePrefix = "Enemy Copy";
        [SerializeField] private float duplicateMinRadius = 2f;
        [SerializeField] private float duplicateMaxRadius = 6f;
        [SerializeField] private float duplicateMinSpacing = 2f;
        [SerializeField] private int duplicatePlacementAttempts = 100;

        [Header("Duplicate Spawn Collision")]
        [SerializeField] private bool avoidSpawnCollisionLayers = false;
        [SerializeField] private LayerMask blockedSpawnLayers = 0;
        [SerializeField] private float spawnCollisionCheckRadius = 1f;
        [SerializeField] private float spawnCollisionCheckYOffset = 0.5f;
        [SerializeField] private QueryTriggerInteraction spawnCollisionTriggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Scene Gizmo Preview")]
        [SerializeField] private float duplicateRangeGizmoYOffset = 0f;

        [Header("Prefab Preview")]
        [SerializeField] private float previewYaw;

        [MenuItem(k_menuPath)]
        public static void Open() => GetWindow<EnemyModelReplacerWindow>("Enemy Model Replacer");

        private void OnEnable()
        {
            titleContent = new GUIContent("Enemy Replacer", FindEditorIcon("Prefab Icon"));
            minSize = new Vector2(360f, 620f);

            SceneView.duringSceneGui -= DrawRangeGizmosInScene;
            SceneView.duringSceneGui += DrawRangeGizmosInScene;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawRangeGizmosInScene;
            CleanupPrefabPreview();
        }

        private void OnGUI()
        {
            EnsureModernStyles();
            DrawModernHeader();
            DrawTabBar();

            windowScrollPosition = EditorGUILayout.BeginScrollView(
                windowScrollPosition,
                false,
                false,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            try
            {
                EditorGUI.BeginChangeCheck();
                DrawSelectedTab();

                if (EditorGUI.EndChangeCheck())
                {
                    SceneView.RepaintAll();
                    Repaint();
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }

            DrawActionFooter();
        }
    }
}
