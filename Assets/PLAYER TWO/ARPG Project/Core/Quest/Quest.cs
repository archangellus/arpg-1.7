using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Quest", menuName = "PLAYER TWO/ARPG Project/Quest/Quest")]
    public class Quest : ScriptableObject
    {
        public enum CompletingMode
        {
            ReachScene,
            Progress,
            Trigger,
        }

        [Header("Main Settings")]
        [Tooltip("The title of the Quest.")]
        public string title;

        [TextArea(5, 5)]
        [Tooltip("A description with details of the Quest lore.")]
        public string description;

        [Tooltip("A short description of the Quest objective.")]
        public string objective;

        [Tooltip("The amount of progression points required to complete the Quest.")]
        public int targetProgress;

        [Tooltip("The completing mode of the Quest.")]
        public CompletingMode completingMode;

        [Tooltip(
            "If true, the Quest will require the player to return to the Quest giver to complete it."
        )]
        public bool returnToGiver;

        [Header("Reward Settings")]
        [Tooltip("The amount of experience points gained by completing this Quest.")]
        public int experience;

        [Tooltip("The amount of coins gained by completing this Quest.")]
        public int coins;

        [Tooltip("The items gained by completing this Quest.")]
        public QuestItemReward[] items;

        [Header("Progression Settings")]
        [Tooltip(
            "The name of the destination scene used when the completing mode is 'Reach Scene.'"
        )]
        public string destinationScene;

        [Tooltip(
            "The key of the progress, e.g. name of the enemy, used when the completing mode is 'Progress.'"
        )]
        public string progressKey;

        [Header("Dialogue Settings")]
        [TextArea(5, 5)]
        [Tooltip("The dialogue to be shown when the Quest is started.")]
        public string startDialogue;

        [TextArea(5, 5)]
        [Tooltip("The dialogue to be shown when the Quest is completed.")]
        public string completeDialogue;

        /// <summary>
        /// Returns true if this Quest has any rewards.
        /// </summary>
        public bool hasReward => experience > 0 || coins > 0 || items.Length > 0;

        /// <summary>
        /// Returns true if the completing mode of this Quest is 'Reach Scene.'
        /// </summary>
        public bool IsReachScene() => completingMode == CompletingMode.ReachScene;

        /// <summary>
        /// Returns true if the completing mode of this Quest is 'Progress.'
        /// </summary>
        public bool IsProgress() => completingMode == CompletingMode.Progress;

        /// <summary>
        /// Returns true if the completing mode of this Quest is 'Trigger.'
        /// </summary>
        public bool IsTrigger() => completingMode == CompletingMode.Trigger;

        /// <summary>
        /// Returns true if a given scene name matches the Quest's destination scene name.
        /// </summary>
        /// <param name="scene">The name of the scene you want to compare.</param>
        public bool IsDestinationScene(string scene) => scene.CompareTo(destinationScene) == 0;

        /// <summary>
        /// Returns true if a given progress key matches the Quest's progress key.
        /// </summary>
        /// <param name="key">The progress key you want to compare.</param>
        public bool IsProgressKey(string key) => progressKey.CompareTo(key) == 0;

        /// <summary>
        /// Returns the formatted reward text.
        /// </summary>
        public string GetRewardText()
        {
            var text = "";

            if (!hasReward)
                return "None";
            if (experience > 0)
                text += $"{experience} exp";
            if (coins > 0)
                text += $"\n{coins} coins";

            foreach (var item in items)
                if (item.data)
                    text += $"\n{item.data.name}";

            return text;
        }
    }
}
