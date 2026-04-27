using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/UI/UI Character Selection")]
    public class UICharacterSelection : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Determines if the first character on the list should be selected on start.")]
        public bool selectFirstOnStart = true;

        [Header("Character References")]
        [Tooltip("A reference to the UI Character Window to display the Character's data.")]
        public UICharacterWindow characterWindow;

        [Tooltip("The prefab to use to represent each character on the list.")]
        public UICharacterButton characterSlot;

        [Tooltip("A reference to the UI Character Form component.")]
        public UICharacterForm characterForm;

        [Tooltip("A reference to the Game Object that contains the character's list.")]
        public GameObject charactersWindow;

        [Tooltip("A reference to the Game Object that contains the actions to manage characters.")]
        public GameObject characterActions;

        [Tooltip("A reference to the transform used as container for placing all characters.")]
        public Transform charactersContainer;

        [Header("Button References")]
        [Tooltip("A reference to the Button component that opens the character creation window.")]
        public Button newCharacterButton;

        [Tooltip("A reference to the Button component that starts the Game.")]
        public Button startGameButton;

        [Tooltip(
            "A reference to the Button component that deletes the current selected character."
        )]
        public Button deleteCharacterButton;

        [Header("Audio References")]
        [Tooltip("The Audio Clip that plays when selecting a character.")]
        public AudioClip selectCharacterAudio;

        [Tooltip("The Audio Clip that plays when deleting a character.")]
        public AudioClip deleteCharacterAudio;

        [Space(15)]
        public UnityEvent<CharacterInstance> onCharacterSelected;

        protected CanvasGroup m_charactersWindowGroup;
        protected CanvasGroup m_characterActionsGroup;

        protected int m_currentCharacterId = -1;
        protected bool m_creatingCharacter;

        protected List<UICharacterButton> m_characters = new();

        protected Game m_game => Game.instance;
        protected GameAudio m_audio => GameAudio.instance;

        protected virtual void InitializeGroups()
        {
            if (!charactersWindow.TryGetComponent(out m_charactersWindowGroup))
                m_charactersWindowGroup = charactersWindow.AddComponent<CanvasGroup>();

            if (!characterActions.TryGetComponent(out m_characterActionsGroup))
                m_characterActionsGroup = characterActions.AddComponent<CanvasGroup>();
        }

        protected virtual void InitializeCallbacks()
        {
            newCharacterButton.onClick.AddListener(ToggleCharacterCreation);
            characterForm.onSubmit.AddListener(ToggleCharacterCreation);
            characterForm.onCancel.AddListener(ToggleCharacterCreation);
            startGameButton.onClick.AddListener(StartGame);

            deleteCharacterButton.onClick.AddListener(() =>
            {
                m_audio.PlayUiEffect(deleteCharacterAudio);
                DeleteSelectedCharacter();
            });

            m_game.onCharacterAdded.AddListener(AddCharacter);
            m_game.onDataLoaded.AddListener(RefreshList);
            m_game.onCharacterDeleted.AddListener(() =>
            {
                var nextToSelect = m_currentCharacterId - 1;
                m_currentCharacterId = -1;
                RefreshList();

                if (nextToSelect >= 0)
                    SelectCharacter(nextToSelect);
            });
        }

        /// <summary>
        /// Toggles the character creation form.
        /// </summary>
        public virtual void ToggleCharacterCreation()
        {
            m_creatingCharacter = !m_creatingCharacter;
            m_charactersWindowGroup.alpha = m_characterActionsGroup.alpha = m_creatingCharacter
                ? 0.5f
                : 1;
            m_charactersWindowGroup.blocksRaycasts = m_characterActionsGroup.blocksRaycasts =
                !m_creatingCharacter;
            characterForm.gameObject.SetActive(m_creatingCharacter);

            if (!m_creatingCharacter)
                if (!m_creatingCharacter)
{
    if (m_currentCharacterId >= 0 && m_currentCharacterId < m_characters.Count)
        CharacterPreview.instance.Preview(m_characters[m_currentCharacterId].character);
    else
        CharacterPreview.instance.Clear();
}
        }

        /// <summary>
        /// Starts the Game with the current character.
        /// </summary>
        public virtual void StartGame()
        {
            characterActions.SetActive(false);
            m_game.StartGame(m_currentCharacterId);
        }

        /// <summary>
        /// Adds a given Character Instance to the Game.
        /// </summary>
        /// <param name="characterId">The ID of the Character Instance you want to add.</param>
        public virtual void AddCharacter(int characterId)
        {
            m_currentCharacterId = -1;
            RefreshList();
            SelectCharacter(characterId);
        }

        /// <summary>
        /// Deletes the current selected character.
        /// </summary>
        public virtual void DeleteSelectedCharacter()
        {
            if (m_currentCharacterId < 0 || m_currentCharacterId >= m_characters.Count)
                return;

            characterWindow.gameObject.SetActive(false);
            characterActions.gameObject.SetActive(false);
            m_game.DeleteCharacter(m_currentCharacterId);
            CharacterPreview.instance.Clear();
        }

        /// <summary>
        /// Selects a given Character Instance.
        /// </summary>
        /// <param name="characterId">The ID of the Character Instance you want to select.</param>
        public virtual void SelectCharacter(int characterId)
        {
            if (characterId < 0 || characterId >= m_characters.Count)
                return;

            m_currentCharacterId = characterId;
            m_characters[m_currentCharacterId].SetInteractable(false);
            characterWindow.gameObject.SetActive(true);
            characterActions.gameObject.SetActive(true);
            characterWindow.UpdateTexts(m_characters[characterId].character);
            CharacterPreview.instance.Preview(m_characters[characterId].character);
            onCharacterSelected.Invoke(m_characters[characterId].character);
        }

        /// <summary>
        /// Selects the first Character on the list if it exists.
        /// </summary>
        public virtual void SelectFirstCharacter()
        {
            if (m_characters.Count > 0)
                SelectCharacter(0);
        }

        /// <summary>
        /// Refreshes the list of characters.
        /// </summary>
        public virtual void RefreshList()
        {
            var characters = m_game.characters;

            foreach (var character in m_characters)
            {
                character.gameObject.SetActive(false);
            }

            for (int i = 0; i < characters.Count; i++)
            {
                if (m_characters.Count < i + 1)
                {
                    var index = i;

                    m_characters.Add(Instantiate(characterSlot, charactersContainer));
                    m_characters[i]
                        .onSelect.AddListener(
                            (character) =>
                            {
                                if (
                                    m_currentCharacterId != index
                                    && m_currentCharacterId >= 0
                                    && m_currentCharacterId < m_characters.Count
                                    && m_characters[m_currentCharacterId]
                                )
                                    m_characters[m_currentCharacterId].SetInteractable(true);

                                m_audio.PlayUiEffect(selectCharacterAudio);
                                SelectCharacter(index);
                            }
                        );
                }

                m_characters[i].SetCharacter(characters[i]);
                m_characters[i].gameObject.SetActive(true);
                m_characters[i].SetInteractable(true);
            }
        }

        protected virtual void Start()
        {
            m_game.ReloadGameData();
            InitializeGroups();
            InitializeCallbacks();
            RefreshList();

            if (selectFirstOnStart)
                SelectFirstCharacter();
        }
    }
}
