using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Window")]
    public class GUIWindow : MonoBehaviour, IPointerDownHandler
    {
        public enum CloseReason
        {
            PlayerMove,
            Other,
        }

        [Header("Window Settings")]
        [Tooltip("If true, the Window will automatically close when the Player starts moving.")]
        public bool closeWhenPlayerMove;

        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when the Window is opened.")]
        public AudioClip openClip;

        [Tooltip("The Audio Clip that plays when the Window is closed.")]
        public AudioClip closeClip;

        [Header("Window Events")]
        public UnityEvent onOpen;
        public UnityEvent onClose;
        public UnityEvent<CloseReason> onCloseWithReason;

        protected float m_lastToggleTime;
        protected RectTransform m_rect;
        protected Entity m_player;

        protected const float k_toggleDelay = 0.1f;

        protected RectTransform rect
        {
            get
            {
                if (!m_rect)
                    m_rect = GetComponent<RectTransform>();

                return m_rect;
            }
        }

        /// <summary>
        /// Returns true if the Window is open.
        /// </summary>
        public bool isOpen => gameObject.activeSelf;

        protected GameAudio m_audio => GameAudio.instance;

        /// <summary>
        /// Toggles the Window visibility.
        /// </summary>
        public virtual void Toggle()
        {
            if (IsQuickToggle())
                return;

            rect.SetAsLastSibling();

            if (isOpen)
                Hide();
            else
                Show();
        }

        /// <summary>
        /// Shows the Window.
        /// </summary>
        public virtual void Show()
        {
            if (isOpen || IsQuickToggle())
                return;

            m_lastToggleTime = Time.unscaledTime;
            m_audio.PlayUiEffect(openClip);
            gameObject.SetActive(true);
            rect.SetAsLastSibling();
            OnOpen();
            onOpen.Invoke();
        }

        /// <summary>
        /// Hides the Window.
        /// </summary>
        public virtual void Hide() => Hide(CloseReason.Other);

        /// <summary>
        /// Hides the Window with a specific reason.
        /// </summary>
        public virtual void Hide(CloseReason reason)
        {
            if (!isOpen || IsQuickToggle())
                return;

            if (m_audio)
                m_audio.PlayUiEffect(closeClip);

            m_lastToggleTime = Time.unscaledTime;
            gameObject.SetActive(false);
            OnClose();
            onClose.Invoke();
            onCloseWithReason.Invoke(reason);
        }

        protected virtual bool IsQuickToggle() =>
            Time.unscaledTime - m_lastToggleTime < k_toggleDelay;

        protected virtual void OnOpen() { }

        protected virtual void OnClose() { }

        public void OnPointerDown(PointerEventData eventData) => rect.SetAsLastSibling();

        protected virtual void Start()
        {
            if (Level.instance)
                m_player = Level.instance.player;
        }

        protected virtual void LateUpdate()
        {
            if (closeWhenPlayerMove && m_player && m_player.lateralVelocity.sqrMagnitude > 0)
            {
                Hide(CloseReason.PlayerMove);
            }
        }
    }
}
