using System;
using System.Collections.Generic;
using UnityEngine;

public class ReplaceCharacterWhenAnimationEnds : MonoBehaviour
{
    [Serializable]
    public class CharacterSwapEntry
    {
        public string entryName;

        [Header("Source")]
        public GameObject currentCharacter;
        public Animator animator;
        [Min(0)] public int animatorLayer = 0;

        [Header("Watched Animation State")]
        [Tooltip("Use the state short name like 'Death' or full path like 'Base Layer.Death'.")]
        public string watchedStateName;
        [Tooltip("1 = replace after one full playthrough of the watched state.")]
        public float replaceAfterNormalizedTime = 1f;

        [Header("Replacement")]
        [Tooltip("Can be a prefab or an existing disabled scene object.")]
        public GameObject replacementCharacter;
        [Tooltip("If true, the old character is disabled. If false, it is destroyed.")]
        public bool disableOriginalInsteadOfDestroy = true;

        [Header("VFX While Animation Is Playing")]
        public bool useVfxWhileWatchedStatePlays = false;
        [Tooltip("Default is false. If enabled, the VFX can replay while the watched state is still active.")]
        public bool loopVfxWhileStatePlays = false;
        [Tooltip("Can be a prefab or an existing disabled scene object.")]
        public GameObject vfxObject;
        [Tooltip("Optional anchor for the VFX. If none is assigned, the current character transform is used.")]
        public Transform vfxAnchor;
        [Tooltip("If true, the VFX follows the anchor while the animation plays.")]
        public bool parentVfxToAnchor = true;
        [Tooltip("If true and the VFX is a spawned prefab, it will be destroyed when the watched state ends.")]
        public bool destroySpawnedVfxWhenStateEnds = true;

        [NonSerialized] public bool hasReplaced;
        [NonSerialized] internal int watchedStateHash;

        [NonSerialized] internal GameObject runtimeVfxInstance;
        [NonSerialized] internal ParticleSystem[] runtimeParticleSystems;
        [NonSerialized] internal bool runtimeVfxUsesSceneObject;
        [NonSerialized] internal bool runtimeWasWatchedStatePlayingLastFrame;
        [NonSerialized] internal bool runtimeVfxTriggeredThisState;
    }

    [SerializeField] private List<CharacterSwapEntry> characters = new();

    private void Awake()
    {
        InitializeEntries();
    }

    private void OnValidate()
    {
        InitializeEntries();
    }

    private void Update()
    {
        for (int i = 0; i < characters.Count; i++)
        {
            CharacterSwapEntry entry = characters[i];
            if (entry == null)
                continue;

            bool isWatchedStatePlaying = false;
            Animator animator;
            AnimatorStateInfo stateInfo;

            if (!entry.hasReplaced)
            {
                isWatchedStatePlaying = TryGetWatchedStateInfo(entry, out animator, out stateInfo);

                HandleWatchedStateVfx(entry, isWatchedStatePlaying);

                if (isWatchedStatePlaying && stateInfo.normalizedTime >= entry.replaceAfterNormalizedTime)
                {
                    ReplaceCharacter(entry);
                }
            }
            else
            {
                HandleWatchedStateVfx(entry, false);
            }
        }
    }

    private void OnDisable()
    {
        CleanupAllVfx();
    }

    private void OnDestroy()
    {
        CleanupAllVfx();
    }

    private void InitializeEntries()
    {
        foreach (var entry in characters)
        {
            if (entry == null)
                continue;

            if (entry.currentCharacter != null && entry.animator == null)
                entry.animator = entry.currentCharacter.GetComponent<Animator>();

            entry.watchedStateHash = string.IsNullOrWhiteSpace(entry.watchedStateName)
                ? 0
                : Animator.StringToHash(entry.watchedStateName);
        }
    }

