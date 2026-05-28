using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PLAYERTWO.ARPGProject.InfinityWardrobe
{
    /// <summary>
    /// Mirrors the equipped armor pieces into Infinity PBR wardrobe objects.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class InfinityWardrobeBinder : MonoBehaviour
    {
        private static readonly BindingFlags k_BindingFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private EntityItemManager m_items;
        private Component m_prefabAndObjectManager;
        private InfinityWardrobeLibrary m_library;
        private InfinityWardrobeService m_service;
        private bool m_loggedMissingManager;

        private readonly HashSet<string> m_missingGroups = new();
        private readonly Dictionary<Transform, Dictionary<string, Transform>> m_boneLookups =
            new();
        private readonly HashSet<int> m_processedInstances = new();

        private static readonly string[] k_EquipmentMethodCandidates =
            { "EquipObject", "EquipNow", "Equip" };

        private void Awake()
        {
            m_items = GetComponent<EntityItemManager>();
        }

        private void OnDestroy()
        {
            if (m_items)
                m_items.onChanged.RemoveListener(OnItemsChanged);

            m_service?.NotifyBinderDestroyed(this);
            m_boneLookups.Clear();
            m_processedInstances.Clear();
        }

        internal void Configure(InfinityWardrobeService service, InfinityWardrobeLibrary library)
        {
            m_service = service;
            m_library = library;

            if (!m_items)
                m_items = GetComponent<EntityItemManager>();

            if (m_items && !m_prefabAndObjectManager)
                m_prefabAndObjectManager = FindWardrobeManager();

            if (m_items)
            {
                m_items.onChanged.RemoveListener(OnItemsChanged);
                m_items.onChanged.AddListener(OnItemsChanged);
            }

            ApplyImmediate();
        }

        internal void ApplyImmediate()
        {
            if (!m_items || m_library == null)
                return;

            if (!EnsureWardrobeManager())
                return;

            var equipped = BuildEquippedItemSet();
            var contexts = BuildRuleContexts(equipped);
            var protectedEntries = CollectProtectedEntries(contexts);

            foreach (var context in contexts)
            {
                ApplyRule(
                    context.Rule,
                    context.Equipped,
                    context.Group,
                    context.Objects,
                    protectedEntries
                );
            }

            EventBus.RaiseInfinityWardrobeApplied(m_items.entity);
        }

        private void OnItemsChanged()
        {
            ApplyImmediate();
        }

        private bool EnsureWardrobeManager()
        {
            if (m_prefabAndObjectManager)
                return true;

            m_prefabAndObjectManager = FindWardrobeManager();

            if (!m_prefabAndObjectManager && !m_loggedMissingManager)
            {
                m_loggedMissingManager = true;
                Debug.LogWarning(
                    $"[InfinityWardrobe] Could not find a PrefabAndObjectManager component under '{name}'."
                );
            }

            return m_prefabAndObjectManager;
        }

        internal static bool HasWardrobeManager(Component component)
        {
            return LocateWardrobeManager(component) != null;
        }

        internal static Component LocateWardrobeManager(Component component)
        {
            if (!component)
                return null;

            var behaviours = component.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                var type = behaviour.GetType();
                if (type != null && type.Name.Equals("PrefabAndObjectManager", StringComparison.Ordinal))
                    return behaviour;
            }

            return null;
        }

        private Component FindWardrobeManager()
        {
            return LocateWardrobeManager(this);
        }

        private HashSet<Item> BuildEquippedItemSet()
        {
            var set = new HashSet<Item>();
            var equipped = m_items.GetEquippedItems();

            foreach (var instance in equipped)
            {
                if (instance?.data)
                    set.Add(instance.data);
            }

            return set;
        }

        private readonly struct RuleContext
        {
            public RuleContext(
                InfinityWardrobeRule rule,
                bool equipped,
                object group,
                IList objects
            )
            {
                Rule = rule;
                Equipped = equipped;
                Group = group;
                Objects = objects;
            }

            public InfinityWardrobeRule Rule { get; }
            public bool Equipped { get; }
            public object Group { get; }
            public IList Objects { get; }
        }

        private List<RuleContext> BuildRuleContexts(HashSet<Item> equipped)
        {
            var contexts = new List<RuleContext>();

            foreach (var rule in m_library.Rules)
            {
                if (rule == null || !rule.HasUsableItems)
                    continue;

                bool isEquipped = rule.MatchesEquippedItems(equipped);
                if (!isEquipped && !rule.RevertOnUnequip)
                    continue;

                if (!TryGetGroup(rule, out var group, out var objects))
                    continue;

                contexts.Add(new RuleContext(rule, isEquipped, group, objects));
            }

            return contexts;
        }

        private HashSet<object> CollectProtectedEntries(List<RuleContext> contexts)
        {
            var protectedEntries = new HashSet<object>();

            foreach (var context in contexts)
            {
                if (!context.Equipped)
                    continue;

                CacheProtectedEntries(context.Objects, context.Rule.Actions, protectedEntries);
            }

            return protectedEntries;
        }

        private void CacheProtectedEntries(
            IList objects,
            IReadOnlyList<InfinityWardrobeObjectAction> actions,
            HashSet<object> protectedEntries
        )
        {
            if (objects == null || actions == null)
                return;

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null || !action.EnableOnEquip)
                    continue;

                var indices = action.ObjectIndices;
                if (!HasObjectIndices(indices))
                    continue;

                for (int j = 0; j < indices.Count; j++)
                {
                    int index = indices[j];
                    var entry = GetObjectEntry(objects, index);
                    if (entry != null)
                        protectedEntries.Add(entry);
                }
            }
        }

        private void ApplyRule(
            InfinityWardrobeRule rule,
            bool equipped,
            object group,
            IList objects,
            HashSet<object> protectedEntries
        )
        {
            bool changed = false;

            if (equipped && rule.ClearGroupBeforeApply)
                changed |= DisableAll(objects);

            foreach (var action in rule.Actions)
            {
                var indices = action.ObjectIndices;
                if (!HasObjectIndices(indices))
                    continue;

                bool target = equipped ? action.EnableOnEquip : action.EnableOnUnequip;
                bool instantiate = target;

                if (!equipped
                    && !target
                    && ShouldPreserveEntry(objects, indices, protectedEntries))
                {
                    continue;
                }

                changed |= ApplyObjectState(objects, indices, target, instantiate);

                if (equipped)
                    changed |= DisableLinkedObjects(objects, action.DisableOtherObjectIndices);
            }

            if (!equipped && rule.ClearGroupOnUnequip)
                changed |= DisableAll(objects);

            if (changed)
                UpdateGroupActiveFlag(group, objects);
        }

        private bool TryGetGroup(
            InfinityWardrobeRule rule,
            out object group,
            out IList objects
        )
        {
            group = null;
            objects = null;

            if (!m_prefabAndObjectManager)
                return false;

            var groupsField = m_prefabAndObjectManager
                .GetType()
                .GetField("prefabGroups", k_BindingFlags);

            if (groupsField?.GetValue(m_prefabAndObjectManager) is not IList groups)
                return false;

            int filteredIndex = 0;

            for (int i = 0; i < groups.Count; i++)
            {
                var candidate = groups[i];
                if (candidate == null)
                    continue;

                var candidateType = candidate.GetType();
                string typeName = GetStringField(candidateType, candidate, "groupType");
                if (!string.IsNullOrEmpty(rule.GroupType)
                    && !string.Equals(typeName, rule.GroupType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidateName = GetStringField(candidateType, candidate, "name");
                bool hasName = !string.IsNullOrEmpty(rule.GroupName);
                bool matchesName = hasName
                    && string.Equals(candidateName, rule.GroupName, StringComparison.OrdinalIgnoreCase);

                bool hasIndex = rule.GroupIndex >= 0;
                bool matchesIndex = hasIndex && filteredIndex == rule.GroupIndex;

                bool shouldPick = hasName
                    ? matchesName
                    : hasIndex
                        ? matchesIndex
                        : true;

                if (shouldPick)
                {
                    var objectsField = candidateType.GetField("groupObjects", k_BindingFlags);
                    if (objectsField?.GetValue(candidate) is IList list)
                    {
                        group = candidate;
                        objects = list;
                        return true;
                    }
                }

                filteredIndex++;
            }

            var key = $"{rule.GroupType}:{rule.GroupName}:{rule.GroupIndex}";
            if (m_missingGroups.Add(key))
            {
                Debug.LogWarning(
                    $"[InfinityWardrobe] Unable to resolve group '{rule.GroupName}' (type '{rule.GroupType}')."
                );
            }

            return false;
        }

        private static string GetStringField(Type type, object instance, string field)
        {
            var info = type.GetField(field, k_BindingFlags);
            return info?.GetValue(instance) as string;
        }

        private bool DisableAll(IList objects)
        {
            bool changed = false;

            if (objects == null)
                return changed;

            for (int i = 0; i < objects.Count; i++)
                changed |= ApplyObjectState(objects, i, false, instantiate: false);

            return changed;
        }

        private bool DisableLinkedObjects(IList objects, IReadOnlyList<int> indices)
        {
            if (objects == null || indices == null)
                return false;

            bool changed = false;

            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (index < 0)
                    continue;

                changed |= ApplyObjectState(objects, index, false, instantiate: false);
            }

            return changed;
        }

        private static bool HasObjectIndices(IReadOnlyList<int> indices)
        {
            return indices != null && indices.Count > 0;
        }

        private bool ShouldPreserveEntry(
            IList objects,
            IReadOnlyList<int> indices,
            HashSet<object> protectedEntries
        )
        {
            if (!HasObjectIndices(indices))
                return false;

            for (int i = 0; i < indices.Count; i++)
            {
                if (ShouldPreserveEntry(objects, indices[i], protectedEntries))
                    return true;
            }

            return false;
        }

        private bool ShouldPreserveEntry(
            IList objects,
            int index,
            HashSet<object> protectedEntries
        )
        {
            if (protectedEntries == null || protectedEntries.Count == 0)
                return false;

            var entry = GetObjectEntry(objects, index);
            return entry != null && protectedEntries.Contains(entry);
        }

        private static object GetObjectEntry(IList objects, int index)
        {
            if (objects == null || index < 0 || index >= objects.Count)
                return null;

            return objects[index];
        }

        private bool ApplyObjectState(
            IList objects,
            IReadOnlyList<int> indices,
            bool enable,
            bool instantiate = true
        )
        {
            if (!HasObjectIndices(indices))
                return false;

            bool changed = false;

            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (index < 0)
                    continue;

                changed |= ApplyObjectState(objects, index, enable, instantiate);
            }

            return changed;
        }

        private bool ApplyObjectState(
            IList objects,
            int index,
            bool enable,
            bool instantiate = true,
            object cachedEntry = null
        )
        {
            if (objects == null)
                return false;

            var entry = cachedEntry ?? GetObjectEntry(objects, index);
            if (entry == null)
                return false;

            var entryType = entry.GetType();
            var instanceField = entryType.GetField("inGameObject", k_BindingFlags);
            var prefabField = entryType.GetField("objectToHandle", k_BindingFlags);
            var parentField = entryType.GetField("parentTransform", k_BindingFlags);
            var renderField = entryType.GetField("render", k_BindingFlags);

            var instance = instanceField?.GetValue(entry) as GameObject;
            var parent = parentField?.GetValue(entry) as Transform;
            var parentTransform = parent ? parent : (m_items ? m_items.transform : null);

            if (!instance && enable && instantiate)
            {
                var prefab = prefabField?.GetValue(entry) as GameObject;

                if (prefab)
                {
                    instance = Instantiate(prefab, parentTransform);
                    instanceField?.SetValue(entry, instance);
                    ConfigureRendererReferences(entryType, entry, instance);
                    bool processed = EnsureEquipmentObjectProcessing(instance, parentTransform);
                    if (!processed)
                        RetargetSkinnedMeshes(instance, parentTransform);
                }
            }
            else if (instance && enable)
            {
                EnsureEquipmentObjectProcessing(instance, parentTransform);
            }

            bool changed = false;

            if (instance && instance.activeSelf != enable)
            {
                instance.SetActive(enable);
                changed = true;
            }

            changed |= SetBooleanField(renderField, entry, enable);

            return changed;
        }

        private void ConfigureRendererReferences(Type entryType, object entry, GameObject instance)
        {
            if (entryType == null || entry == null || !instance)
                return;

            var meshRendererField = entryType.GetField("meshRenderer", k_BindingFlags);
            var skinnedField = entryType.GetField("skinnedMeshRenderer", k_BindingFlags);

            var skinned = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned)
            {
                skinnedField?.SetValue(entry, skinned);
                return;
            }

            var mesh = instance.GetComponentInChildren<MeshRenderer>(true);
            if (mesh)
                meshRendererField?.SetValue(entry, mesh);
        }

        private bool EnsureEquipmentObjectProcessing(GameObject instance, Transform parentTransform)
        {
            if (!instance)
                return false;

            int id = instance.GetInstanceID();
            if (m_processedInstances.Contains(id))
                return true;

            var parent = parentTransform;
            if (!parent && instance.transform.parent)
                parent = instance.transform.parent;
            else if (!parent && m_items)
                parent = m_items.transform;

            bool processed = ProcessEquipmentObjectComponents(instance, parent);
            if (processed)
                m_processedInstances.Add(id);

            return processed;
        }

        private bool ProcessEquipmentObjectComponents(GameObject instance, Transform parent)
        {
            if (!instance)
                return false;

            var animator = parent ? parent.GetComponentInParent<Animator>() : null;
            bool processedAny = false;
            var components = instance.GetComponentsInChildren<Component>(true);

            foreach (var component in components)
            {
                if (!component)
                    continue;

                var type = component.GetType();
                if (type == null)
                    continue;

                if (!string.Equals(type.Name, "EquipmentObject", StringComparison.Ordinal)
                    && (type.FullName == null
                        || !type.FullName.EndsWith(".EquipmentObject", StringComparison.Ordinal)))
                {
                    continue;
                }

                SuppressEquipmentObjectWarning(component);

                if (TryProcessEquipmentObject(type, component, parent, animator))
                {
                    DestroyEquipmentObjectComponent(component);
                    processedAny = true;
                }
            }

            return processedAny;
        }

        private static void SuppressEquipmentObjectWarning(Component component)
        {
            if (component is Behaviour behaviour && behaviour.enabled)
                behaviour.enabled = false;
        }

        private static void DestroyEquipmentObjectComponent(Component component)
        {
            if (!component)
                return;

            if (Application.isPlaying)
                Destroy(component);
            else
                DestroyImmediate(component);
        }

        private bool TryProcessEquipmentObject(Type type, Component component, Transform parent, Animator animator)
        {
            foreach (var methodName in k_EquipmentMethodCandidates)
            {
                var method = type.GetMethod(methodName, k_BindingFlags);
                if (method == null)
                    continue;

                if (TryInvokeEquipmentObjectMethod(method, component, parent, animator))
                    return true;
            }

            return false;
        }

        private bool TryInvokeEquipmentObjectMethod(
            MethodInfo method,
            Component component,
            Transform parent,
            Animator animator
        )
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                if (!TryBuildEquipmentObjectArgument(
                        parameters[i].ParameterType,
                        component,
                        parent,
                        animator,
                        out var argument
                    ))
                {
                    return false;
                }

                args[i] = argument;
            }

            try
            {
                method.Invoke(component, args);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[InfinityWardrobe] Failed to run '{method.Name}' on EquipmentObject: {exception.Message}",
                    component
                );
            }

            return false;
        }

        private bool TryBuildEquipmentObjectArgument(
            Type parameterType,
            Component component,
            Transform parent,
            Animator animator,
            out object argument
        )
        {
            argument = null;

            if (parameterType == typeof(GameObject))
            {
                argument = parent ? parent.gameObject : component.gameObject;
                return true;
            }

            if (parameterType == typeof(Transform))
            {
                argument = parent ? parent : component.transform;
                return true;
            }

            if (parameterType == typeof(Animator))
            {
                argument = animator ? animator : component.GetComponentInParent<Animator>();
                return argument != null;
            }

            if (parameterType == typeof(bool))
            {
                argument = true;
                return true;
            }

            if (parameterType == typeof(int))
            {
                argument = 0;
                return true;
            }

            if (parameterType == typeof(float))
            {
                argument = 0f;
                return true;
            }

            if (parameterType.IsEnum)
            {
                var values = Enum.GetValues(parameterType);
                argument = values.Length > 0 ? values.GetValue(0) : Activator.CreateInstance(parameterType);
                return true;
            }

            if (!parameterType.IsValueType)
                return true;

            if (parameterType.GetConstructor(Type.EmptyTypes) != null)
            {
                argument = Activator.CreateInstance(parameterType);
                return true;
            }

            return false;
        }

        private void RetargetSkinnedMeshes(GameObject instance, Transform parent)
        {
            if (!instance || !parent)
                return;

            var lookup = GetBoneLookup(parent);
            if (lookup == null || lookup.Count == 0)
                return;

            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                if (!renderer)
                    continue;

                var bones = renderer.bones;
                bool touched = false;

                for (int i = 0; i < bones.Length; i++)
                {
                    var bone = bones[i];
                    if (bone && lookup.TryGetValue(bone.name, out var mapped) && mapped != bone)
                    {
                        bones[i] = mapped;
                        touched = true;
                    }
                }

                if (renderer.rootBone
                    && lookup.TryGetValue(renderer.rootBone.name, out var newRoot)
                    && newRoot != renderer.rootBone)
                {
                    renderer.rootBone = newRoot;
                    touched = true;
                }

                if (touched)
                    renderer.bones = bones;
            }
        }

        private Dictionary<string, Transform> GetBoneLookup(Transform root)
        {
            if (!root)
                return null;

            if (m_boneLookups.TryGetValue(root, out var lookup) && lookup != null)
                return lookup;

            lookup = BuildBoneLookup(root);
            m_boneLookups[root] = lookup;
            return lookup;
        }

        private static Dictionary<string, Transform> BuildBoneLookup(Transform root)
        {
            var lookup = new Dictionary<string, Transform>(64, StringComparer.Ordinal);
            var stack = new Stack<Transform>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!current)
                    continue;

                lookup[current.name] = current;

                for (int i = 0; i < current.childCount; i++)
                    stack.Push(current.GetChild(i));
            }

            return lookup;
        }

        private void UpdateGroupActiveFlag(object group, IList objects)
        {
            if (group == null)
                return;

            bool anyEnabled = AnyObjectEnabled(objects);
            var flag = group.GetType().GetField("isActive", k_BindingFlags);
            SetBooleanField(flag, group, anyEnabled);
        }

        private bool AnyObjectEnabled(IList objects)
        {
            if (objects == null)
                return false;

            for (int i = 0; i < objects.Count; i++)
            {
                var entry = objects[i];
                if (entry == null)
                    continue;

                var renderField = entry.GetType().GetField("render", k_BindingFlags);
                if (renderField != null && GetBooleanField(renderField, entry))
                    return true;
            }

            return false;
        }

        private static bool SetBooleanField(FieldInfo field, object instance, bool value)
        {
            if (field == null || instance == null)
                return false;

            bool previous = GetBooleanField(field, instance);
            if (previous == value)
                return false;

            if (field.FieldType == typeof(bool))
                field.SetValue(instance, value);
            else if (field.FieldType == typeof(int))
                field.SetValue(instance, value ? 1 : 0);
            else
                field.SetValue(instance, value);

            return true;
        }

        private static bool GetBooleanField(FieldInfo field, object instance)
        {
            if (field == null || instance == null)
                return false;

            var value = field.GetValue(instance);
            return value switch
            {
                bool boolValue => boolValue,
                int intValue => intValue != 0,
                byte byteValue => byteValue != 0,
                _ => value != null && value.Equals(true)
            };
        }
    }
}
