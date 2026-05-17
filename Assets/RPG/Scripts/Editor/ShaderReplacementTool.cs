using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ShaderReplacementTool : EditorWindow
{
    private int selectedShaderToReplaceIndex = 0;
    private int selectedNewShaderIndex = 0;
    private List<string> shaderNames = new List<string>();
    private Shader[] shaders;
    private List<Material> materialsToUpdate = new List<Material>();

    [MenuItem("Tools/Shader Replacement Tool")]
    public static void ShowWindow()
    {
        GetWindow<ShaderReplacementTool>("Shader Replacement Tool");
    }

    private void OnEnable()
    {
        LoadShaderList();
    }

    private void LoadShaderList()
    {
        shaderNames.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Shader");
        shaders = new Shader[guids.Length];

        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            shaders[i] = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            shaderNames.Add(shaders[i].name);
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Shader Replacement Settings", EditorStyles.boldLabel);

        if (shaderNames.Count == 0)
        {
            EditorGUILayout.HelpBox("No shaders found in the project.", MessageType.Warning);
        }
        else
        {
            selectedShaderToReplaceIndex = EditorGUILayout.Popup("Shader to Replace", selectedShaderToReplaceIndex, shaderNames.ToArray());
            selectedNewShaderIndex = EditorGUILayout.Popup("New Shader", selectedNewShaderIndex, shaderNames.ToArray());
        }

        if (GUILayout.Button("Find and Replace Shaders"))
        {
            if (shaders.Length > 0)
            {
                FindAndReplaceShaders();
            }
            else
            {
                Debug.LogError("No shaders available for replacement.");
            }
        }
    }

    private void FindAndReplaceShaders()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material");
        materialsToUpdate.Clear();
        Shader shaderToReplace = shaders[selectedShaderToReplaceIndex];
        Shader newShader = shaders[selectedNewShaderIndex];

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            if (material.shader == shaderToReplace)
            {
                materialsToUpdate.Add(material);
            }
        }

        foreach (Material mat in materialsToUpdate)
        {
            Undo.RecordObject(mat, "Shader Replacement");
            Texture mainTexture = mat.mainTexture;
            mat.shader = newShader;
            mat.SetTexture("_BaseColorMap", mainTexture); // For HDRP Lit, the base map might be named differently
            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();
        Debug.Log(materialsToUpdate.Count + " materials updated to " + newShader.name);
    }
}
