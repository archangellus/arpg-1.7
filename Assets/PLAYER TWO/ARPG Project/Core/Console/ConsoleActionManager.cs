using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class ConsoleActionManager
    {
        protected Dictionary<string, ConsoleAction> m_actions = new();

        public ConsoleActionManager()
        {
            RegisterAllActions();
        }

        /// <summary>
        /// Checks if an action with the given key exists in the action manager.
        /// </summary>
        /// <param name="key">The command key to check for.</param>
        /// <returns>True if the action exists, otherwise false.</returns>
        public virtual bool HasAction(string key) => m_actions.ContainsKey(key);

        /// <summary>
        /// Returns all registered console actions.
        /// </summary>
        public virtual ConsoleAction[] GetAllActions() => m_actions.Values.ToArray();

        /// <summary>
        /// Executes the console action associated with the given key.
        /// </summary>
        /// <param name="key">The command key to execute.</param>
        /// <param name="args">The command arguments to pass to the action.</param>
        public virtual void Execute(string key, string[] args)
        {
            try
            {
                if (m_actions.TryGetValue(key, out var action))
                    ExecuteAction(action, args);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"Error executing command '{key}': {exception.Message}");
            }
        }

        protected virtual void ExecuteAction(ConsoleAction action, string[] args)
        {
            try
            {
                action.Execute(args);
            }
            catch
            {
                Console.LogError(
                    $"Error executing command '{action.Command}'. Maybe the arguments are incorrect?"
                );
                Console.LogWarning("Usage: " + action.Usage);
            }
        }

        protected virtual void RegisterAction(ConsoleAction action)
        {
            if (action == null)
                return;

            var command = action.Command;

            if (!m_actions.ContainsKey(command))
                m_actions[command] = action;
        }

        protected virtual void RegisterAllActions()
        {
            var actionType = typeof(ConsoleAction);
            var actions = System
                .AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type =>
                    actionType.IsAssignableFrom(type)
                    && !type.IsAbstract
                    && type.GetConstructor(System.Type.EmptyTypes) != null
                )
                .Select(type => (ConsoleAction)System.Activator.CreateInstance(type));

            foreach (var action in actions)
                RegisterAction(action);
        }
    }
}
