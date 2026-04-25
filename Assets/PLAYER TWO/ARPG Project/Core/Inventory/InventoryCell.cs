namespace PLAYERTWO.ARPGProject
{
    public struct InventoryCell
    {
        public int row;
        public int column;

        public InventoryCell(int row = -1, int column = -1)
        {
            this.row = row;
            this.column = column;
        }
    }
}
