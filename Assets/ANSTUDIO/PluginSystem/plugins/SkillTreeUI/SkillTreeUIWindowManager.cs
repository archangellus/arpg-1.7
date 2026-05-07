using UnityEngine;

namespace PLAYERTWO.ARPGProject.SkillTreeUI
{
    /// <summary>
    /// Optional window manager for projects that want the Skill Tree UI to behave like
    /// GUIWindowsManager, without registering every other GUIWindow under the same canvas.
    /// Add this next to GUIWindowsManager and assign the Skill Tree GUIWindow, or leave it
    /// empty and it will search for the window that contains a SkillTreeUIController.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/Skill Tree UI Window Manager")]
    public class SkillTreeUIWindowManager : MonoBehaviour
    {
        [Tooltip("The GUIWindow that opens/closes the Skill Tree UI. If empty, the component searches children for a GUIWindow containing a SkillTreeUIController.")]
        public GUIWindow skillTreeWindow;

        [Tooltip("Optional Skill Tree controller reference. If empty, it is found automatically under this object.")]
        public SkillTreeUIController controller;

        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when opening the Skill Tree window.")]
        public AudioClip openClip;

        [Tooltip("The Audio Clip that plays when closing the Skill Tree window.")]
        public AudioClip closeClip;

        protected GameAudio m_audio => GameAudio.instance;

        private bool m_registeredAudioHooks;

        private void Reset()
        {
            AutoAssignReferences();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                AutoAssignReferences();
        }
#endif

        private void Awake()
        {
            AutoAssignReferences();
        }

        private void Start()
        {
            RegisterAudioHooks();
        }

        private void OnDestroy()
        {
            UnregisterAudioHooks();
        }

        public SkillTreeUIController GetController()
        {
            AutoAssignReferences();
            return controller;
        }

        public GUIWindow GetWindow()
        {
            AutoAssignReferences();
            return skillTreeWindow;
        }

        public void AutoAssignReferences()
        {
            if (!controller)
                controller = GetComponentInChildren<SkillTreeUIController>(true);

            if (!skillTreeWindow && controller)
                skillTreeWindow = controller.GetComponentInParent<GUIWindow>(true);

            if (!skillTreeWindow)
                skillTreeWindow = FindSkillTreeWindowInChildren();
        }

        private GUIWindow FindSkillTreeWindowInChildren()
        {
            var windows = GetComponentsInChildren<GUIWindow>(true);
            foreach (var window in windows)
            {
                if (!window)
                    continue;

                if (window.GetComponentInChildren<SkillTreeUIController>(true))
                    return window;
            }

            return null;
        }

        private void RegisterAudioHooks()
        {
            if (m_registeredAudioHooks)
                return;

            AutoAssignReferences();

            if (!skillTreeWindow)
            {
                Debug.LogWarning("[SkillTreeUI] SkillTreeUIWindowManager could not find a Skill Tree GUIWindow.", this);
                return;
            }

            skillTreeWindow.onOpen.AddListener(PlayOpenClip);
            skillTreeWindow.onClose.AddListener(PlayCloseClip);
            m_registeredAudioHooks = true;
        }

        private void UnregisterAudioHooks()
        {
            if (!m_registeredAudioHooks || !skillTreeWindow)
                return;

            skillTreeWindow.onOpen.RemoveListener(PlayOpenClip);
            skillTreeWindow.onClose.RemoveListener(PlayCloseClip);
            m_registeredAudioHooks = false;
        }

        private void PlayOpenClip()
        {
            if (m_audio && openClip)
                m_audio.PlayUiEffect(openClip);
        }

        private void PlayCloseClip()
        {
            if (m_audio && closeClip)
                m_audio.PlayUiEffect(closeClip);
        }
    }
}
