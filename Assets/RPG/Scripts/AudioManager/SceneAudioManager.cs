using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class SceneAudioManager : MonoBehaviour
{
    [Serializable]
    public class AudioEntry
    {
        public string id;

        [FormerlySerializedAs("clip")]
        [SerializeField] private AudioClip legacyClip;
        public List<AudioClip> clips = new List<AudioClip>();

        [Range(0f, 1f)]
        public float volume = 1f;

        [Range(0.1f, 3f)]
        public float pitch = 1f;

        public bool useRandomPitch = false;

        [Range(0.1f, 3f)]
        public float randomPitchMin = 0.95f;

        [Range(0.1f, 3f)]
        public float randomPitchMax = 1.05f;

        public bool HasAnyClip()
        {
            if (clips == null)
                return false;

            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null)
                    return true;
            }

            return false;
        }

        public void MigrateLegacyClipIfNeeded()
        {
            if (legacyClip == null)
                return;

            if (clips == null)
            {
                clips = new List<AudioClip>();
            }

            if (!clips.Contains(legacyClip))
            {
                clips.Add(legacyClip);
            }

            legacyClip = null;
        }

        public AudioClip GetRandomClip()
        {
            if (clips == null || clips.Count == 0)
                return null;

            int validCount = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null)
                {
                    validCount++;
                }
            }

            if (validCount == 0)
                return null;

            int selectedValidIndex = UnityEngine.Random.Range(0, validCount);
            int currentValidIndex = 0;

            for (int i = 0; i < clips.Count; i++)
            {
                AudioClip clip = clips[i];
                if (clip == null)
                    continue;

                if (currentValidIndex == selectedValidIndex)
                    return clip;

                currentValidIndex++;
            }

            return null;
        }

#if UNITY_EDITOR
        public AudioClip GetPreviewClip()
        {
            return GetRandomClip();
        }
