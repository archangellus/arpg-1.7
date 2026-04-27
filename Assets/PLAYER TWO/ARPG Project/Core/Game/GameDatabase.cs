using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Game/Game Database")]
    public class GameDatabase : Singleton<GameDatabase>
    {
        [Tooltip("The Game Data object which references all the data from your game.")]
        public GameData gameData;

        public List<Character> characters => gameData.characters;
        public List<Item> items => gameData.items;
        public List<Skill> skills => gameData.skills;
        public List<Quest> quests => gameData.quests;
        public List<ItemRarity> itemRarities => gameData.itemRarities;

        public EntityEffect bleedEffect => gameData.bleedEffect;
        public EntityEffect burnEffect => gameData.burnEffect;
        public EntityEffect freezeEffect => gameData.freezeEffect;
        public EntityEffect poisonEffect => gameData.poisonEffect;

        /// <summary>
        /// Returns the index of a given element on a list of a given type.
        /// If the element was not found, this method returns -1.
        /// </summary>
        /// <param name="element">The element you want to find the index.</param>
        /// <typeparam name="T">The type of the list you want to find the element from.</typeparam>
        public int GetElementId<T>(T element)
            where T : ScriptableObject
        {
            if (element is Character character)
                return characters.IndexOf(character);
            else if (element is Item item)
                return items.IndexOf(item);
            else if (element is Skill skill)
                return skills.IndexOf(skill);
            else if (element is Quest quest)
                return quests.IndexOf(quest);
            else if (element is ItemRarity itemRarity)
                return itemRarities.IndexOf(itemRarity);

            return -1;
        }

        /// <summary>
        /// Returns an element by its id from a list of a given type.
        /// </summary>
        /// <param name="id">The id of the element you're looking for.</param>
        /// <typeparam name="T">The type of the list from which you want to find the element.</typeparam>
        public T FindElementById<T>(int id)
            where T : ScriptableObject
        {
            var type = typeof(T);

            if (type == typeof(Character) && characters.IsIndexValid(id))
                return characters[id] as T;
            else if (type == typeof(Item) && items.IsIndexValid(id))
                return items[id] as T;
            else if (type == typeof(Skill) && skills.IsIndexValid(id))
                return skills[id] as T;
            else if (type == typeof(Quest) && quests.IsIndexValid(id))
                return quests[id] as T;
            else if (type == typeof(ItemRarity) && itemRarities.IsIndexValid(id))
                return itemRarities[id] as T;

            return default;
        }
    }
}
