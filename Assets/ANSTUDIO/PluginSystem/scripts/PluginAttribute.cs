using System;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Provides metadata used by the <see cref="PluginManager"/> when loading a plugin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PluginAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="PluginAttribute"/>.
        /// </summary>
        /// <param name="id">Unique identifier for the plugin.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or whitespace.</exception>
        public PluginAttribute(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Plugin id cannot be null or whitespace.", nameof(id));

            Id = id;
        }

        /// <summary>
        /// Unique identifier for the plugin.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Human friendly name for the plugin.
        /// Defaults to the class name when not provided.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Version string for the plugin.
        /// Defaults to <c>1.0.0</c> when not provided.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Optional list of plugin identifiers that must be initialized first.
        /// </summary>
        public string[] Dependencies { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Optional numeric load order used when dependencies do not define the order.
        /// Lower numbers are loaded first.
        /// </summary>
        public int LoadOrder { get; set; }
    }
}
