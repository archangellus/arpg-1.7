using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Represents metadata about a plugin discovered by the <see cref="PluginManager"/>.
    /// </summary>
    public sealed class PluginDescriptor
    {
        private PluginDescriptor(Type type, string id, string displayName, string version,
            IReadOnlyList<string> dependencies, int loadOrder, IPlugin instance)
        {
            Type = type;
            Id = id;
            DisplayName = displayName;
            Version = version;
            Dependencies = dependencies;
            LoadOrder = loadOrder;
            Instance = instance;
        }

        /// <summary>
        /// Plugin type implementing <see cref="IPlugin"/>.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Human friendly display name.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Version string.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Dependencies expressed as other plugin identifiers.
        /// </summary>
        public IReadOnlyList<string> Dependencies { get; }

        /// <summary>
        /// Numeric load order. Smaller values load first.
        /// </summary>
        public int LoadOrder { get; }

        /// <summary>
        /// Indicates whether the plug-in is enabled based on editor configuration.
        /// </summary>
        public bool Enabled { get; internal set; } = true;

        /// <summary>
        /// Tracks whether <see cref="IPlugin.Initialize"/> has run for this descriptor.
        /// </summary>
        public bool Initialized { get; internal set; }

        /// <summary>
        /// Runtime plugin instance.
        /// </summary>
        public IPlugin Instance { get; private set; }

        internal static PluginDescriptor Create(Type type, IPlugin instance = null)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!typeof(IPlugin).IsAssignableFrom(type))
                throw new ArgumentException($"Type {type.FullName} does not implement {nameof(IPlugin)}.", nameof(type));

            var attribute = type.GetCustomAttribute<PluginAttribute>();

            string id = attribute?.Id;
            if (string.IsNullOrWhiteSpace(id))
                id = type.FullName;

            string displayName = attribute?.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = type.Name;

            string version = attribute?.Version;
            if (string.IsNullOrWhiteSpace(version))
                version = "1.0.0";

            var dependencies = attribute?.Dependencies ?? Array.Empty<string>();
            if (dependencies.Length > 0)
                dependencies = dependencies.Where(d => !string.IsNullOrWhiteSpace(d)).ToArray();

            int loadOrder = attribute?.LoadOrder ?? 0;

            return new PluginDescriptor(type, id, displayName, version, dependencies, loadOrder, instance);
        }

        internal void Attach(IPlugin instance)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }
    }
}
