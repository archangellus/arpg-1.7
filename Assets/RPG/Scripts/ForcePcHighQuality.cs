using UnityEngine;

public static class ForcePcHighQuality
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceQuality()
    {
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            if (QualitySettings.names[i] == "PC High")
            {
                QualitySettings.SetQualityLevel(i, true);
                Debug.Log("Forced quality level to PC High");
                return;
            }
        }

        Debug.LogWarning("PC High quality level was not found.");
    }
}