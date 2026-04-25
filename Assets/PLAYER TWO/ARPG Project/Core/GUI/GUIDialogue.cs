using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(GUIWindow))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Dialogue")]
    public class GUIDialogue : MonoBehaviour
    {
        [Header("Element References")]
        [Tooltip("The Text component that displays the NPC's name.")]
        public Text npcNameText;

        [Tooltip("The Text component that displays the dialogue text.")]
        public Text dialogueText;

        [Tooltip("The Button that allows the Player to skip the dialogue.")]
        public Button skipButton;

        [Tooltip("The Button that allows the Player to continue after the dialogue ends.")]
        public Button continueButton;

        [Header("Auto Scroll Settings")]
        [Tooltip("The ScrollRect component that contains the dialogue text.")]
        public ScrollRect scrollRect;

        [Tooltip("The speed at which the dialogue text auto-scrolls.")]
        public float autoScrollSpeed;

        [Tooltip("The delay before the auto-scroll starts.")]
        public float scrollStartDelay;

        [Tooltip("The delay before the auto-scroll triggers the continue button.")]
        public float scrollEndDelay;

        protected bool m_initialized;

        protected GUIWindow m_window;
        protected UnityEvent m_onFinish;

        protected WaitForSeconds m_scrollStartDelay;
        protected WaitForSeconds m_scrollEndDelay;

        protected Coroutine m_autoScrollRoutine;

        /// <summary>
        /// Gets the GUIWindow component associated with this dialogue.
        /// </summary>
        public GUIWindow window
        {
            get
            {
                if (!m_window)
                    m_window = GetComponent<GUIWindow>();

                return m_window;
            }
        }

        protected virtual void OnEnable()
        {
            if (!m_initialized)
            {
                m_onFinish = new UnityEvent();
                m_scrollStartDelay = new WaitForSeconds(scrollStartDelay);
                m_scrollEndDelay = new WaitForSeconds(scrollEndDelay);
                skipButton.onClick.AddListener(Finish);
                continueButton.onClick.AddListener(Finish);
                window.onCloseWithReason.AddListener(HandleWindowClose);
                m_initialized = true;
            }

            skipButton.interactable = true;
            continueButton.interactable = false;
            m_autoScrollRoutine = StartCoroutine(AutoScrollRoutine());
        }

        /// <summary>
        /// Shows the dialogue window and fills it with the provided NPC name and dialogue text.
        /// The onFinish callback is invoked when the dialogue ends.
        /// </summary>
        /// <param name="npcName">The name of the NPC.</param>
        /// <param name="dialogue">The dialogue text to display.</param>
        /// <param name="onFinish">The callback to invoke when the dialogue ends.</param>
        public virtual void ShowAndFill(string npcName, string dialogue, UnityAction onFinish)
        {
            window.Show();
            Fill(npcName, dialogue, onFinish);
        }

        /// <summary>
        /// Fills the dialogue window with the provided NPC name and dialogue text.
        /// The onFinish callback is invoked when the dialogue ends.
        /// </summary>
        /// <param name="npcName">The name of the NPC.</param>
        /// <param name="dialogue">The dialogue text to display.</param>
        /// <param name="onFinish">The callback to invoke when the dialogue ends.</param>
        public virtual void Fill(string npcName, string dialogue, UnityAction onFinish)
        {
            npcNameText.text = npcName;
            dialogueText.text = dialogue;
            m_onFinish.RemoveAllListeners();
            m_onFinish.AddListener(onFinish);
        }

        /// <summary>
        /// Ends the dialogue and hides the window.
        /// </summary>
        public virtual void Finish()
        {
            ResetDialogue();
            window.Hide();
        }

        protected virtual void ResetDialogue()
        {
            if (m_autoScrollRoutine != null)
            {
                StopCoroutine(m_autoScrollRoutine);
                m_autoScrollRoutine = null;
            }

            scrollRect.verticalNormalizedPosition = 1;
        }

        protected virtual void HandleWindowClose(GUIWindow.CloseReason reason)
        {
            ResetDialogue();

            if (reason == GUIWindow.CloseReason.PlayerMove)
                return;

            m_onFinish.Invoke();
        }

        protected virtual IEnumerator AutoScrollRoutine()
        {
            var scrollPosition = 1f;

            yield return m_scrollStartDelay;

            while (scrollPosition > 0)
            {
                scrollPosition -= Time.deltaTime * autoScrollSpeed;
                scrollPosition = Mathf.Clamp01(scrollPosition);
                scrollRect.verticalNormalizedPosition = scrollPosition;
                yield return null;
            }

            yield return m_scrollEndDelay;

            skipButton.interactable = false;
            continueButton.interactable = true;
        }
    }
}
