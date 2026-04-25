using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Game/Game Controller")]
    public class GameController : MonoBehaviour
    {
        public virtual void Pause(bool value) => GamePause.instance.Pause(value);
        public virtual void Save() => GameSave.instance.Save();
        public virtual void LoadScene(string scene) => GameScenes.instance.LoadScene(scene);
        public virtual void ExitGame() => Game.instance.ExitGame();
    }
}
