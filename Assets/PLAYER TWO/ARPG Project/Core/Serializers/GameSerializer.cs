using UnityEngine;
using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class GameSerializer
    {
        public List<CharacterSerializer> characters = new List<CharacterSerializer>();
        public InventorySerializer[] stashes;

        public GameSerializer(Game game)
        {
            InitializeCharacters(game.characters);
            InitializeStashes(game.stash);
        }

        protected virtual void InitializeCharacters(List<CharacterInstance> characters)
        {
            foreach (var character in characters)
            {
                this.characters.Add(new CharacterSerializer(character));
            }
        }

        protected virtual void InitializeStashes(GameStash stash)
        {
            stashes = new InventorySerializer[stash.amount];

            for (int i = 0; i < stashes.Length; i++)
            {
                stashes[i] = new InventorySerializer(stash.GetInventory(i));
            }
        }

        public virtual string ToJson() => JsonUtility.ToJson(this);

        public static GameSerializer FromJson(string json) =>
            JsonUtility.FromJson<GameSerializer>(json);
    }
}
