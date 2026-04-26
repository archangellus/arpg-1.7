using System.Collections.Generic;
using System.IO;
using PLAYERTWO.ARPGProject;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    public class EnemyPrefabDropWindow : EditorWindow
    {
        protected const float k_dropZoneHeight = 90f;

        protected GameObject m_prefab;
        protected Vector2 m_scroll;

        [MenuItem("PLAYER TWO/ARPG Project/Tools/Enemy Prefab Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<EnemyPrefabDropWindow>("Enemy Prefab Setup");
            window.minSize = new Vector2(420, 240);
        }

        protected virtual void OnGUI()
        {
            m_scroll = EditorGUILayout.BeginScrollView(m_scroll);

            try
            {
                EditorGUILayout.HelpBox(
                    "Drag a prefab into the drop zone and click 'Configure Enemy Prefab'. " +
                    "The tool validates dependencies, adds the enemy components, and wires defaults.",
                    MessageType.Info
                );

                DrawDropZone();

                EditorGUILayout.Space(8);
                m_prefab = (GameObject)EditorGUILayout.ObjectField(
                    "Prefab",
                    m_prefab,
                    typeof(GameObject),
                    false
                );

                using (new EditorGUI.DisabledScope(!CanConfigure(m_prefab, out _)))
                {
                    if (GUILayout.Button("Configure Enemy Prefab", GUILayout.Height(32)))
                    {
                        try
                        {
                            ConfigurePrefab(m_prefab);
                        }
                        catch (System.Exception exception)
                        {
                            Debug.LogException(exception);
                            EditorUtility.DisplayDialog(
                                "Enemy Prefab Setup",
                                "An unexpected error occurred while configuring the prefab. Check the Console for details.",
                                "Ok"
                            );
                        }
                    }
                }

                if (m_prefab && !CanConfigure(m_prefab, out var error))
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        protected virtual void DrawDropZone()
        {
            var dropZone = GUILayoutUtility.GetRect(0f, k_dropZoneHeight, GUILayout.ExpandWidth(true));
            UnityEngine.GUI.Box(dropZone, "Drop prefab here", EditorStyles.helpBox);

            var evt = Event.current;

            if (!dropZone.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is GameObject candidate && CanConfigure(candidate, out _))
                    {
                        m_prefab = candidate;
                        Repaint();
                        break;
                    }
                }

                evt.Use();
            }
        }

        protected virtual bool CanConfigure(GameObject prefab, out string error)
        {
            error = string.Empty;

            if (!prefab)
            {
                error = "Select a prefab asset.";
                return false;
            }

            var path = AssetDatabase.GetAssetPath(prefab);

            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
            {
                error = "The object must be a prefab asset from the Project window.";
                return false;
            }

            return true;
        }

        protected virtual void ConfigurePrefab(GameObject prefab)
        {
            if (!CanConfigure(prefab, out var error))
            {
                EditorUtility.DisplayDialog("Enemy Prefab Setup", error, "Ok");
                return;
            }

            var path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            var outputPath = GetOutputPrefabPath(prefab.name, path);

            try
            {
                var animator = root.GetComponentInChildren<Animator>(true);

                if (!animator)
                {
                    EditorUtility.DisplayDialog(
                        "Enemy Prefab Setup",
                        "The prefab must contain an Animator component in its hierarchy.",
                        "Ok"
                    );
                    return;
                }

                root.tag = GameTags.Enemy;

                var entity = GetOrAdd<Entity>(root);
                GetOrAdd<CharacterController>(root);
                GetOrAdd<EntityStatsManager>(root);
                var skillManager = GetOrAdd<EntitySkillManager>(root);
                var itemManager = GetOrAdd<EntityItemManager>(root);
                var entityAnimator = GetOrAdd<EntityAnimator>(root);
                var entityAi = GetOrAdd<EntityAI>(root);
                var mapMarker = GetOrAdd<MapMarker>(root);
                var highlighter = GetOrAdd<Highlighter>(root);
                GetOrAdd<EntityHealthBar>(root);
                var feedback = GetOrAdd<EntityFeedback>(root);
                var itemLoot = GetOrAdd<ItemLoot>(root);
                GetOrAdd<EntityAudio>(root);
                var rootCollider = GetOrAdd<BoxCollider>(root);

                EnsureTargetTags(entityAi);
                EnsureTargetTags(entity);
                EnsureCastingOrigins(root.transform, skillManager);
                EnsureItemSlots(root.transform, itemManager, skillManager.hands);
                EnsureMapMarkerType(mapMarker);
                EnsureHighlighter(highlighter, root.transform);
                EnsureFeedback(feedback, root.transform);
                EnsureItemLootStats(itemLoot, prefab.name, outputPath);
                EnsureAnimatorController(animator);
                EnsureAnimatorOverrides(entityAnimator);
                EnsureAnimationEvents(animator.gameObject, entity);
                EnsureHitbox(root.transform, entity);
                EnsureRootCollider(rootCollider);
                EnsureFxChildren(root.transform);
                ConfigureFeedbackReferences(feedback, root.transform);

                if (skillManager.skills == null)
                    skillManager.skills = new List<Skill>();

                skillManager.autoEquipFirstSkill = false;

                root.name = Path.GetFileNameWithoutExtension(outputPath);
                PrefabUtility.SaveAsPrefabAsset(root, outputPath);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog(
                    "Enemy Prefab Setup",
                    $"Created '{root.name}' as a configured enemy prefab.",
                    "Ok"
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        protected virtual string GetOutputPrefabPath(string sourceName, string sourcePath)
        {
            var directory = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/");

            if (string.IsNullOrEmpty(directory))
                directory = "Assets";

            var targetPath = $"{directory}/{sourceName} Enemy.prefab";
            return AssetDatabase.GenerateUniqueAssetPath(targetPath);
        }

        protected virtual void EnsureTargetTags(EntityAI ai)
        {
            ai.targetTags ??= new List<string>();
            AddUnique(ai.targetTags, GameTags.Player);
            AddUnique(ai.targetTags, GameTags.Summoned);
        }

        protected virtual void EnsureTargetTags(Entity entity)
        {
            entity.targetTags ??= new List<string>();
            AddUnique(entity.targetTags, GameTags.Player);
            AddUnique(entity.targetTags, GameTags.Summoned);
        }

        protected virtual void EnsureCastingOrigins(Transform root, EntitySkillManager skills)
        {
            if (skills.hands)
                return;

            var castingOrigins = FindOrCreateChild(root, "Casting Origins");
            var hands = FindOrCreateChild(castingOrigins, "Hands");
            skills.hands = hands;
        }

        protected virtual void EnsureItemSlots(Transform root, EntityItemManager items, Transform fallback)
        {
            var slots = FindOrCreateChild(root, "Item Slots");

            items.rightHandSlot = items.rightHandSlot
                ? items.rightHandSlot
                : FindOrCreateChild(slots, "Right Hand Slot");

            items.leftHandSlot = items.leftHandSlot
                ? items.leftHandSlot
                : FindOrCreateChild(slots, "Left Hand Slot");

            items.leftHandShieldSlot = items.leftHandShieldSlot
                ? items.leftHandShieldSlot
                : FindOrCreateChild(slots, "Shield Slot");

            items.projectileOrigin = items.projectileOrigin
                ? items.projectileOrigin
                : fallback ? fallback : FindOrCreateChild(slots, "Projectile Origin");
        }

        protected virtual void EnsureAnimatorController(Animator animator)
        {
            if (animator.runtimeAnimatorController)
                return;

            var guids = AssetDatabase.FindAssets("Humanoid Animator t:RuntimeAnimatorController");

            if (guids.Length == 0)
                return;

            var controllerPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

            if (controller)
                animator.runtimeAnimatorController = controller;
        }

        protected virtual void EnsureAnimatorOverrides(EntityAnimator entityAnimator)
        {
            if (entityAnimator.defaultAnimations)
                return;

            var guids = AssetDatabase.FindAssets("Zombie Default t:AnimatorOverrideController");

            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets("t:AnimatorOverrideController");

            if (guids.Length == 0)
                return;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            entityAnimator.defaultAnimations = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        }

        protected virtual void EnsureMapMarkerType(MapMarker marker)
        {
            if (marker.type)
                return;

            var guids = AssetDatabase.FindAssets("Enemy Marker t:MapMarkerType");

            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets("t:MapMarkerType");

            if (guids.Length == 0)
                return;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            marker.type = AssetDatabase.LoadAssetAtPath<MapMarkerType>(path);
        }

        protected virtual void EnsureHighlighter(Highlighter highlighter, Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            if (renderers == null || renderers.Length == 0)
                return;

            highlighter.renderers = renderers;
        }

        protected virtual void EnsureFeedback(EntityFeedback feedback, Transform root)
        {
            feedback.meshRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        protected virtual void EnsureItemLootStats(ItemLoot itemLoot, string prefabName, string prefabPath)
        {
            if (itemLoot.stats == null)
                itemLoot.stats = CreateItemLootStats(prefabName, prefabPath);
        }

        protected virtual ItemLootStats CreateItemLootStats(string prefabName, string prefabPath)
        {
            var directory = Path.GetDirectoryName(prefabPath)?.Replace("\\", "/");

            if (string.IsNullOrEmpty(directory))
                directory = "Assets";

            var statsPath = $"{directory}/{prefabName} Item Loot Stats.asset";
            var uniquePath = AssetDatabase.GenerateUniqueAssetPath(statsPath);
            var instance = ScriptableObject.CreateInstance<ItemLootStats>();

            var source = FindSourceItemLootStats();
            if (source)
                EditorUtility.CopySerialized(source, instance);

            instance.name = Path.GetFileNameWithoutExtension(uniquePath);
            AssetDatabase.CreateAsset(instance, uniquePath);
            return instance;
        }

        protected virtual ItemLootStats FindSourceItemLootStats()
        {
            var guids = AssetDatabase.FindAssets("t:ItemLootStats");

            if (guids.Length == 0)
                return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<ItemLootStats>(path);
        }

        protected virtual void EnsureAnimationEvents(GameObject animatorObject, Entity entity)
        {
            var listener = GetOrAdd<EntityAnimationEventListener>(animatorObject);
            listener.onAttack ??= new UnityEvent();

            if (HasPersistentMethod(listener.onAttack, entity, nameof(Entity.PerformAttack)))
                return;

            UnityEventTools.AddPersistentListener(listener.onAttack, entity.PerformAttack);
            EditorUtility.SetDirty(listener);
        }

        protected virtual void EnsureHitbox(Transform root, Entity entity)
        {
            var hitboxRoot = FindOrCreateChild(root, "Hitboxes");
            var hitboxTransform = FindOrCreateChild(hitboxRoot, "Melee");
            hitboxTransform.localPosition = new Vector3(0f, 1f, 0.7f);

            var collider = GetOrAdd<BoxCollider>(hitboxTransform.gameObject);
            collider.size = new Vector3(1f, 1f, 1f);
            collider.center = new Vector3(0f, 0.5f, 0f);
            collider.isTrigger = false;
            collider.enabled = false;

            var hitbox = GetOrAdd<EntityHitbox>(hitboxTransform.gameObject);
            hitbox.incrementCombo = true;
            hitbox.areaDamage = false;
            entity.hitbox = hitbox;
        }

        protected virtual void EnsureRootCollider(BoxCollider collider)
        {
            collider.isTrigger = true;
            collider.enabled = true;
        }

        protected virtual void EnsureFxChildren(Transform root)
        {
            CopyZombieChild("Damage Particles", root);
            CopyZombieChild("Blob Shadow", root);
        }

        protected virtual void ConfigureFeedbackReferences(EntityFeedback feedback, Transform root)
        {
            if (!feedback.damageText)
                feedback.damageText = FindDamageTextPrefab();

            if (!TryFindDeepChild(root, "Damage Particles", out var damageParticlesRoot))
                return;

            var particles = damageParticlesRoot.GetComponentsInChildren<ParticleSystem>(true);

            if (particles.Length > 0)
                feedback.damageParticle = particles[0];

            if (particles.Length > 1)
                feedback.criticalDamageParticle = particles[1];
        }

        protected virtual GameObject FindDamageTextPrefab()
        {
            var searchPath = "Assets/PLAYER TWO/ARPG Project/Examples/Prefabs/Misc";
            var guids = AssetDatabase.FindAssets("Damage Text t:Prefab", new[] { searchPath });

            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets("DamageText t:Prefab", new[] { searchPath });

            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets("Damage Text t:Prefab");

            if (guids.Length == 0)
                return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        protected virtual void CopyZombieChild(string childName, Transform destinationRoot)
        {
            if (TryFindDeepChild(destinationRoot, childName, out var existing))
                DestroyImmediate(existing.gameObject);

            var zombiePrefab = FindZombiePrefab();

            if (!zombiePrefab)
            {
                FindOrCreateChild(destinationRoot, childName);
                return;
            }

            var zombiePath = AssetDatabase.GetAssetPath(zombiePrefab);
            var zombieRoot = PrefabUtility.LoadPrefabContents(zombiePath);

            try
            {
                if (!TryFindDeepChild(zombieRoot.transform, childName, out var sourceChild))
                {
                    FindOrCreateChild(destinationRoot, childName);
                    return;
                }

                var clone = Instantiate(sourceChild.gameObject, destinationRoot);
                clone.name = childName;
                clone.transform.SetLocalPositionAndRotation(sourceChild.localPosition, sourceChild.localRotation);
                clone.transform.localScale = sourceChild.localScale;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(zombieRoot);
            }
        }

        protected virtual GameObject FindZombiePrefab()
        {
            var guids = AssetDatabase.FindAssets("Zombie t:Prefab");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.Contains("/Examples/Prefabs/Enemies/"))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            if (guids.Length == 0)
                return null;

            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        protected virtual T GetOrAdd<T>(GameObject go) where T : Component
        {
            if (!go.TryGetComponent<T>(out var component))
                component = go.AddComponent<T>();

            return component;
        }

        protected virtual Transform FindOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);

            if (child)
                return child;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        protected virtual bool TryFindDeepChild(Transform parent, string name, out Transform result)
        {
            result = null;

            if (!parent)
                return false;

            var matches = parent.GetComponentsInChildren<Transform>(true);

            foreach (var match in matches)
            {
                if (match.name != name)
                    continue;

                result = match;
                return true;
            }

            return false;
        }

        protected virtual void AddUnique(List<string> list, string value)
        {
            if (!list.Contains(value))
                list.Add(value);
        }

        protected virtual bool HasPersistentMethod(UnityEngine.Events.UnityEvent unityEvent, Object target, string method)
        {
            if (unityEvent == null || target == null || string.IsNullOrEmpty(method))
                return false;

            for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
            {
                if (unityEvent.GetPersistentTarget(i) == target && unityEvent.GetPersistentMethodName(i) == method)
                    return true;
            }

            return false;
        }
    }
}
