using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Skill Slot")]
    public class GUISkillSlot : GUISlot, IDropHandler
    {
        public event System.Action<Skill> OnDropSKill;

        [Tooltip("A reference to the Image component used as the selection outline.")]
        public Image selection;

        [Tooltip("A reference to the Image component used as the Skill cool down image.")]
        public Image coolDownImage;

        [Header("Slot Events")]
        public UnityEvent onIconClick;
        public UnityEvent onIconDoubleClick;

        protected GUISkillIcon m_icon;
        protected Coroutine m_coolDownRoutine;

        /// <summary>
        /// Returns the current skill on this GUI Skill Slot.
        /// </summary>
        public Skill skill { get; protected set; }

        /// <summary>
        /// Returns the cool down counter.
        /// </summary>
        public float coolDown { get; set; }

        /// <summary>
        /// Returns the GUI Skill Icon children of this slot.
        /// </summary>
        public GUISkillIcon icon
        {
            get
            {
                if (!m_icon)
                {
                    m_icon = GetComponentInChildren<GUISkillIcon>();
                }

                return m_icon;
            }
        }

        /// <summary>
        /// Returns true if this Skill Slot is selected.
        /// </summary>
        public bool selected => selection.enabled;

        /// <summary>
        /// Sets a given Skill on this slot.
        /// </summary>
        /// <param name="skill">The Skill you want to set.</param>
        public virtual void SetSkill(
            Skill skill,
            bool draggable = false,
            float remainingCoolDown = 0
        )
        {
            this.skill = skill;
            icon.draggable = draggable;
            SetIcon(skill.SafeGet(s => s.icon));
            Visible(skill);

            if (skill && remainingCoolDown > 0)
                StartCoolDown(skill.coolDown, remainingCoolDown);
            else
                StopCoolDown();
        }

        /// <summary>
        /// Sets the sprite of the icon on this slot.
        /// </summary>
        /// <param name="sprite">The sprite you want to set.</param>
        public virtual void SetIcon(Sprite sprite) => icon.image.sprite = sprite;

        /// <summary>
        /// Sets the visibility of the icon.
        /// </summary>
        /// <param name="value">If true, the icon is visible.</param>
        public virtual void Visible(bool value) => icon.image.enabled = value;

        /// <summary>
        /// Selects this slot highlighting it.
        /// </summary>
        /// <param name="value">If true, the slot will be highlighted.</param>
        public virtual void Select(bool value) => selection.enabled = value;

        /// <summary>
        /// Starts the cool down counter.
        /// </summary>
        /// <param name="duration">The duration of the cool down.</param>
        /// <param name="remaining">The remaining time of the cool down.</param>
        public virtual void StartCoolDown(float duration, float remaining = 0)
        {
            if (m_coolDownRoutine != null)
                StopCoroutine(m_coolDownRoutine);

            m_coolDownRoutine = StartCoroutine(CoolDownRoutine(duration, remaining));
        }

        /// <summary>
        /// Stops the cool down counter.
        /// </summary>
        public virtual void StopCoolDown()
        {
            if (m_coolDownRoutine != null)
                StopCoroutine(m_coolDownRoutine);

            coolDownImage.fillAmount = 0;
        }

        protected IEnumerator CoolDownRoutine(float coolDown, float remaining = 0)
        {
            var duration = remaining > 0 ? remaining : coolDown;
            coolDownImage.fillAmount = 1;

            while (duration > 0)
            {
                duration -= Time.deltaTime;
                coolDownImage.fillAmount = duration / coolDown;
                yield return null;
            }

            coolDownImage.fillAmount = 0;
        }

        protected virtual void Start()
        {
            icon.onClick.AddListener(onIconClick.Invoke);
            icon.onDoubleClick.AddListener(onIconDoubleClick.Invoke);
        }

        protected virtual void OnEnable() => Select(false);

        public void OnDrop(PointerEventData eventData)
        {
            if (
                eventData.pointerDrag
                && eventData.pointerDrag.TryGetComponent(out GUISkillIcon skillIcons)
            )
            {
                OnDropSKill?.Invoke(skillIcons.GetSkill());
            }
        }
    }
}
