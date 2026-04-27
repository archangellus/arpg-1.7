namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Pairs an equipment slot with its initial CharacterItem for a Character's loadout.
    /// </summary>
    [System.Serializable]
    public class CharacterEquipmentEntry
    {
        public ItemSlots slot;
        public CharacterItem item;
    }
}
