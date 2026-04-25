using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Stash")]
    public class Stash : Interactive
    {
        protected override void OnInteract(object other)
        {
            if (other is not Entity)
                return;

            GUIWindowsManager.instance.stashWindow.Show();
        }
    }
}
