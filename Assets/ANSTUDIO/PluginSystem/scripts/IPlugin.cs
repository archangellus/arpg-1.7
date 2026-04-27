using System;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Base interface for all plugins.
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Called when the game starts and the plugin is loaded.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Called when the game is shutting down.
        /// </summary>
        void Shutdown();
    }
}