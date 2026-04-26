using System;
using System.Collections;
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
        [Tooltip("Use the state short name like 'Death' or the full path like 'Base Layer.Death'.")]
        public string watchedStateName;
        [Tooltip("1 = replace after one full playthrough of the watched state.")]
        public float replaceAfterNormalizedTime = 1f;

        [Header("Replacement")]
        [Tooltip("Can be a prefab or an existing disabled scene object.")]
        public GameObject replacementCharacter;
        [Tooltip("If true, the old character is disabled. If false, it is destroyed.")]
        public bool disableOriginalInsteadOfDestroy = true;
        [Tooltip("Copies the current character local scale to the replacement.")]
        public bool copyLocalScale = false;

        [Header("Performance")]
        [Tooltip("If the replacement is a prefab, it will be instantiated early and kept inactive to avoid frame spikes during the swap.")]
        public bool preloadPrefabReplacement = true;

        [NonSerialized] public bool hasReplaced;
        [NonSerialized] internal int watchedStateHash;
        [NonSerialized] internal GameObject preloadedInstance;
    }

    [Header("Characters")]
    [SerializeField] private List<CharacterSwapEntry> characters = new();

    [Header("Preload Settings")]
    [SerializeField] private bool preloadPrefabReplacementsOnStart = true;
    [SerializeField] private bool spreadPreloadAcrossFrames = true;
    [SerializeField] private Transform preloadContainer;

    private void Awake()
    {
        InitializeEntries();

        if (preloadContainer == null)
            preloadContainer = transform;
    }

    private void Start()
    {
        if (preloadPrefabReplacementsOnStart)
            StartCoroutine(PreloadReplacementsCoroutine());
    }

    private void OnValidate()
    {
        InitializeEntries();
    }

    private void Update()
    {
        for (int i = 0; i < characters.Count; i++)
        {
            TryReplaceCharacter(characters[i]);
        }
    }

    private void InitializeEntries()
    {
        if (characters == null)
            return;

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

    private IEnumerator PreloadReplacementsCoroutine()
    {
        for (int i = 0; i < characters.Count; i++)
        {
            PreloadReplacement(characters[i]);

            if (spreadPreloadAcrossFrames)
                yield return null;
        }
    }

    private void PreloadReplacement(CharacterSwapEntry entry)
    {
        if (entry == null || entry.replacementCharacter == null)
            return;

        if (!entry.preloadPrefabReplacement)
            return;

        if (entry.preloadedInstance != null)
            return;

        // If this is already a scene object, we do not instantiate it.
        if (entry.replacementCharacter.scene.IsValid())
            return;

        entry.preloadedInstance = Instantiate(entry.replacementCharacter, preloadContainer);
        entry.preloadedInstance.SetActive(false);
    }

    private void TryReplaceCharacter(CharacterSwapEntry entry)
    {
        if (entry == null || entry.hasReplaced)
            return;

        if (entry.currentCharacter == null || entry.replacementCharacter == null)
            return;

        if (entry.replacementCharacter == entry.currentCharacter)
        {
            Debug.LogWarning($"[{nameof(ReplaceCharacterWhenAnimationEnds)}] Replacement character cannot be the same as the current character.", this);
            return;
        }

        Animator anim = entry.animator != null ? entry.animator : entry.currentCharacter.GetComponent<Animator>();
        if (anim == null)
            return;

        if (entry.animatorLayer < 0 || entry.animatorLayer >= anim.layerCount)
            return;

        if (string.IsNullOrWhiteSpace(entry.watchedStateName))
            return;

        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(entry.animatorLayer);

        bool isMatchingState =
            stateInfo.IsName(entry.watchedStateName) ||
            stateInfo.shortNameHash == entry.watchedStateHash ||
            stateInfo.fullPathHash == entry.watchedStateHash;

        if (!isMatchingState)
            return;

        if (stateInfo.normalizedTime < entry.replaceAfterNormalizedTime)
            return;

        ReplaceCharacter(entry);
    }

    private void ReplaceCharacter(CharacterSwapEntry entry)
    {
        Transform source = entry.currentCharacter.transform;
        GameObject replacementInstance = GetReplacementInstance(entry);

        if (replacementInstance == null)
            return;

        replacementInstance.transform.SetParent(source.parent, true);
        replacementInstance.transform.SetPositionAndRotation(source.position, source.rotation);

        if (entry.copyLocalScale)
            replacementInstance.transform.localScale = source.localScale;

        if (!replacementInstance.activeSelf)
            replacementInstance.SetActive(true);

        Animator replacementAnimator = replacementInstance.GetComponent<Animator>();
        if (replacementAnimator != null)
        {
            replacementAnimator.Rebind();
            replacementAnimator.Update(0f);
        }

        entry.hasReplaced = true;

        if (entry.disableOriginalInsteadOfDestroy)
            entry.currentCharacter.SetActive(false);
        else
            Destroy(entry.currentCharacter);
    }

    private GameObject GetReplacementInstance(CharacterSwapEntry entry)
    {
        if (entry.replacementCharacter == null)
            return null;

        // Existing scene object assigned in inspector
        if (entry.replacementCharacter.scene.IsValid())
            return entry.replacementCharacter;

        // Preloaded prefab instance
        if (entry.preloadedInstance != null)
            return entry.preloadedInstance;

        // Fallback if preload was disabled or not finished yet
        return Instantiate(entry.replacementCharacter);
    }

    [ContextMenu("Reset Replacement Flags")]
    public void ResetReplacementFlags()
    {
        foreach (var entry in characters)
        {
            if (entry == null)
                continue;

            entry.hasReplaced = false;
        }
    }
}