    private bool TryGetWatchedStateInfo(CharacterSwapEntry entry, out Animator anim, out AnimatorStateInfo stateInfo)
    {
        anim = null;
        stateInfo = default;

        if (entry == null)
            return false;

        if (entry.currentCharacter == null)
            return false;

        if (string.IsNullOrWhiteSpace(entry.watchedStateName))
            return false;

        anim = entry.animator != null ? entry.animator : entry.currentCharacter.GetComponent<Animator>();
        if (anim == null)
            return false;

        if (entry.animatorLayer < 0 || entry.animatorLayer >= anim.layerCount)
            return false;

        stateInfo = anim.GetCurrentAnimatorStateInfo(entry.animatorLayer);

        bool isMatchingState =
            stateInfo.IsName(entry.watchedStateName) ||
            stateInfo.shortNameHash == entry.watchedStateHash ||
            stateInfo.fullPathHash == entry.watchedStateHash;

        return isMatchingState;
    }

    private void HandleWatchedStateVfx(CharacterSwapEntry entry, bool isWatchedStatePlaying)
    {
        if (entry == null || !entry.useVfxWhileWatchedStatePlays || entry.vfxObject == null)
            return;

        if (isWatchedStatePlaying)
        {
            bool stateJustStarted = !entry.runtimeWasWatchedStatePlayingLastFrame;

            if (stateJustStarted)
            {
                PlayVfxOnce(entry);
                entry.runtimeVfxTriggeredThisState = true;
            }
            else if (entry.loopVfxWhileStatePlays)
            {
                TryReplayVfxIfFinished(entry);
            }
        }
        else
        {
            if (entry.runtimeWasWatchedStatePlayingLastFrame)
            {
                StopOrHideVfx(entry);
            }

            entry.runtimeVfxTriggeredThisState = false;
        }

        entry.runtimeWasWatchedStatePlayingLastFrame = isWatchedStatePlaying;
    }

