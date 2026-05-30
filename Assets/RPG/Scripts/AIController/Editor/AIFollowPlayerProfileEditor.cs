using System.Collections.Generic;
using PLAYERTWO.ARPGProject;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AIFollowPlayerProfile))]
public class AIFollowPlayerProfileEditor : Editor
{
    private const string RarityIdsPropertyName = "gatherableItemRarityIds";
    private const string FilterByRarityPropertyName = "filterItemsByRarity";
    private const string RaritiesInitializedPropertyName = "gatherableItemRaritiesInitialized";
    private const string EnableGatheringPropertyName = "enableGathering";

    private static readonly string[] GatheringPropertyNames =
    {
        "gatherMoney",
        "gatherItems",
        "gatherableItemTypes",
        "gatherScanRange",
        "gatherTargetDistance",
        "gatherMoveToCollectibleRange",
        "gatherDelay",
        "gatherMoveDuration",
        "gatherSpeedBonus",
        "gatherSpeedDistanceScale",
        "returnSpeedBonus",
        "returnSpeedDistanceScale",
        "maxGatherDistanceFromPlayer",
        "gatherLeashHysteresis",
    };

    private SerializedProperty enableGatheringProperty;
    private SerializedProperty filterByRarityProperty;
    private SerializedProperty rarityIdsProperty;
    private SerializedProperty raritiesInitializedProperty;

    private void OnEnable()
    {
        enableGatheringProperty = serializedObject.FindProperty(EnableGatheringPropertyName);
        filterByRarityProperty = serializedObject.FindProperty(FilterByRarityPropertyName);
        rarityIdsProperty = serializedObject.FindProperty(RarityIdsPropertyName);
        raritiesInitializedProperty = serializedObject.FindProperty(RaritiesInitializedPropertyName);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (filterByRarityProperty != null)
            filterByRarityProperty.boolValue = true;

        bool gatheringEnabled = enableGatheringProperty == null || enableGatheringProperty.boolValue;
        DrawPropertiesExcluding(serializedObject, GetExcludedPropertyNames(gatheringEnabled));

        if (gatheringEnabled)
            DrawRarityFilter();

        serializedObject.ApplyModifiedProperties();
    }


    private string[] GetExcludedPropertyNames(bool gatheringEnabled)
    {
        var excludedProperties = new List<string>
        {
            "m_Script",
            FilterByRarityPropertyName,
            RaritiesInitializedPropertyName,
            RarityIdsPropertyName,
        };

        if (!gatheringEnabled)
            excludedProperties.AddRange(GatheringPropertyNames);

        return excludedProperties.ToArray();
    }

    private void DrawRarityFilter()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pet Pickup Rarity Filter", EditorStyles.boldLabel);

        var rarities = GetExistingRarities();
        if (rarities.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No Item Rarity entries were found in any Game Data asset. Add rarities to Game Data to configure this filter.",
                MessageType.Warning);
            EditorGUILayout.PropertyField(rarityIdsProperty, includeChildren: true);
            return;
        }

        InitializeRarityIdsIfNeeded(rarities.Count);

        if (rarities.Count > 31)
        {
            EditorGUILayout.HelpBox(
                "Unity's mask field supports up to 31 rarity entries. Edit the serialized rarity id list directly.",
                MessageType.Info);
            EditorGUILayout.PropertyField(rarityIdsProperty, includeChildren: true);
            return;
        }

        var rarityNames = new string[rarities.Count];
        for (int i = 0; i < rarities.Count; i++)
            rarityNames[i] = GetRarityName(rarities[i], i);

        int currentMask = GetRarityMask(rarities.Count);
        int nextMask = EditorGUILayout.MaskField("Gatherable Rarities", currentMask, rarityNames);

        if (nextMask != currentMask)
            SetRarityMask(nextMask, rarities.Count);

        EditorGUILayout.HelpBox(
            "Rarity options are loaded from the existing Game Data item rarity list and stored as rarity ids, matching ItemInstance.rarityId.",
            MessageType.None);
    }

    private void InitializeRarityIdsIfNeeded(int rarityCount)
    {
        if (raritiesInitializedProperty != null && raritiesInitializedProperty.boolValue)
            return;

        SetAllRarities(rarityCount);

        if (raritiesInitializedProperty != null)
            raritiesInitializedProperty.boolValue = true;
    }

    private void SetAllRarities(int rarityCount)
    {
        rarityIdsProperty.ClearArray();

        for (int i = 0; i < rarityCount; i++)
        {
            rarityIdsProperty.InsertArrayElementAtIndex(i);
            rarityIdsProperty.GetArrayElementAtIndex(i).intValue = i;
        }
    }

    private List<ItemRarity> GetExistingRarities()
    {
        var rarities = new List<ItemRarity>();

        var activeGameData = GameDatabase.instance != null ? GameDatabase.instance.gameData : null;
        if (TryCopyRarities(activeGameData, rarities))
            return rarities;

        var gameDataGuids = AssetDatabase.FindAssets("t:GameData");
        foreach (var guid in gameDataGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var gameData = AssetDatabase.LoadAssetAtPath<GameData>(path);
            if (TryCopyRarities(gameData, rarities))
                return rarities;
        }

        return rarities;
    }

    private bool TryCopyRarities(GameData gameData, List<ItemRarity> destination)
    {
        if (gameData == null || gameData.itemRarities == null || gameData.itemRarities.Count == 0)
            return false;

        foreach (var rarity in gameData.itemRarities)
            destination.Add(rarity);

        return true;
    }

    private string GetRarityName(ItemRarity rarity, int index)
    {
        if (rarity == null)
            return $"Missing Rarity {index}";

        if (!string.IsNullOrWhiteSpace(rarity.displayName))
            return rarity.displayName;

        return rarity.name;
    }

    private int GetRarityMask(int rarityCount)
    {
        int mask = 0;

        for (int i = 0; i < rarityIdsProperty.arraySize; i++)
        {
            int rarityId = rarityIdsProperty.GetArrayElementAtIndex(i).intValue;
            if (rarityId >= 0 && rarityId < rarityCount)
                mask |= 1 << rarityId;
        }

        return mask;
    }

    private void SetRarityMask(int mask, int rarityCount)
    {
        rarityIdsProperty.ClearArray();

        for (int i = 0; i < rarityCount; i++)
        {
            if ((mask & (1 << i)) == 0)
                continue;

            int newIndex = rarityIdsProperty.arraySize;
            rarityIdsProperty.InsertArrayElementAtIndex(newIndex);
            rarityIdsProperty.GetArrayElementAtIndex(newIndex).intValue = i;
        }
    }
}
