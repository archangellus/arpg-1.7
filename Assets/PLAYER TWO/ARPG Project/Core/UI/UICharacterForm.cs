using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/UI/UI Character Form")]
    public class UICharacterForm : MonoBehaviour
    {
        [Tooltip("A reference to the Input Field component used to input the character name.")]
        public InputField characterName;

        [Tooltip("A reference to the Dropdown component used to select the character's class.")]
        public Dropdown characterClass;

        [Tooltip("A reference to the Button component to create characters.")]
        public Button createButton;
        public UnityEvent onSubmit;
        public UnityEvent onCancel;

        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when showing the form.")]
        public AudioClip showFormClip;

        [Tooltip("The Audio Clip that plays when cancelling the form.")]
        public AudioClip cancelFormClip;

        [Tooltip("TYhe Audio Clip that plays when submitting the form.")]
        public AudioClip submitFormClip;

        protected string[] m_characterNames;

        protected CharacterInstance m_characterInstance;

        protected GameAudio m_audio => GameAudio.instance;

        /// <summary>
        /// Cancels the form.
        /// </summary>
        public virtual void Cancel()
        {
            m_audio.PlayUiEffect(cancelFormClip);
            onCancel.Invoke();
        }

        protected virtual void HandleSubmit()
        {
            if (!ValidName(characterName.text))
                return;

            Game.instance.CreateCharacter(characterName.text, characterClass.value);
            m_audio.PlayUiEffect(submitFormClip);
            onSubmit.Invoke();
        }

        protected virtual bool ValidName(string name) =>
            name.Length > 0 && !m_characterNames.Contains(name);

        protected IEnumerator SelectInputField()
        {
            yield return null;

            characterName.ActivateInputField();
            characterName.Select();
        }

        protected virtual void Start()
        {
            createButton.onClick.AddListener(HandleSubmit);
            characterName.onValueChanged.AddListener(
                (value) => createButton.interactable = ValidName(value)
            );
            characterClass.onValueChanged.AddListener(PreviewClass);
        }

#if UNITY_STANDALONE || UNITY_WEBGL
        protected virtual void Update()
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame)
                HandleSubmit();
            else if (Keyboard.current.escapeKey.wasPressedThisFrame)
                Cancel();
        }
#endif

        protected virtual void PreviewClass(int classId)
        {
            var character = GameDatabase.instance.FindElementById<Character>(classId);
            m_characterInstance = new CharacterInstance(character, "");
            CharacterPreview.instance.Preview(m_characterInstance);
        }

        protected virtual void OnEnable()
        {
            var classes = GameDatabase.instance.characters.Select(c => c.name);
            m_characterNames = Game.instance.characters.Select(c => c.name).ToArray();
            characterClass.ClearOptions();
            characterClass.AddOptions(classes.ToList());
            characterName.text = "";
            createButton.interactable = false;
            PreviewClass(characterClass.value);
            StartCoroutine(SelectInputField());
            m_audio.PlayUiEffect(showFormClip);
        }
    }
}
