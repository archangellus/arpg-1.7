using UnityEngine;
using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Game Data", menuName = "PLAYER TWO/ARPG Project/Game/Game Data")]
    public class GameData : ScriptableObject
    {
        [Tooltip("The list of all available Characters.")]
        public List<Character> characters;

        [Tooltip("The list of all available Items.")]
        public List<Item> items;

        [Tooltip("The list of all available Skills.")]
        public List<Skill> skills;

        [Tooltip("The list of all available Quests.")]
        public List<Quest> quests;
    }
}
