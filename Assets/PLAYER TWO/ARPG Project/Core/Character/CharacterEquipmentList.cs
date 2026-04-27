using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Serializable wrapper around a list of <see cref="CharacterEquipmentEntry"/> items,
    /// used to draw the equipment list with a custom property drawer in the Unity Inspector.
    /// </summary>
    [System.Serializable]
    public class CharacterEquipmentList : IEnumerable<CharacterEquipmentEntry>
    {
        public List<CharacterEquipmentEntry> entries = new();

        public IEnumerator<CharacterEquipmentEntry> GetEnumerator() => entries.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
