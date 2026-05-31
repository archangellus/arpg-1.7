using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Inventory Slot")]
    public class GUIInventorySlot : MonoBehaviour
    {
        public enum State
        {
            Free,
            Occupied,
        }

        [Header("Color Settings")]
        [Tooltip("The regular color of the slot.")]
        public Color regular = Color.white;

        [Tooltip("The color of the slot if it's occupied.")]
        public Color occupied = GameColors.HalfBack;

        [Tooltip("The color when hovering an item that can't be placed.")]
        public Color invalid = GameColors.LightRed;

        [Tooltip("The color when hovering an item that can be placed.")]
        public Color valid = GameColors.LightBlue;

        protected State m_state;
        protected Image m_image;

        protected Image image
        {
            get
            {
                if (m_image == null)
                    m_image = GetComponent<Image>();

                return m_image;
            }
        }

        /// <summary>
        /// Sets this slot as free.
        /// </summary>
        public virtual void SetFree() => m_state = State.Free;

        /// <summary>
        /// Sets this slot as occupied.
        /// </summary>
        public virtual void SetOccupied() => m_state = State.Occupied;

        /// <summary>
        /// Resets the color of the slot to match its current state.
        /// </summary>
        public virtual void Reset()
        {
            image.color = m_state == State.Occupied ? occupied : regular;
        }

        /// <summary>
        /// Changes the slot color to valid.
        /// </summary>
        public virtual void HighlightValid() => image.color = valid;

        /// <summary>
        /// Changes the slot color to invalid.
        /// </summary>
        public virtual void HighlightInvalid() => image.color = invalid;

        protected virtual void Awake() => m_image = GetComponent<Image>();
    }
}
