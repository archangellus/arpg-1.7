using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Game/Game Pause")]
    public class GamePause : Singleton<GamePause>
    {
        /// <summary>
        /// Returns true if the Game is paused.
        /// </summary>
        public bool isPaused => Time.timeScale == 0;

        /// <summary>
        /// Sets the pause value of the game.
        /// </summary>
        /// <param name="value">If true, the game will be paused.</param>
        public virtual void Pause(bool value) => Time.timeScale = value ? 0 : 1;
    }
}
