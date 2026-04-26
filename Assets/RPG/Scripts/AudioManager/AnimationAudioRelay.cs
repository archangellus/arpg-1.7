using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class AnimationAudioRelay : MonoBehaviour
{
    private SceneAudioManager audioManager;
    private AudioSource localAudioSource;

    private void Awake()
    {
        CacheAudioSource();
        CacheAudioManager();
    }

    private void CacheAudioSource()
    {
        if (localAudioSource != null)
            return;

        localAudioSource = GetComponent<AudioSource>();

        if (localAudioSource != null)
        {
            localAudioSource.playOnAwake = false;
        }
        else
        {
            Debug.LogWarning($"[{nameof(AnimationAudioRelay)}] No {nameof(AudioSource)} found on {gameObject.name}.", this);
        }
    }

    private void CacheAudioManager()
    {
        if (audioManager != null)
            return;

#if UNITY_2023_1_OR_NEWER
        audioManager = FindAnyObjectByType<SceneAudioManager>();
#else
        audioManager = FindObjectOfType<SceneAudioManager>();
#endif

        if (audioManager == null)
        {
            Debug.LogWarning($"[{nameof(AnimationAudioRelay)}] No {nameof(SceneAudioManager)} found in the scene.", this);
        }
    }

    public void PlaySound(string soundId)
    {
        if (audioManager == null)
        {
            CacheAudioManager();
        }

        if (localAudioSource == null)
        {
            CacheAudioSource();
        }

        if (audioManager == null || localAudioSource == null)
            return;

        audioManager.PlayByName(soundId, localAudioSource);
    }

    public void PlaySoundByIndex(int index)
    {
        if (audioManager == null)
        {
            CacheAudioManager();
        }

        if (localAudioSource == null)
        {
            CacheAudioSource();
        }

        if (audioManager == null || localAudioSource == null)
            return;

        audioManager.PlayByIndex(index, localAudioSource);
    }

    public void StopLocalAudio()
    {
        if (localAudioSource == null)
        {
            CacheAudioSource();
        }

        if (localAudioSource == null)
            return;

        localAudioSource.Stop();
        localAudioSource.pitch = 1f;
    }
}
