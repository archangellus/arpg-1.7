using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PLAYERTWO.ARPGProject
{
    [CustomPropertyDrawer(typeof(CharacterEquipmentList))]
    public class CharacterEquipmentListDrawer : PropertyDrawer
    {
        static readonly ItemSlots[] k_allSlots = (ItemSlots[])
            System.Enum.GetValues(typeof(ItemSlots));

        class SlotFieldData
        {
            public SerializedProperty entriesProp;
            public int index;
            public ListView listView;
        }

        SerializedProperty GetEntriesProp(SerializedProperty property) =>
            property.FindPropertyRelative("entries");

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var entriesProp = GetEntriesProp(property);

            var listView = new ListView
            {
                showBoundCollectionSize = false,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                showAddRemoveFooter = true,
                showBorder = true,
                showFoldoutHeader = true,
                headerTitle = property.displayName,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                makeItem = CreateEntryElement,
            };
            listView.bindItem = (elem, i) => BindEntry(elem, entriesProp, i, listView);
            listView.unbindItem = (elem, i) => UnbindEntry(elem);
            listView.onAdd = _ => AddFirstFreeSlot(entriesProp);
            listView.BindProperty(entriesProp);

            return listView;
        }

        static VisualElement CreateEntryElement()
        {
            var foldout = new Foldout();

            var slotField = new PopupField<int>("Slot")
            {
                name = "slot-field",
                formatListItemCallback = idx => ((ItemSlots)idx).ToString().ToTitleCase(),
                formatSelectedValueCallback = idx => ((ItemSlots)idx).ToString().ToTitleCase(),
            };

            var itemField = new PropertyField { label = "Item", name = "item-field" };

            foldout.Add(slotField);
            foldout.Add(itemField);
            return foldout;
        }

        void BindEntry(
            VisualElement elem,
            SerializedProperty entriesProp,
            int index,
            ListView listView
        )
        {
            var entryProp = entriesProp.GetArrayElementAtIndex(index);
            var slotProp = entryProp.FindPropertyRelative("slot");
            var itemProp = entryProp.FindPropertyRelative("item");

            var foldout = (Foldout)elem;
            var slotField = foldout.Q<PopupField<int>>("slot-field");
            var itemField = foldout.Q<PropertyField>("item-field");

            foldout.text = ((ItemSlots)slotProp.enumValueIndex).ToString().ToTitleCase();

            var usedSlots = CollectUsedSlots(entriesProp);
            slotField.choices = BuildFilteredChoices(slotProp.enumValueIndex, usedSlots);
            slotField.SetValueWithoutNotify(slotProp.enumValueIndex);

            slotField.userData = new SlotFieldData
            {
                entriesProp = entriesProp,
                index = index,
                listView = listView,
            };

            slotField.UnregisterValueChangedCallback(OnSlotChanged);
            slotField.RegisterValueChangedCallback(OnSlotChanged);

            itemField.BindProperty(itemProp);
        }

        static void UnbindEntry(VisualElement elem)
        {
            var foldout = (Foldout)elem;
            foldout.Q<PopupField<int>>("slot-field").UnregisterValueChangedCallback(OnSlotChanged);
            foldout.Q<PropertyField>("item-field").Unbind();
        }

        static void OnSlotChanged(ChangeEvent<int> evt)
        {
            if (((VisualElement)evt.target).userData is not SlotFieldData data)
                return;

            var entryProp = data.entriesProp.GetArrayElementAtIndex(data.index);
            entryProp.FindPropertyRelative("slot").enumValueIndex = evt.newValue;
            data.entriesProp.serializedObject.ApplyModifiedProperties();
            data.listView.RefreshItems();
        }

        static HashSet<int> CollectUsedSlots(SerializedProperty entriesProp)
        {
            var used = new HashSet<int>();

            for (int i = 0; i < entriesProp.arraySize; i++)
                used.Add(
                    entriesProp
                        .GetArrayElementAtIndex(i)
                        .FindPropertyRelative("slot")
                        .enumValueIndex
                );

            return used;
        }

        static List<int> BuildFilteredChoices(int currentSlotIndex, HashSet<int> usedSlots)
        {
            var choices = new List<int>();

            foreach (var slot in k_allSlots)
            {
                int idx = (int)slot;

                if (!usedSlots.Contains(idx) || idx == currentSlotIndex)
                    choices.Add(idx);
            }

            return choices;
        }

        static void AddFirstFreeSlot(SerializedProperty entriesProp)
        {
            var usedSlots = CollectUsedSlots(entriesProp);

            if (usedSlots.Count >= k_allSlots.Length)
                return;

            entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
            var newEntry = entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1);

            foreach (var slot in k_allSlots)
            {
                if (!usedSlots.Contains((int)slot))
                {
                    newEntry.FindPropertyRelative("slot").enumValueIndex = (int)slot;
                    break;
                }
            }

            entriesProp.serializedObject.ApplyModifiedProperties();
        }
    }
}
