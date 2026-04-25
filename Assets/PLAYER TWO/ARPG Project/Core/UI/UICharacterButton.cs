using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/UI/UI Character")]
    public class UICharacterButton : MonoBehaviour
    {
        [Tooltip("A reference to the Text component used as the Character's name.")]
        public Text nameText;

        [Tooltip("A reference to the Text component used as the Character's level.")]
        public Text levelText;
        public UnityEvent<CharacterInstance> onSelect;

        protected Button m_button;

        /// <summary>
        /// Returns the Character Instance associated to this UI Character Button.
        /// </summary>
        public CharacterInstance character { get; protected set; }

        protected virtual void InitializeButton()
        {
            m_button = GetComponent<Button>();
            m_button.onClick.AddListener(() => onSelect.Invoke(character));
        }

        /// <summary>
        /// Sets the Character Instance of this UI Character Button.
        /// </summary>
        /// <param name="character">The Character Instance you want to set.</param>
        public virtual void SetCharacter(CharacterInstance character)
        {
            this.character = character;
            nameText.text = this.character.name;
            levelText.text = $"Level {this.character.stats.initialLevel.ToString()}";
        }

        /// <summary>
        /// Sets the interactable value.
        /// </summary>
        /// <param name="value">If true, the button will be interactable.</param>
        public virtual void SetInteractable(bool value) => m_button.interactable = value;

        protected virtual void Awake() => InitializeButton();
    }
}