    private void PlayVfxOnce(CharacterSwapEntry entry)
    {
        if (entry.runtimeVfxInstance == null)
        {
            CreateOrAssignVfxInstance(entry);
        }

        if (entry.runtimeVfxInstance == null)
            return;

        PositionVfx(entry);

        if (!entry.runtimeVfxInstance.activeSelf)
            entry.runtimeVfxInstance.SetActive(true);

        if (entry.runtimeParticleSystems == null || entry.runtimeParticleSystems.Length == 0)
            entry.runtimeParticleSystems = entry.runtimeVfxInstance.GetComponentsInChildren<ParticleSystem>(true);

        if (entry.runtimeParticleSystems.Length > 0)
        {
            for (int i = 0; i < entry.runtimeParticleSystems.Length; i++)
            {
                ParticleSystem ps = entry.runtimeParticleSystems[i];
                if (ps == null)
                    continue;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }
    }

    private void TryReplayVfxIfFinished(CharacterSwapEntry entry)
    {
        if (entry.runtimeVfxInstance == null)
            return;

        PositionVfx(entry);

        if (!entry.runtimeVfxInstance.activeSelf)
            entry.runtimeVfxInstance.SetActive(true);

        if (entry.runtimeParticleSystems == null || entry.runtimeParticleSystems.Length == 0)
            entry.runtimeParticleSystems = entry.runtimeVfxInstance.GetComponentsInChildren<ParticleSystem>(true);

        if (entry.runtimeParticleSystems.Length == 0)
            return;

        bool anyAlive = false;

        for (int i = 0; i < entry.runtimeParticleSystems.Length; i++)
        {
            ParticleSystem ps = entry.runtimeParticleSystems[i];
            if (ps != null && ps.IsAlive(true))
            {
                anyAlive = true;
                break;
            }
        }

        if (!anyAlive)
        {
            for (int i = 0; i < entry.runtimeParticleSystems.Length; i++)
            {
                ParticleSystem ps = entry.runtimeParticleSystems[i];
                if (ps == null)
                    continue;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }
    }

    private void CreateOrAssignVfxInstance(CharacterSwapEntry entry)
    {
        if (entry.vfxObject == null)
            return;

        if (entry.vfxObject.scene.IsValid())
        {
            entry.runtimeVfxInstance = entry.vfxObject;
            entry.runtimeVfxUsesSceneObject = true;
        }
        else
        {
            entry.runtimeVfxInstance = Instantiate(entry.vfxObject);
            entry.runtimeVfxUsesSceneObject = false;
        }

        if (entry.runtimeVfxInstance != null)
        {
            entry.runtimeParticleSystems = entry.runtimeVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    private void PositionVfx(CharacterSwapEntry entry)
    {
        if (entry.runtimeVfxInstance == null)
            return;

        Transform anchor = entry.vfxAnchor != null
            ? entry.vfxAnchor
            : (entry.currentCharacter != null ? entry.currentCharacter.transform : null);

        if (anchor == null)
            return;

        if (entry.parentVfxToAnchor)
        {
            entry.runtimeVfxInstance.transform.SetParent(anchor, false);
            entry.runtimeVfxInstance.transform.localPosition = Vector3.zero;
            entry.runtimeVfxInstance.transform.localRotation = Quaternion.identity;
        }
        else
        {
            entry.runtimeVfxInstance.transform.SetParent(null, true);
            entry.runtimeVfxInstance.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }
    }

    private void StopOrHideVfx(CharacterSwapEntry entry)
    {
        if (entry == null || entry.runtimeVfxInstance == null)
            return;

        if (entry.runtimeParticleSystems == null || entry.runtimeParticleSystems.Length == 0)
            entry.runtimeParticleSystems = entry.runtimeVfxInstance.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < entry.runtimeParticleSystems.Length; i++)
        {
            if (entry.runtimeParticleSystems[i] != null)
            {
                entry.runtimeParticleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (entry.runtimeVfxUsesSceneObject)
        {
            entry.runtimeVfxInstance.SetActive(false);
        }
        else
        {
            if (entry.destroySpawnedVfxWhenStateEnds)
            {
                Destroy(entry.runtimeVfxInstance);
                entry.runtimeVfxInstance = null;
                entry.runtimeParticleSystems = null;
            }
            else
            {
                entry.runtimeVfxInstance.SetActive(false);
            }
        }
    }

    private void ReplaceCharacter(CharacterSwapEntry entry)
    {
        if (entry.currentCharacter == null || entry.replacementCharacter == null)
            return;

        if (entry.replacementCharacter == entry.currentCharacter)
        {
            Debug.LogWarning($"[{nameof(ReplaceCharacterWhenAnimationEnds)}] Replacement character cannot be the same as the current character.", this);
            return;
        }

        StopOrHideVfx(entry);

        Transform source = entry.currentCharacter.transform;
        GameObject replacementInstance;

        if (entry.replacementCharacter.scene.IsValid())
        {
            replacementInstance = entry.replacementCharacter;
            replacementInstance.transform.SetParent(source.parent, true);
            replacementInstance.transform.SetPositionAndRotation(source.position, source.rotation);
            replacementInstance.SetActive(true);
        }
        else
        {
            replacementInstance = Instantiate(
                entry.replacementCharacter,
                source.position,
                source.rotation,
                source.parent
            );
        }

        entry.hasReplaced = true;

        if (entry.disableOriginalInsteadOfDestroy)
            entry.currentCharacter.SetActive(false);
        else
            Destroy(entry.currentCharacter);
    }

    private void CleanupAllVfx()
    {
        foreach (var entry in characters)
        {
            if (entry == null)
                continue;

            if (entry.runtimeVfxInstance == null)
                continue;

            if (!entry.runtimeVfxUsesSceneObject)
            {
                Destroy(entry.runtimeVfxInstance);
            }
            else
            {
                entry.runtimeVfxInstance.SetActive(false);
            }

            entry.runtimeVfxInstance = null;
            entry.runtimeParticleSystems = null;
            entry.runtimeWasWatchedStatePlayingLastFrame = false;
            entry.runtimeVfxTriggeredThisState = false;
        }
    }

    [ContextMenu("Reset Replacement Flags")]
    public void ResetReplacementFlags()
    {
        foreach (var entry in characters)
        {
            if (entry == null)
                continue;

            entry.hasReplaced = false;
            entry.runtimeWasWatchedStatePlayingLastFrame = false;
            entry.runtimeVfxTriggeredThisState = false;
        }
    }
}