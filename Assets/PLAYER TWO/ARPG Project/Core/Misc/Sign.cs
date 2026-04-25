using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Sign")]
    public class Sign : Interactive
    {
        [Tooltip("The title of this Sign.")]
        public string title = "New Sign Title";

        [TextArea(5, 5)]
        [Tooltip("The tex of this Sign.")]
        public string text = "This is the text description.";

        protected GUIWindowsManager m_manager => GUIWindowsManager.instance;

        protected override void OnInteract(object other)
        {
            if (other is not Entity)
                return;

            ((Entity)other).StandStill();
            m_manager.informationWindow.Show();
            m_manager.GetInformation().SetInformation(title, text);
        }
    }
}
