using System.Collections.Generic;
using PLAYERTWO.ARPGProject;
using UnityEngine;

/// <summary>
/// Rotates a selected bone while a valid target stays inside this object's trigger.
/// Supports Humanoid bones through Animator, or any generic Transform bone.
///
/// When an Animator is driving the bone, this script applies an additive local rotation offset
/// on top of the animated pose in LateUpdate so the base animation can keep playing.
///
/// It can also temporarily override the character's current animation with random clips while
/// tracking a target, optionally only once on enter, and optionally play a random exit clip.
/// </summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class BoneTriggerLookAt : MonoBehaviour
{
    public enum BoneSource
    {
        Humanoid,
        Generic,
    }

    public enum RotationAxis
    {
        X,
        Y,
        Z,
    }

    public enum ReferenceDirection
    {
        Forward,
        Back,
        Right,
        Left,
        Up,
        Down,
    }

    [Header("Bone")]
    [Tooltip("Choose whether the bone comes from a Humanoid Animator or a direct Transform reference.")]
    public BoneSource boneSource = BoneSource.Humanoid;

    [Tooltip("Animator used to resolve a Humanoid bone.")]
    public Animator humanoidAnimator;

    [Tooltip("Humanoid bone to rotate when Bone Source is set to Humanoid.")]
    public HumanBodyBones humanoidBone = HumanBodyBones.Head;

    [Tooltip("Transform to rotate when Bone Source is set to Generic.")]
    public Transform genericBone;

    [Header("Detection")]
    [Tooltip("Only objects with one of these tags can drive the rotation.")]
    public List<string> targetTags = new() { "Player" };

    [Tooltip("Optional world-space offset applied to the tracked target position.")]
    public Vector3 targetOffset;

    [Tooltip("If true, the closest valid target inside the trigger is followed.")]
    public bool useClosestTarget = true;

    [Header("Rotation")]
    [Tooltip("Which local axis of the bone is allowed to rotate.")]
    public RotationAxis rotationAxis = RotationAxis.Y;

    [Tooltip("Which local direction represents the bone's default forward direction.")]
    public ReferenceDirection referenceDirection = ReferenceDirection.Forward;

    [Tooltip("The maximum allowed angle from the default pose. If exceeded, the bone returns to its default rotation.")]
    [Min(0f)]
    public float maxRotationAngle = 60f;

    [Tooltip("How fast the bone interpolates toward its desired rotation.")]
    [Min(0f)]
    public float rotationSpeed = 360f;

    [Tooltip("If true and an Animator is driving the bone, the current animated pose is used as the base each frame.")]
    public bool useAnimatedPoseAsBase = true;

    [Tooltip("If true, the bone smoothly returns to its default pose after all valid targets leave the trigger.")]
    public bool resetRotationOnExit = true;

    [Tooltip("Update in LateUpdate so this happens after Animator pose evaluation.")]
    public bool updateInLateUpdate = true;

    [Header("Animation Override")]
    [Tooltip("If true, the script can override the current Animator animation while tracking a target.")]
    public bool overrideAnimationWhenTracking;

    [Tooltip("Animator used to play the override animations. If empty, the script will try to use the same Animator driving the bone.")]
    public Animator animationAnimator;

    [Tooltip("Full name of the Animator state that should receive the temporary override clip. Example: Base Layer.Override")]
    public string overrideStateName = "Base Layer.Override";

    [Tooltip("Name of the clip in the Animator Override Controller that should be replaced at runtime.")]
    public string overrideClipName = "Override Clip";

    [Tooltip("How long the Animator crossfade takes when playing an override clip.")]
    [Min(0f)]
    public float animationCrossFadeDuration = 0.1f;

    [Tooltip("Random clips that can play while a valid target is inside the trigger.")]
    public List<AnimationClip> facingAnimations = new();

    [Tooltip("If true, only one random facing animation is played when a valid target enters the trigger. It will not repeat until all valid targets exit and a new enter happens.")]
    public bool playFacingAnimationOnlyOnEnter;

    [Tooltip("If true, override animations are only allowed during the first valid trigger occupancy. Starting from the second entry, no facing or exit override animation will play again.")]
    public bool playAnimationOnlyOnce;

    [Tooltip("Random clips that can play when all valid targets have exited the trigger.")]
    public List<AnimationClip> exitAnimations = new();

    [Tooltip("If true, the Animator returns to the previously playing state after the exit animation finishes.")]
    public bool returnToPreviousStateAfterExitAnimation = true;

    [Header("Quest Conditions")]
    [Tooltip("If enabled, override animations will stop and no new override clips will play after all quests assigned in the Quest Giver are completed.")]
    public bool stopAnimationWhenQuestGiverCompleted;

    [Tooltip("Quest Giver used to check whether all assigned quests are completed. If left empty, this script will try to find one on the same GameObject.")]
    public QuestGiver questGiver;

    [Header("Animation Linked Object")]
    [Tooltip("If true, a scene object is enabled when an override animation starts and disabled after it ends.")]
    public bool toggleObjectWithOverrideAnimation;

    [Tooltip("A scene object to enable while the override animation is active.")]
    public GameObject objectToToggleWithAnimation;

    [Tooltip("Extra time in seconds to keep the object active after the override animation has finished.")]
    [Min(0f)]
    public float objectExtraActiveTime;

    protected readonly Dictionary<Transform, int> m_targetOverlaps = new();
    protected readonly List<Transform> m_cleanupBuffer = new();

    protected Transform m_bone;
    protected Animator m_boneAnimator;
    protected AnimatorOverrideController m_runtimeOverrideController;
    protected RuntimeAnimatorController m_originalRuntimeAnimatorController;
    protected Collider m_trigger;

    protected Quaternion m_initialLocalRotation;
    protected Quaternion m_currentOffsetLocal = Quaternion.identity;
    protected Quaternion m_desiredOffsetLocal = Quaternion.identity;
    protected Quaternion m_lastBaseLocalRotation;

    protected bool m_initialized;
    protected bool m_animationInitialized;
    protected bool m_isOccupancyActive;
    protected bool m_hasPlayedFacingAnimationThisOccupancy;
    protected bool m_isPlayingExitAnimation;
    protected bool m_waitingToRestorePreviousAnimatorState;
    protected bool m_hasStoredPreviousAnimatorState;

    protected int m_previousAnimatorStateHash;
    protected float m_previousAnimatorNormalizedTime;
    protected float m_currentOverrideAnimationEndTime;

    protected AnimationClip m_lastFacingClip;
    protected AnimationClip m_lastExitClip;

    protected int m_totalOccupancyCount;
    protected bool m_isAnimationLinkedObjectActive;
    protected float m_animationLinkedObjectDisableTime;

    /// <summary>
    /// The current resolved bone transform.
    /// </summary>
    public Transform bone => m_bone;

    /// <summary>
    /// The currently selected target inside the trigger, if any.
    /// </summary>
    public Transform currentTarget { get; protected set; }

    protected virtual void Awake()
    {
        Initialize();
    }

    protected virtual void Reset()
    {
        if (TryGetComponent(out Collider collider))
            collider.isTrigger = true;
    }

    protected virtual void Update()
    {
        if (!updateInLateUpdate)
            HandleTracking();
    }

    protected virtual void LateUpdate()
    {
        if (updateInLateUpdate)
            HandleTracking();
    }

    protected virtual void OnDisable()
    {
        currentTarget = null;
        m_targetOverlaps.Clear();
        m_cleanupBuffer.Clear();

        m_isOccupancyActive = false;
        m_hasPlayedFacingAnimationThisOccupancy = false;
        m_isPlayingExitAnimation = false;
        m_waitingToRestorePreviousAnimatorState = false;

        if (m_bone)
        {
            m_currentOffsetLocal = Quaternion.identity;
            m_desiredOffsetLocal = Quaternion.identity;
            m_bone.localRotation = m_initialLocalRotation;
        }

        SetAnimationLinkedObjectActive(false, immediate: true);
        ClearStoredPreviousAnimatorState();
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!TryGetValidTargetRoot(other, out Transform targetRoot))
            return;

        if (!m_targetOverlaps.ContainsKey(targetRoot))
            m_targetOverlaps[targetRoot] = 0;

        m_targetOverlaps[targetRoot]++;
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (!TryGetValidTargetRoot(other, out Transform targetRoot))
            return;

        if (!m_targetOverlaps.ContainsKey(targetRoot))
            return;

        m_targetOverlaps[targetRoot]--;

        if (m_targetOverlaps[targetRoot] <= 0)
            m_targetOverlaps.Remove(targetRoot);
    }

    /// <summary>
    /// Re-captures the current local rotation as the new default rotation.
    /// Useful if you change the setup at runtime.
    /// </summary>
    [ContextMenu("Capture Current Rotation As Default")]
    public virtual void CaptureCurrentRotationAsDefault()
    {
        ResolveBone();

        if (!m_bone)
            return;

        m_initialLocalRotation = m_bone.localRotation;
        m_lastBaseLocalRotation = m_initialLocalRotation;
        m_currentOffsetLocal = Quaternion.identity;
        m_desiredOffsetLocal = Quaternion.identity;
    }

    /// <summary>
    /// Forces the script to resolve the bone again.
    /// Useful if the animator or target bone changes at runtime.
    /// </summary>
    [ContextMenu("Refresh Bone")]
    public virtual void RefreshBone()
    {
        ResolveBone();
        InitializeAnimationOverride();

        if (!m_bone)
            return;

        m_initialLocalRotation = m_bone.localRotation;
        m_lastBaseLocalRotation = m_initialLocalRotation;
        m_currentOffsetLocal = Quaternion.identity;
        m_desiredOffsetLocal = Quaternion.identity;
    }

    /// <summary>
    /// Resets the internal counter used by Play Animation Only Once.
    /// After calling this, the next valid entry can play override animations again.
    /// </summary>
    [ContextMenu("Reset Play Animation Only Once Counter")]
    public virtual void ResetPlayAnimationOnlyOnceCounter()
    {
        m_totalOccupancyCount = 0;
    }

    protected virtual void Initialize()
    {
        if (m_initialized)
            return;

        m_trigger = GetComponent<Collider>();
        m_trigger.isTrigger = true;


        if (stopAnimationWhenQuestGiverCompleted && !questGiver)
            questGiver = GetComponent<QuestGiver>();


        ResolveBone();
        InitializeAnimationOverride();

        if (m_bone)
        {
            m_initialLocalRotation = m_bone.localRotation;
            m_lastBaseLocalRotation = m_initialLocalRotation;
        }
        else
        {
            Debug.LogWarning($"{nameof(BoneTriggerLookAt)} on '{name}' could not resolve a bone.", this);
        }

        m_initialized = true;
    }

    protected virtual void ResolveBone()
    {
        m_bone = boneSource switch
        {
            BoneSource.Humanoid => ResolveHumanoidBone(),
            BoneSource.Generic => genericBone,
            _ => null,
        };

        m_boneAnimator = ResolveDrivingAnimator();
    }

    protected virtual Transform ResolveHumanoidBone()
    {
        if (!humanoidAnimator)
            return null;

        if (!humanoidAnimator.isHuman)
        {
            Debug.LogWarning(
                $"{nameof(BoneTriggerLookAt)} on '{name}' is set to Humanoid, but the assigned Animator is not Humanoid.",
                this
            );
            return null;
        }

        return humanoidAnimator.GetBoneTransform(humanoidBone);
    }

    protected virtual Animator ResolveDrivingAnimator()
    {
        if (boneSource == BoneSource.Humanoid)
            return humanoidAnimator;

        if (!genericBone)
            return null;

        return genericBone.GetComponentInParent<Animator>();
    }

    protected virtual Animator ResolveAnimationAnimator()
    {
        if (animationAnimator)
            return animationAnimator;

        if (m_boneAnimator)
            return m_boneAnimator;

        return null;
    }

    protected virtual void InitializeAnimationOverride()
    {
        m_animationInitialized = true;

        if (!overrideAnimationWhenTracking)
            return;

        animationAnimator = ResolveAnimationAnimator();

        if (!animationAnimator)
        {
            Debug.LogWarning(
                $"{nameof(BoneTriggerLookAt)} on '{name}' could not resolve an Animator for animation overrides.",
                this
            );
            return;
        }

        if (!animationAnimator.runtimeAnimatorController)
        {
            Debug.LogWarning(
                $"{nameof(BoneTriggerLookAt)} on '{name}' could not initialize animation overrides because the Animator has no Runtime Animator Controller.",
                this
            );
            return;
        }

        if (m_runtimeOverrideController != null && animationAnimator.runtimeAnimatorController == m_runtimeOverrideController)
            return;

        m_originalRuntimeAnimatorController = animationAnimator.runtimeAnimatorController;
        m_runtimeOverrideController = new AnimatorOverrideController(m_originalRuntimeAnimatorController);
        animationAnimator.runtimeAnimatorController = m_runtimeOverrideController;
    }

    protected virtual void HandleTracking()
    {
        if (!m_initialized)
            Initialize();

        if (!m_bone)
            return;

        CleanupDeadTargets();
        UpdateOccupancyState();
        currentTarget = GetBestTarget();

        var baseLocalRotation = GetBaseLocalRotation();
        m_desiredOffsetLocal = GetDesiredOffset(baseLocalRotation, currentTarget);
        RotateBoneTowardsDesired(baseLocalRotation);

        HandleAnimationState();
        UpdateAnimationLinkedObjectState();
    }

    protected virtual Quaternion GetBaseLocalRotation()
    {
        bool animatorDrivesBone =
            useAnimatedPoseAsBase
            && m_boneAnimator
            && m_boneAnimator.enabled
            && m_boneAnimator.isActiveAndEnabled;

        if (animatorDrivesBone)
        {
            m_lastBaseLocalRotation = m_bone.localRotation;
            return m_lastBaseLocalRotation;
        }

        m_lastBaseLocalRotation = m_initialLocalRotation;
        return m_lastBaseLocalRotation;
    }

    protected virtual void CleanupDeadTargets()
    {
        m_cleanupBuffer.Clear();

        foreach (var pair in m_targetOverlaps)
        {
            if (pair.Key == null || pair.Value <= 0)
                m_cleanupBuffer.Add(pair.Key);
        }

        for (int i = 0; i < m_cleanupBuffer.Count; i++)
            m_targetOverlaps.Remove(m_cleanupBuffer[i]);
    }

    protected virtual void UpdateOccupancyState()
    {
        bool hasTargets = m_targetOverlaps.Count > 0;

        if (hasTargets && !m_isOccupancyActive)
            BeginOccupancy();
        else if (!hasTargets && m_isOccupancyActive)
            EndOccupancy();
    }

    protected virtual void BeginOccupancy()
    {
        m_isOccupancyActive = true;
        m_totalOccupancyCount++;
        m_hasPlayedFacingAnimationThisOccupancy = false;
        m_isPlayingExitAnimation = false;
        m_waitingToRestorePreviousAnimatorState = false;


        if (overrideAnimationWhenTracking && CanPlayOverrideAnimationsThisOccupancy() && !ShouldStopOverrideAnimationsForCompletedQuests())
            PlayFacingAnimationOnOccupancyStart();

    }

    protected virtual void EndOccupancy()
    {
        m_isOccupancyActive = false;
        currentTarget = null;
        m_hasPlayedFacingAnimationThisOccupancy = false;

        if (!overrideAnimationWhenTracking)
            return;

        if (!CanPlayOverrideAnimationsThisOccupancy())
        {
            m_isPlayingExitAnimation = false;
            m_waitingToRestorePreviousAnimatorState = false;

            if (returnToPreviousStateAfterExitAnimation)
                RestorePreviousAnimatorState();
            else
                ClearStoredPreviousAnimatorState();

            return;
        }

        if (TryGetRandomClip(exitAnimations, m_lastExitClip, out var exitClip))
        {
            PlayOverrideClip(exitClip, rememberPreviousState: !m_hasStoredPreviousAnimatorState);
            m_lastExitClip = exitClip;
            m_isPlayingExitAnimation = true;
            m_waitingToRestorePreviousAnimatorState = returnToPreviousStateAfterExitAnimation;
            return;
        }

        m_isPlayingExitAnimation = false;
        m_waitingToRestorePreviousAnimatorState = false;

        if (returnToPreviousStateAfterExitAnimation)
            RestorePreviousAnimatorState();
        else
            ClearStoredPreviousAnimatorState();
    }

    protected virtual bool CanPlayOverrideAnimationsThisOccupancy()
    {
        return !playAnimationOnlyOnce || m_totalOccupancyCount <= 1;
    }

    protected virtual Transform GetBestTarget()
    {
        if (m_targetOverlaps.Count == 0)
            return null;

        Transform best = null;
        float bestDistanceSqr = float.PositiveInfinity;

        foreach (var pair in m_targetOverlaps)
        {
            var candidate = pair.Key;

            if (!candidate)
                continue;

            if (!useClosestTarget)
                return candidate;

            float distanceSqr = (candidate.position - transform.position).sqrMagnitude;

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                best = candidate;
            }
        }

        return best;
    }

    protected virtual Quaternion GetDesiredOffset(Quaternion baseLocalRotation, Transform target)
    {
        if (!target)
            return resetRotationOnExit ? Quaternion.identity : m_currentOffsetLocal;

        Transform parent = m_bone.parent;
        Vector3 worldTargetPosition = target.position + targetOffset;
        Vector3 worldDirection = worldTargetPosition - m_bone.position;

        if (worldDirection.sqrMagnitude <= Mathf.Epsilon)
            return resetRotationOnExit ? Quaternion.identity : m_currentOffsetLocal;

        Vector3 targetDirectionInParentSpace = parent
            ? parent.InverseTransformDirection(worldDirection.normalized)
            : worldDirection.normalized;

        Vector3 axisLocal = GetAxisVector(rotationAxis);
        Vector3 axisInParentSpace = baseLocalRotation * axisLocal;
        Vector3 referenceInParentSpace = baseLocalRotation * GetReferenceVector(referenceDirection);

        Vector3 flattenedReference = Vector3.ProjectOnPlane(referenceInParentSpace, axisInParentSpace);
        Vector3 flattenedTarget = Vector3.ProjectOnPlane(targetDirectionInParentSpace, axisInParentSpace);

        if (flattenedReference.sqrMagnitude <= Mathf.Epsilon || flattenedTarget.sqrMagnitude <= Mathf.Epsilon)
            return resetRotationOnExit ? Quaternion.identity : m_currentOffsetLocal;

        float angle = Vector3.SignedAngle(
            flattenedReference.normalized,
            flattenedTarget.normalized,
            axisInParentSpace.normalized
        );

        if (Mathf.Abs(angle) > maxRotationAngle)
            return resetRotationOnExit ? Quaternion.identity : m_currentOffsetLocal;

        return Quaternion.AngleAxis(angle, axisLocal);
    }

    protected virtual void RotateBoneTowardsDesired(Quaternion baseLocalRotation)
    {
        if (rotationSpeed <= 0f)
        {
            m_currentOffsetLocal = m_desiredOffsetLocal;
            m_bone.localRotation = baseLocalRotation * m_currentOffsetLocal;
            return;
        }

        float step = rotationSpeed * Time.deltaTime;
        m_currentOffsetLocal = Quaternion.RotateTowards(m_currentOffsetLocal, m_desiredOffsetLocal, step);
        m_bone.localRotation = baseLocalRotation * m_currentOffsetLocal;
    }

    protected virtual void HandleAnimationState()
    {
        if (!overrideAnimationWhenTracking || !m_animationInitialized || !animationAnimator || !m_runtimeOverrideController)
            return;

        if (ShouldStopOverrideAnimationsForCompletedQuests())
        {
            StopOverrideAnimationsImmediately();
            return;
        }


        if (m_isOccupancyActive)
        {
            if (CanPlayOverrideAnimationsThisOccupancy()
                && !playFacingAnimationOnlyOnEnter
                && !m_isPlayingExitAnimation
                && Time.time >= m_currentOverrideAnimationEndTime)
            {
                PlayNextFacingAnimation();
            }

            return;
        }

        if (m_isPlayingExitAnimation && Time.time >= m_currentOverrideAnimationEndTime)
        {
            m_isPlayingExitAnimation = false;

            if (m_waitingToRestorePreviousAnimatorState)
                RestorePreviousAnimatorState();
            else
                ClearStoredPreviousAnimatorState();
        }
    }

    protected virtual bool ShouldStopOverrideAnimationsForCompletedQuests()
    {
        if (!stopAnimationWhenQuestGiverCompleted || !questGiver)
            return false;

        return questGiver.state == QuestGiver.State.None;
    }

    protected virtual void StopOverrideAnimationsImmediately()
    {
        m_isPlayingExitAnimation = false;
        m_currentOverrideAnimationEndTime = 0f;
        m_waitingToRestorePreviousAnimatorState = false;

        if (returnToPreviousStateAfterExitAnimation)
            RestorePreviousAnimatorState();
        else
            ClearStoredPreviousAnimatorState();

        SetAnimationLinkedObjectActive(false, immediate: true);
    }


    protected virtual void PlayFacingAnimationOnOccupancyStart()
    {
        if (playFacingAnimationOnlyOnEnter)
        {
            if (m_hasPlayedFacingAnimationThisOccupancy)
                return;

            if (PlayNextFacingAnimation())
                m_hasPlayedFacingAnimationThisOccupancy = true;

            return;
        }

        PlayNextFacingAnimation();
    }

    protected virtual bool PlayNextFacingAnimation()
    {
        // If the quests are completed, we shouldn't play any new override animations, but if we're already playing one, we should let it finish instead of abruptly stopping it.
        if (ShouldStopOverrideAnimationsForCompletedQuests())
            return false;


        if (!TryGetRandomClip(facingAnimations, m_lastFacingClip, out var facingClip))
            return false;

        PlayOverrideClip(facingClip, rememberPreviousState: !m_hasStoredPreviousAnimatorState);
        m_lastFacingClip = facingClip;
        m_isPlayingExitAnimation = false;
        m_waitingToRestorePreviousAnimatorState = false;
        return true;
    }

    protected virtual void PlayOverrideClip(AnimationClip clip, bool rememberPreviousState)
    {
        if (!clip || !animationAnimator || m_runtimeOverrideController == null)
            return;

        if (string.IsNullOrWhiteSpace(overrideClipName))
        {
            Debug.LogWarning(
                $"{nameof(BoneTriggerLookAt)} on '{name}' cannot play override animations because Override Clip Name is empty.",
                this
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(overrideStateName))
        {
            Debug.LogWarning(
                $"{nameof(BoneTriggerLookAt)} on '{name}' cannot play override animations because Override State Name is empty.",
                this
            );
            return;
        }

        if (rememberPreviousState)
            StorePreviousAnimatorState();

        try
        {
            m_runtimeOverrideController[overrideClipName] = clip;
        }
        catch
        {
            Debug.LogWarning(
                $"{nameof(BoneTriggerLookAt)} on '{name}' could not find an override clip named '{overrideClipName}' in the Animator Override Controller.",
                this
            );
            return;
        }

        float clipDuration = GetClipDuration(clip);

        animationAnimator.CrossFadeInFixedTime(overrideStateName, animationCrossFadeDuration, 0, 0f);
        m_currentOverrideAnimationEndTime = Time.time + clipDuration;
        NotifyOverrideAnimationStarted(clipDuration);
    }


    protected virtual void NotifyOverrideAnimationStarted(float clipDuration)
    {
        if (!toggleObjectWithOverrideAnimation || !objectToToggleWithAnimation)
            return;

        SetAnimationLinkedObjectActive(true);
        m_animationLinkedObjectDisableTime = Time.time + Mathf.Max(0f, clipDuration) + Mathf.Max(0f, objectExtraActiveTime);
    }

    protected virtual void UpdateAnimationLinkedObjectState()
    {
        if (!m_isAnimationLinkedObjectActive)
            return;

        if (Time.time >= m_animationLinkedObjectDisableTime)
            SetAnimationLinkedObjectActive(false);
    }

    protected virtual void SetAnimationLinkedObjectActive(bool value, bool immediate = false)
    {
        if (objectToToggleWithAnimation)
            objectToToggleWithAnimation.SetActive(value);

        m_isAnimationLinkedObjectActive = value;

        if (!value || immediate)
            m_animationLinkedObjectDisableTime = 0f;
    }

    protected virtual void StorePreviousAnimatorState()
    {
        if (!animationAnimator || m_hasStoredPreviousAnimatorState)
            return;

        var currentState = animationAnimator.GetCurrentAnimatorStateInfo(0);
        m_previousAnimatorStateHash = currentState.fullPathHash;
        m_previousAnimatorNormalizedTime = Mathf.Repeat(currentState.normalizedTime, 1f);
        m_hasStoredPreviousAnimatorState = true;
    }

    protected virtual void RestorePreviousAnimatorState()
    {
        if (!animationAnimator)
        {
            ClearStoredPreviousAnimatorState();
            return;
        }

        if (m_hasStoredPreviousAnimatorState)
            animationAnimator.CrossFadeInFixedTime(m_previousAnimatorStateHash, animationCrossFadeDuration, 0, m_previousAnimatorNormalizedTime);

        ClearStoredPreviousAnimatorState();
    }

    protected virtual void ClearStoredPreviousAnimatorState()
    {
        m_hasStoredPreviousAnimatorState = false;
        m_previousAnimatorStateHash = 0;
        m_previousAnimatorNormalizedTime = 0f;
        m_waitingToRestorePreviousAnimatorState = false;
    }

    protected virtual float GetClipDuration(AnimationClip clip)
    {
        if (!clip)
            return 0f;

        float animatorSpeed = animationAnimator ? Mathf.Abs(animationAnimator.speed) : 1f;

        if (animatorSpeed <= Mathf.Epsilon)
            animatorSpeed = 1f;

        return clip.length / animatorSpeed;
    }

    protected virtual bool TryGetRandomClip(List<AnimationClip> clips, AnimationClip lastClip, out AnimationClip clip)
    {
        clip = null;

        if (clips == null || clips.Count == 0)
            return false;

        int validCount = 0;

        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i])
                validCount++;
        }

        if (validCount == 0)
            return false;

        if (validCount == 1)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i])
                {
                    clip = clips[i];
                    return true;
                }
            }
        }

        const int maxAttempts = 8;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = clips[Random.Range(0, clips.Count)];

            if (!candidate)
                continue;

            if (candidate == lastClip)
                continue;

            clip = candidate;
            return true;
        }

        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] && clips[i] != lastClip)
            {
                clip = clips[i];
                return true;
            }
        }

        return false;
    }

    protected virtual bool TryGetValidTargetRoot(Collider other, out Transform targetRoot)
    {
        targetRoot = null;

        if (!other)
            return false;

        Transform candidate = ResolveTargetRoot(other);

        if (!candidate || candidate == transform)
            return false;

        if (!HasValidTag(candidate))
            return false;

        targetRoot = candidate;
        return true;
    }

    protected virtual Transform ResolveTargetRoot(Collider other)
    {
        if (other.attachedRigidbody)
            return FindTaggedTransformInHierarchy(other.attachedRigidbody.transform);

        return FindTaggedTransformInHierarchy(other.transform);
    }

    protected virtual Transform FindTaggedTransformInHierarchy(Transform start)
    {
        Transform current = start;

        while (current != null)
        {
            if (HasValidTag(current))
                return current;

            current = current.parent;
        }

        return null;
    }

    protected virtual bool HasValidTag(Transform candidate)
    {
        if (!candidate || targetTags == null || targetTags.Count == 0)
            return false;

        for (int i = 0; i < targetTags.Count; i++)
        {
            string tagName = targetTags[i];

            if (string.IsNullOrWhiteSpace(tagName))
                continue;

            if (candidate.CompareTag(tagName))
                return true;
        }

        return false;
    }

    protected static Vector3 GetAxisVector(RotationAxis axis)
    {
        return axis switch
        {
            RotationAxis.X => Vector3.right,
            RotationAxis.Y => Vector3.up,
            RotationAxis.Z => Vector3.forward,
            _ => Vector3.up,
        };
    }

    protected static Vector3 GetReferenceVector(ReferenceDirection direction)
    {
        return direction switch
        {
            ReferenceDirection.Forward => Vector3.forward,
            ReferenceDirection.Back => Vector3.back,
            ReferenceDirection.Right => Vector3.right,
            ReferenceDirection.Left => Vector3.left,
            ReferenceDirection.Up => Vector3.up,
            ReferenceDirection.Down => Vector3.down,
            _ => Vector3.forward,
        };
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        Collider trigger = GetComponent<Collider>();

        if (!trigger)
            return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;

        switch (trigger)
        {
            case SphereCollider sphere:
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                break;
            case BoxCollider box:
                Gizmos.DrawWireCube(box.center, box.size);
                break;
            case CapsuleCollider capsule:
                Gizmos.DrawWireSphere(capsule.center, capsule.radius);
                break;
        }
    }
#endif
}
