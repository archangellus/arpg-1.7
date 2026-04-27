using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Discovers and manages plugins implementing <see cref="IPlugin"/>, honouring
    /// dependency metadata and providing runtime access to plugin descriptors.
    /// </summary>
    public static class PluginManager
    {
        private static readonly List<IPlugin> s_plugins = new();
        private static readonly List<PluginDescriptor> s_descriptors = new();
        private static readonly Dictionary<string, PluginDescriptor> s_descriptorLookup =
            new(StringComparer.OrdinalIgnoreCase);

        private static PluginStateUtility.PluginStateCollection s_pluginStates;
        private static Dictionary<string, bool> s_pluginEnabledLookup;

        private static bool s_initialized;

        /// <summary>
        /// Initializes all discovered plugins.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (s_initialized)
                return;

            s_initialized = true;
            s_pluginStates = PluginStateUtility.LoadStates();
            s_pluginEnabledLookup = s_pluginStates?.states?.ToDictionary(
                s => s.plugin,
                s => s.enabled,
                StringComparer.OrdinalIgnoreCase);
            LoadPlugins();

            foreach (var descriptor in s_descriptors)
            {
                descriptor.Enabled = IsPluginEnabled(descriptor);
                Debug.Log($"[PluginManager] Plugin '{descriptor.DisplayName}' is {(descriptor.Enabled ? "enabled" : "disabled")}.");

                if (!descriptor.Enabled)
                    continue;

                try
                {
                    descriptor.Instance.Initialize();
                    descriptor.Initialized = true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PluginManager] Failed to initialize {descriptor.DisplayName}: {ex}");
                }
            }

            Application.quitting += Shutdown;
        }

        /// <summary>
        /// Loads all plugins from the current AppDomain that implement <see cref="IPlugin"/>.
        /// </summary>
        private static void LoadPlugins()
        {
            var descriptors = DiscoverDescriptors();
            var ordered = OrderDescriptors(descriptors);

            foreach (var descriptor in ordered)
            {
                if (s_descriptorLookup.TryGetValue(descriptor.Id, out var existing))
                {
                    if (existing.Type != descriptor.Type)
                        Debug.LogWarning($"[PluginManager] Plugin '{descriptor.Id}' is already registered. Skipping duplicate definition on {descriptor.Type.FullName}.");
                    continue;
                }

                descriptor.Enabled = IsPluginEnabled(descriptor);

                try
                {
                    if (descriptor.Instance == null)
                    {
                        if (descriptor.Type.GetConstructor(Type.EmptyTypes) == null)
                        {
                            Debug.LogError($"[PluginManager] {descriptor.Type.FullName} is missing a parameterless constructor and cannot be instantiated automatically.");
                            continue;
                        }

                        var instance = (IPlugin)Activator.CreateInstance(descriptor.Type);
                        descriptor.Attach(instance);
                    }

                    RegisterDescriptor(descriptor);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PluginManager] Failed to create {descriptor.Type.FullName}: {ex}");
                }
            }
        }

        /// <summary>
        /// Safely retrieves types from an assembly, handling potential ReflectionTypeLoadExceptions.
        /// </summary>
        private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        /// <summary>
        /// Shuts down all plugins and clears the plugin list.
        /// </summary>
        private static void Shutdown()
        {
            for (int i = s_descriptors.Count - 1; i >= 0; i--)
            {
                if (!s_descriptors[i].Enabled || !s_descriptors[i].Initialized)
                    continue;

                try
                {
                    s_descriptors[i].Instance.Shutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PluginManager] Failed to shutdown {s_descriptors[i].DisplayName}: {ex}");
                }
            }

            s_plugins.Clear();
            s_descriptors.Clear();
            s_descriptorLookup.Clear();
            s_initialized = false;
        }

        /// <summary>
        /// Registers a plugin instance manually.
        /// </summary>
        public static void Register(IPlugin plugin)
        {
            if (plugin == null || s_plugins.Contains(plugin))
                return;

            var descriptor = PluginDescriptor.Create(plugin.GetType(), plugin);

            descriptor.Enabled = IsPluginEnabled(descriptor);

            if (s_descriptorLookup.ContainsKey(descriptor.Id))
            {
                Debug.LogWarning($"[PluginManager] Plugin '{descriptor.Id}' is already registered. Skipping duplicate instance.");
                return;
            }

            RegisterDescriptor(descriptor);

            if (s_initialized && descriptor.Enabled)
            {
                try
                {
                    plugin.Initialize();
                    descriptor.Initialized = true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PluginManager] Failed to initialize {descriptor.DisplayName}: {ex}");
                }
            }
        }

        /// <summary>
        /// Returns all loaded plugins implementing the specified type.
        /// </summary>
        public static IEnumerable<T> GetPlugins<T>() where T : class, IPlugin =>
            s_plugins.OfType<T>();

        /// <summary>
        /// Returns metadata for all discovered plugins.
        /// </summary>
        public static IEnumerable<PluginDescriptor> GetDescriptors() => s_descriptors.ToArray();

        /// <summary>
        /// Attempts to retrieve plugin metadata by identifier.
        /// </summary>
        public static bool TryGetDescriptor(string id, out PluginDescriptor descriptor) =>
            s_descriptorLookup.TryGetValue(id, out descriptor);

        
        /// <summary>
        /// Enables or disables a plugin at runtime by plugin id, display name, type name, or folder-style key.
        /// Persists the state to <c>plugin_states.json</c>.
        /// </summary>
        public static bool SetPluginEnabled(string pluginKey, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(pluginKey))
                return false;

            if (!TryResolveDescriptor(pluginKey, out var descriptor))
                return false;

            PersistPluginState(descriptor, enabled);

            if (!s_initialized)
                return true;

            if (descriptor.Enabled == enabled)
                return true;

            descriptor.Enabled = enabled;

            try
            {
                if (enabled)
                {
                    descriptor.Instance.Initialize();
                    descriptor.Initialized = true;
                    if (!s_plugins.Contains(descriptor.Instance))
                        s_plugins.Add(descriptor.Instance);
                }
                else
                {
                    if (descriptor.Initialized)
                    {
                        descriptor.Instance.Shutdown();
                        descriptor.Initialized = false;
                    }

                    s_plugins.Remove(descriptor.Instance);
                }

                EventBus.Publish(new PluginToggleEvent(descriptor.Id, enabled));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PluginManager] Failed to {(enabled ? "enable" : "disable")} {descriptor.DisplayName}: {ex}");
                return false;
            }
        }
    

        private static List<PluginDescriptor> DiscoverDescriptors()
        {
            var pluginType = typeof(IPlugin);

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetTypesSafe)
                .Where(t => pluginType.IsAssignableFrom(t) && !t.IsAbstract)
                .Select(type => PluginDescriptor.Create(type))
                .ToList();
        }

        private static IEnumerable<PluginDescriptor> OrderDescriptors(List<PluginDescriptor> descriptors)
        {
            if (descriptors.Count == 0)
                return descriptors;

            var comparer = StringComparer.OrdinalIgnoreCase;
            var descriptorLookup = descriptors.ToDictionary(d => d.Id, comparer);
            var dependencyGraph = descriptors.ToDictionary(
                d => d.Id,
                d => new HashSet<string>(d.Dependencies.Where(descriptorLookup.ContainsKey), comparer),
                comparer);
            var dependents = descriptors.ToDictionary(d => d.Id, _ => new List<PluginDescriptor>(), comparer);
            var incomingEdges = dependencyGraph.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count, comparer);

            foreach (var descriptor in descriptors)
            {
                foreach (var dependencyId in descriptor.Dependencies)
                {
                    if (!descriptorLookup.ContainsKey(dependencyId))
                    {
                        Debug.LogWarning($"[PluginManager] Plugin '{descriptor.DisplayName}' depends on '{dependencyId}' but it was not found.");
                        continue;
                    }

                    dependents[dependencyId].Add(descriptor);
                }
            }

            var ordered = new List<PluginDescriptor>(descriptors.Count);
            var ready = new List<PluginDescriptor>(descriptors.Where(d => incomingEdges[d.Id] == 0));
            ready.Sort(CompareLoadOrder);

            while (ready.Count > 0)
            {
                var descriptor = ready[0];
                ready.RemoveAt(0);
                ordered.Add(descriptor);

                foreach (var dependent in dependents[descriptor.Id])
                {
                    if (dependencyGraph[dependent.Id].Remove(descriptor.Id))
                    {
                        incomingEdges[dependent.Id]--;
                        if (incomingEdges[dependent.Id] == 0)
                        {
                            ready.Add(dependent);
                            ready.Sort(CompareLoadOrder);
                        }
                    }
                }
            }

            if (ordered.Count != descriptors.Count)
            {
                var remaining = descriptors.Except(ordered).OrderBy(d => d, Comparer<PluginDescriptor>.Create(CompareLoadOrder)).ToList();
                foreach (var descriptor in remaining)
                {
                    Debug.LogError($"[PluginManager] Detected circular dependency involving '{descriptor.DisplayName}'. Loading order will fall back to declared priority.");
                }

                ordered.AddRange(remaining);
            }

            return ordered;
        }

        private static int CompareLoadOrder(PluginDescriptor left, PluginDescriptor right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int orderComparison = left.LoadOrder.CompareTo(right.LoadOrder);
            if (orderComparison != 0)
                return orderComparison;

            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
        }

        private static void RegisterDescriptor(PluginDescriptor descriptor)
        {
            if (descriptor.Instance == null)
            {
                Debug.LogError($"[PluginManager] Attempted to register plugin '{descriptor.Id}' without an instantiated instance.");
                return;
            }

            s_descriptorLookup[descriptor.Id] = descriptor;
            s_descriptors.Add(descriptor);
            if (descriptor.Enabled)
                s_plugins.Add(descriptor.Instance);
        }

        private static bool IsPluginEnabled(PluginDescriptor descriptor)
        {
            if (descriptor == null)
                return true;

            if (s_pluginStates == null)
            {
                s_pluginStates = PluginStateUtility.LoadStates();
                s_pluginEnabledLookup = s_pluginStates?.states?.ToDictionary(
                    s => s.plugin,
                    s => s.enabled,
                    StringComparer.OrdinalIgnoreCase);
            }

            if (s_pluginEnabledLookup != null &&
                s_pluginEnabledLookup.TryGetValue(descriptor.Id, out var enabledById))
                return enabledById;

            if (s_pluginStates?.states != null)
            {
                var match = s_pluginStates.states.FirstOrDefault(s =>
                    KeysMatch(s.plugin, descriptor.Id) ||
                    KeysMatch(s.plugin, descriptor.DisplayName) ||
                    KeysMatch(s.plugin, descriptor.Type.Name));

                if (match != null)
                    return match.enabled;
            }

            return true;
        }

        private static bool TryResolveDescriptor(string pluginKey, out PluginDescriptor descriptor)
        {
            descriptor = s_descriptors.FirstOrDefault(d =>
                KeysMatch(pluginKey, d.Id) ||
                KeysMatch(pluginKey, d.DisplayName) ||
                KeysMatch(pluginKey, d.Type.Name) ||
                KeysMatch(pluginKey, RemovePluginSuffix(d.Type.Name)));

            return descriptor != null;
        }

        private static void PersistPluginState(PluginDescriptor descriptor, bool enabled)
        {
            if (descriptor == null)
                return;

            s_pluginStates ??= PluginStateUtility.LoadStates();
            s_pluginStates.states ??= new List<PluginStateUtility.PluginState>();
            s_pluginEnabledLookup ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            var existing = s_pluginStates.states.FirstOrDefault(s => KeysMatch(s.plugin, descriptor.Id));
            if (existing == null)
            {
                existing = new PluginStateUtility.PluginState { plugin = descriptor.Id, enabled = enabled };
                s_pluginStates.states.Add(existing);
            }
            else
            {
                existing.plugin = descriptor.Id;
                existing.enabled = enabled;
            }

            s_pluginEnabledLookup[descriptor.Id] = enabled;
            PluginStateUtility.SaveStates(s_pluginStates);
        }

        private static bool KeysMatch(string left, string right) =>
            string.Equals(NormalizeKey(left), NormalizeKey(right), StringComparison.Ordinal);

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return new string(value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static string RemovePluginSuffix(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return string.Empty;

            const string suffix = "Plugin";
            return typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? typeName[..^suffix.Length]
                : typeName;
        }
    }
}

