using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/UI/UI Character Window")]
    public class UICharacterWindow : MonoBehaviour
    {
        [Header("Text References")]
        [Tooltip("A reference to the Text component used as the Character's name.")]
        public Text nameText;

        [Tooltip("A reference to the Text component used as the Character's level.")]
        public Text levelText;

        [Tooltip("A reference to the Text component used as the Character's strength.")]
        public Text strengthText;

        [Tooltip("A reference to the Text component used as the Character's dexterity.")]
        public Text dexterityText;

        [Tooltip("A reference to the Text component used as the Character's vitality.")]
        public Text vitalityText;

        [Tooltip("A reference to the Text component used as the Character's energy.")]
        public Text energyText;

        protected string m_initialName;
        protected string m_initialLevel;
        protected string m_initialStrength;
        protected string m_initialDexterity;
        protected string m_initialVitality;
        protected string m_initialEnergy;

        protected virtual void InitializeStrings()
        {
            m_initialName = nameText.text;
            m_initialLevel = levelText.text;
            m_initialStrength = strengthText.text;
            m_initialDexterity = dexterityText.text;
            m_initialVitality = vitalityText.text;
            m_initialEnergy = energyText.text;
        }

        /// <summary>
        /// Updates all the texts to match a given Character Instance data.
        /// </summary>
        /// <param name="character">The Character Instance you want to get the data from.</param>
        public virtual void UpdateTexts(CharacterInstance character)
        {
            nameText.text = string.Format(m_initialName, character.name);
            levelText.text = string.Format(m_initialLevel, character.stats.currentLevel);
            strengthText.text = string.Format(m_initialStrength, character.stats.currentStrength);
            dexterityText.text = string.Format(m_initialDexterity, character.stats.currentDexterity);
            vitalityText.text = string.Format(m_initialVitality, character.stats.currentVitality);
            energyText.text = string.Format(m_initialEnergy, character.stats.currentEnergy);
        }

        protected virtual void Awake() => InitializeStrings();
    }
}
