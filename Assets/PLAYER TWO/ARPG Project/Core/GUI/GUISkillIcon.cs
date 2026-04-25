using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(CanvasGroup), typeof(Image))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Skill Icon")]
    public class GUISkillIcon
        : MonoBehaviour,
            IPointerEnterHandler,
            IPointerExitHandler,
            IPointerDownHandler,
            IPointerUpHandler,
            IBeginDragHandler,
            IEndDragHandler,
            IDragHandler,
            IDeselectHandler
    {
        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when picking the icon.")]
        public AudioClip pickAudio;

        [Tooltip("The Audio Clip that plays when placing the icon.")]
        public AudioClip placeAudio;

        [Header("Icon Events")]
        public UnityEvent onClick;
        public UnityEvent onDoubleClick;

        protected Transform m_initialParent;
        protected Vector3 m_initialPosition;

        protected float m_lastClickTime;
        protected bool m_inspecting;

        protected Canvas m_canvas;
        protected CanvasGroup m_group;
        protected GUISkillSlot m_slot;
        protected Image m_image;

        protected const float k_doubleClickThreshold = 0.3f;

        protected GameAudio m_audio => GameAudio.instance;

        /// <summary>
        /// Returns the Image component used to display the icon sprite.
        /// </summary>
        public Image image
        {
            get
            {
                if (!m_image)
                    m_image = GetComponent<Image>();

                return m_image;
            }
        }

        /// <summary>
        /// If true, the Player can drag this icon.
        /// </summary>
        public bool draggable { get; set; }

        public void OnPointerEnter(PointerEventData _)
        {
#if UNITY_STANDALONE || UNITY_WEBGL
            m_inspecting = true;
            GUISkillInspector.instance.SafeCall(i => i.Show(m_slot.skill));
#endif
        }

        public void OnPointerExit(PointerEventData _)
        {
#if UNITY_STANDALONE || UNITY_WEBGL
            m_inspecting = false;
            GUISkillInspector.instance.SafeCall(i => i.Hide());
#endif
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Time.time - m_lastClickTime < k_doubleClickThreshold)
            {
                onDoubleClick.Invoke();
            }

            m_lastClickTime = Time.time;
            onClick.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
#if UNITY_ANDROID || UNITY_IOS
            m_inspecting = true;
            GUISkillInspector.instance.Hide();
            GUISkillInspector.instance.Show(m_slot.skill);
            GUISkillInspector.instance.SetPositionRelativeTo((RectTransform)transform);
            EventSystem.current.SetSelectedGameObject(gameObject);
#endif
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!draggable)
                return;

            m_group.alpha = 0.5f;
            m_group.blocksRaycasts = false;
            m_audio.PlayUiEffect(placeAudio);
            transform.SetParent(m_canvas.transform);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!draggable)
                return;

            transform.position = EntityInputs.GetPointerPosition();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!draggable)
                return;

            m_group.alpha = 1f;
            m_group.blocksRaycasts = true;
            m_audio.PlayUiEffect(pickAudio);
            transform.SetParent(m_initialParent);
            transform.localPosition = m_initialPosition;
        }

        public virtual void OnDeselect(BaseEventData _)
        {
#if UNITY_ANDROID || UNITY_IOS
            GUISkillInspector.instance.Hide();
#endif
        }

        /// <summary>
        /// Returns the Skill this icon represents.
        /// </summary>
        public virtual Skill GetSkill() => m_slot.skill;

        protected virtual void Start()
        {
            m_canvas = GetComponentInParent<Canvas>();
            m_group = GetComponent<CanvasGroup>();
            m_slot = GetComponentInParent<GUISkillSlot>();
            m_initialParent = transform.parent;
            m_initialPosition = transform.localPosition;
        }

        protected virtual void OnDisable()
        {
            if (!m_inspecting)
                return;

            m_inspecting = false;
            GUISkillInspector.instance.SafeCall(i => i.Hide());
        }
    }
}
