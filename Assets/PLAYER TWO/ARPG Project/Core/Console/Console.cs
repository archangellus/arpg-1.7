using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Console/Console")]
    public class Console : Singleton<Console>
    {
        [Header("Console Settings")]
        [Tooltip("Enable console in build mode.")]
        public bool openInBuild;

        [Header("UI Elements")]
        [Tooltip("Input field for console commands.")]
        public TMP_InputField inputField;

        [Tooltip("Text component to display console output.")]
        public TMP_Text outputText;

        [Tooltip("Panel that contains the console UI.")]
        public GameObject consolePanel;

        [Tooltip("Scroll Rect for the console output text.")]
        public ScrollRect outputScrollRect;

        [Space(10)]
        public UnityEvent onConsoleOpened;
        public UnityEvent onConsoleClosed;

        protected ConsoleActionManager m_actionManager = new();
        protected ConsoleHistory m_history = new();

        protected virtual void Start()
        {
            Clear();
            LogSuccess("Welcome to the ARPG Console! Type 'help' for commands.");
            inputField.onSubmit.AddListener(OnInputSubmitted);
        }

        protected virtual void Update()
        {
            if (!openInBuild && !Application.isEditor)
                return;

            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
                Toggle();

            if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                inputField.text = m_history.GetLastCommand();
                inputField.MoveToEndOfLine(false, true);
            }
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                inputField.text = m_history.GetNextCommand();
                inputField.MoveToEndOfLine(false, true);
            }
        }

        /// <summary>
        /// Toggles the console visibility.
        /// </summary>
        public static void Toggle()
        {
            instance.consolePanel.SetActive(!instance.consolePanel.activeSelf);

            if (instance.consolePanel.activeSelf)
            {
                instance.inputField.ActivateInputField();
                instance.inputField.Select();
                instance.onConsoleOpened.Invoke();
            }
            else
            {
                instance.inputField.text = string.Empty;
                instance.inputField.DeactivateInputField();
                instance.onConsoleClosed.Invoke();
            }
        }

        /// <summary>
        /// Clears the console output.
        /// </summary>
        public static void Clear() => instance.outputText.text = string.Empty;

        /// <summary>
        /// Logs a message to the console output.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void Log(string message) => instance.outputText.text += $"{message}\n";

        /// <summary>
        /// Logs a message to the console output with a specific color.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="color">The color to use for the message.</param>
        public static void Log(string message, Color color) =>
            instance.outputText.text +=
                $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>\n";

        /// <summary>
        /// Logs an error message to the console output.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogError(string message) => Log(message, Color.red);

        /// <summary>
        /// Logs a warning message to the console output.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogWarning(string message) => Log(message, Color.yellow);

        /// <summary>
        /// Logs a success message to the console output.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogSuccess(string message) => Log(message, Color.green);

        protected virtual void OnInputSubmitted(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;

            Log($"> {input}");
            m_history.AddCommand(input);
            ProcessCommand(input.Trim());
            inputField.text = string.Empty;
            StartCoroutine(RefreshConsole());
        }

        protected virtual void ProcessCommand(string command)
        {
            var args = command.Split(' ');
            var cmd = args[0].ToLower();

            switch (cmd)
            {
                case "help":
                    Help();
                    break;
                case "clear":
                    Clear();
                    break;
                default:
                    if (m_actionManager.HasAction(cmd))
                        m_actionManager.Execute(cmd, args);
                    else
                    {
                        LogError($"Unknown command: {cmd}");
                        LogWarning("Type 'help' for a list of commands.");
                    }
                    break;
            }
        }

        protected virtual void Help()
        {
            Log("Available commands:");
            Log($"\tclear");
            foreach (var action in m_actionManager.GetAllActions())
                Log($"\t{action.Usage}");
        }

        protected virtual IEnumerator RefreshConsole()
        {
            yield return null;
            inputField.ActivateInputField();
            inputField.Select();
            outputScrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