#endif
    }

    [Header("Drag your clips here")]
    [SerializeField] private List<AudioEntry> audioEntries = new List<AudioEntry>();

    private AudioSource audioSource;
    private Dictionary<string, AudioEntry> audioLookup;

    private void Reset()
    {
        CacheAudioSource();
        ApplyDefaultAudioSourceSettings();
    }

    private void Awake()
    {
        CacheAudioSource();
        ApplyDefaultAudioSourceSettings();
        BuildLookup();
    }

    private void OnValidate()
    {
        CacheAudioSource();
        ApplyDefaultAudioSourceSettings();
        ValidateEntries();
        BuildLookup();
    }

    private void CacheAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void ApplyDefaultAudioSourceSettings()
    {
        if (audioSource == null)
            return;

        audioSource.playOnAwake = false;
        audioSource.volume = 1f;
        audioSource.pitch = 1f;
    }

    private void ValidateEntries()
    {
        if (audioEntries == null)
            return;

        for (int i = 0; i < audioEntries.Count; i++)
        {
            AudioEntry entry = audioEntries[i];
            if (entry == null)
                continue;

            entry.MigrateLegacyClipIfNeeded();

            if (entry.randomPitchMin > entry.randomPitchMax)
            {
                float temp = entry.randomPitchMin;
                entry.randomPitchMin = entry.randomPitchMax;
                entry.randomPitchMax = temp;
            }

            entry.volume = Mathf.Clamp01(entry.volume);
            entry.pitch = Mathf.Clamp(entry.pitch, 0.1f, 3f);
            entry.randomPitchMin = Mathf.Clamp(entry.randomPitchMin, 0.1f, 3f);
            entry.randomPitchMax = Mathf.Clamp(entry.randomPitchMax, 0.1f, 3f);
        }
    }

    private void BuildLookup()
    {
        audioLookup = new Dictionary<string, AudioEntry>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < audioEntries.Count; i++)
        {
            AudioEntry entry = audioEntries[i];

            if (entry == null)
                continue;

            entry.MigrateLegacyClipIfNeeded();

            if (string.IsNullOrWhiteSpace(entry.id))
            {
                Debug.LogWarning($"[{nameof(SceneAudioManager)}] Empty audio id found at index {i} on {gameObject.name}.");
                continue;
            }

            if (!entry.HasAnyClip())
            {
                Debug.LogWarning($"[{nameof(SceneAudioManager)}] Audio entry '{entry.id}' has no clips assigned on {gameObject.name}.");
                continue;
            }

            if (audioLookup.ContainsKey(entry.id))
            {
                Debug.LogWarning($"[{nameof(SceneAudioManager)}] Duplicate audio id '{entry.id}' on {gameObject.name}. Only the first one will be used.");
                continue;
            }

            audioLookup.Add(entry.id, entry);
        }
    }

    public static float ResolvePitch(AudioEntry entry)
    {
        if (entry == null)
            return 1f;

        if (entry.useRandomPitch)
        {
            float min = Mathf.Min(entry.randomPitchMin, entry.randomPitchMax);
            float max = Mathf.Max(entry.randomPitchMin, entry.randomPitchMax);
            return UnityEngine.Random.Range(min, max);
        }

        return Mathf.Clamp(entry.pitch, 0.1f, 3f);
    }

    public void PlayByName(string clipId)
    {
        if (audioSource == null)
        {
            CacheAudioSource();
        }

        PlayByName(clipId, audioSource);
    }

    public void PlayByName(string clipId, AudioSource targetSource)
    {
        if (string.IsNullOrWhiteSpace(clipId))
        {
            Debug.LogWarning($"[{nameof(SceneAudioManager)}] PlayByName was called with an empty id.");
            return;
        }

        if (audioLookup == null)
        {
            BuildLookup();
        }

        if (!audioLookup.TryGetValue(clipId, out AudioEntry entry))
        {
            Debug.LogWarning($"[{nameof(SceneAudioManager)}] No audio clip found with id '{clipId}' on {gameObject.name}.");
            return;
        }

        PlayEntry(entry, targetSource);
    }

    public void PlayByIndex(int index)
    {
        if (audioSource == null)
        {
            CacheAudioSource();
        }

        PlayByIndex(index, audioSource);
    }

    public void PlayByIndex(int index, AudioSource targetSource)
    {
        if (index < 0 || index >= audioEntries.Count)
        {
            Debug.LogWarning($"[{nameof(SceneAudioManager)}] Invalid audio index {index} on {gameObject.name}.");
            return;
        }

        AudioEntry entry = audioEntries[index];

        if (entry == null || !entry.HasAnyClip())
        {
            Debug.LogWarning($"[{nameof(SceneAudioManager)}] Audio entry at index {index} is empty or has no clips.");
            return;
        }

        PlayEntry(entry, targetSource);
    }

    private void PlayEntry(AudioEntry entry, AudioSource targetSource)
    {
        if (entry == null)
            return;

        AudioClip selectedClip = entry.GetRandomClip();
        if (selectedClip == null)
            return;

        if (targetSource == null)
        {
            Debug.LogWarning($"[{nameof(SceneAudioManager)}] Target AudioSource is missing.");
            return;
        }

        targetSource.playOnAwake = false;

        float finalPitch = ResolvePitch(entry);
        targetSource.pitch = finalPitch;
        targetSource.PlayOneShot(selectedClip, Mathf.Clamp01(entry.volume));
    }

    public void StopAudio()
    {
        if (audioSource == null)
        {
            CacheAudioSource();
        }

        StopAudio(audioSource);
    }

    public void StopAudio(AudioSource targetSource)
    {
        if (targetSource != null)
        {
            targetSource.Stop();
            targetSource.pitch = 1f;
        }
    }

    public void SetVolume(float volume)
    {
        if (audioSource == null)
        {
            CacheAudioSource();
        }

        if (audioSource != null)
        {
            audioSource.volume = Mathf.Clamp01(volume);
        }
    }

    public void SetPitch(float pitch)
    {
        if (audioSource == null)
        {
            CacheAudioSource();
        }

        if (audioSource != null)
        {
            audioSource.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        }
    }
}
