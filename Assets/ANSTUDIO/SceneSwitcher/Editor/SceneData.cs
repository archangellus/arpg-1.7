// SceneData.cs — serialisable data types for Scene Switcher.
// Keep all four files in the same folder inside your Unity project.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>Root data container persisted to SceneData.json.</summary>
[System.Serializable]
public class SceneData
{
    public List<SceneInfo>             sceneInfos     = new List<SceneInfo>();
    public List<SceneIconInfo>         importedImages = new List<SceneIconInfo>();
    public List<SceneIconEmbeddedData> embeddedImages = new List<SceneIconEmbeddedData>();

    public void AddScene(string sceneName, string scenePath)
    {
        if (sceneInfos == null) sceneInfos = new List<SceneInfo>();
        sceneInfos.Add(new SceneInfo(sceneName, scenePath));
    }

    public void RemoveScene(int index)
    {
        if (sceneInfos != null && index >= 0 && index < sceneInfos.Count)
            sceneInfos.RemoveAt(index);
    }

    public bool MoveScene(int fromIndex, int insertIndex)
    {
        if (sceneInfos == null || fromIndex < 0 || fromIndex >= sceneInfos.Count)
            return false;

        insertIndex = Mathf.Clamp(insertIndex, 0, sceneInfos.Count);
        if (insertIndex == fromIndex || insertIndex == fromIndex + 1)
            return false;

        SceneInfo scene = sceneInfos[fromIndex];
        sceneInfos.RemoveAt(fromIndex);

        if (insertIndex > fromIndex)
            insertIndex--;

        insertIndex = Mathf.Clamp(insertIndex, 0, sceneInfos.Count);
        sceneInfos.Insert(insertIndex, scene);
        return true;
    }

    public bool SceneExists(string scenePath)
    {
        if (sceneInfos == null) return false;
        return sceneInfos.Exists(s => s != null && s.scenePath == scenePath);
    }
}

[System.Serializable]
public class SceneInfo
{
    public string sceneName;
    public string scenePath;
    public string customIconId = "";

    public SceneInfo() { } // required for JsonUtility

    public SceneInfo(string sceneName, string scenePath)
    {
        this.sceneName   = sceneName;
        this.scenePath   = scenePath;
        this.customIconId = "";
    }
}

[System.Serializable]
public class SceneIconInfo
{
    public string id;
    public string displayName;
    public string assetPath;

    public SceneIconInfo() { }

    public SceneIconInfo(string id, string displayName, string assetPath)
    {
        this.id          = id;
        this.displayName = displayName;
        this.assetPath   = assetPath;
    }
}

[System.Serializable]
public class SceneIconEmbeddedData
{
    public string id;
    public string displayName;
    public string fileName;
    public string imageDataBase64;

    public SceneIconEmbeddedData() { }

    public SceneIconEmbeddedData(string id, string displayName, string fileName, string imageDataBase64)
    {
        this.id              = id;
        this.displayName     = displayName;
        this.fileName        = fileName;
        this.imageDataBase64 = imageDataBase64;
    }
}
#endif
