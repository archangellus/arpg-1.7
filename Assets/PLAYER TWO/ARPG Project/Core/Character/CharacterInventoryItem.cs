namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class CharacterInventoryItem
    {
        public CharacterItem item;
        public int row;
        public int column;

        public CharacterInventoryItem(CharacterItem item, int row, int column)
        {
            this.item = item;
            this.row = row;
            this.column = column;
        }
    }
}
