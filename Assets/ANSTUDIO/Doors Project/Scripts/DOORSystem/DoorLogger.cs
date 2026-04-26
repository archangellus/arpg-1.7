using UnityEngine;

namespace ProjectDoors
{
    /// <summary>
    /// A simple static class for centralized logging of Door-related messages.
    /// </summary>
    public static class DoorLogger
    {
        /// <summary>
        /// Generic info log (same as Debug.Log).
        /// </summary>
        public static void Log(string message, Object context = null)
        {
            Debug.Log($"[ProjectDoors] {message}", context);
        }

        /// <summary>
        /// Warning log (same as Debug.LogWarning).
        /// </summary>
        public static void LogWarning(string message, Object context = null)
        {
            Debug.LogWarning($"[ProjectDoors] {message}", context);
        }

        /// <summary>
        /// Error log (same as Debug.LogError).
        /// </summary>
        public static void LogError(string message, Object context = null)
        {
            Debug.LogError($"[ProjectDoors] {message}", context);
        }
    }
}
