using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class ConsoleHistory
    {
        protected List<string> m_history = new();
        protected int m_historyIndex = -1;

        /// <summary>
        /// Adds a command to the history.
        /// </summary>
        /// <param name="command">The command to add.</param>
        public void AddCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return;

            m_history.Add(command);
            m_historyIndex = -1;
        }

        /// <summary>
        /// Gets the last command from the history.
        /// </summary>
        /// <returns>The last command or an empty string if no commands exist.</returns>
        public string GetLastCommand()
        {
            if (m_history.Count == 0)
                return string.Empty;

            if (m_historyIndex < 0)
                m_historyIndex = m_history.Count;

            m_historyIndex--;
            m_historyIndex = Mathf.Max(m_historyIndex, 0);
            return m_history[m_historyIndex];
        }

        /// <summary>
        /// Gets the next command in the history.
        /// </summary>
        /// <returns>The next command or an empty string if no more commands exist.</returns>
        public string GetNextCommand()
        {
            if (m_history.Count == 0 || m_historyIndex < 0)
                return string.Empty;

            m_historyIndex++;
            m_historyIndex = Mathf.Min(m_historyIndex, m_history.Count);

            if (m_historyIndex == m_history.Count)
                return string.Empty;

            return m_history[m_historyIndex];
        }
    }
}
