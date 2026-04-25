namespace PLAYERTWO.ARPGProject
{
    public abstract class ConsoleAction
    {
        /// <summary>
        /// Returns the command name for this console action.
        /// </summary>
        public virtual string Command =>
            GetType().Name.Replace(typeof(ConsoleAction).Name, string.Empty).ToLowerInvariant();

        /// <summary>
        /// Returns the usage string for this console action.
        /// </summary>
        public virtual string Usage => Command;

        /// <summary>
        /// Returns the description of this console action.
        /// </summary>
        public virtual string Description { get; }

        /// <summary>
        /// Returns the current Game instance.
        /// </summary>
        public virtual Game game => Game.instance;

        /// <summary>
        /// Returns the current Character Instance of this Game session.
        /// If no character is found, it will return null and output an error message.
        /// </summary>
        public virtual CharacterInstance currentCharacter
        {
            get
            {
                if (game.currentCharacter == null || !game.currentCharacter.entity)
                {
                    Console.LogError("No current player found.");
                    return null;
                }

                return game.currentCharacter;
            }
        }

        /// <summary>
        /// Executes the console action.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        public abstract void Execute(string[] args);
    }
}
