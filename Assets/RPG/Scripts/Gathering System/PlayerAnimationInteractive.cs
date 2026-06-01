using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Player Animation Interactive")]
    public class PlayerAnimationInteractive : Interactive
    {
        [Header("Player Animation Settings")]
        [Tooltip("If true, this Interactive triggers an animation on the player when interacted with.")]
        public bool triggerPlayerAnimation = true;

        [Tooltip(
            "Optional Animator override. If empty, the Animator is searched on the interacting player."
        )]
        public Animator playerAnimator;

        [Tooltip("The player Animator trigger that is set when interacting with this object.")]
        public string playerAnimationTrigger = "OnInteract";

        [Tooltip("If true, resets the trigger before setting it to restart the animation cleanly.")]
        public bool resetPlayerAnimationTriggerBeforePlay;

        [Tooltip(
            "If true, skips the normal Interactive behavior and only triggers the player animation."
        )]
        public bool disableNormalInteractionAction;

        protected virtual Animator GetPlayerAnimator(object other)
        {
            if (playerAnimator)
                return playerAnimator;

            if (other is Animator animator)
                return animator;

            if (other is Component component)
                return component.GetComponentInChildren<Animator>();

            if (other is GameObject gameObject)
                return gameObject.GetComponentInChildren<Animator>();

            if (Level.instance && Level.instance.player)
                return Level.instance.player.GetComponentInChildren<Animator>();

            return null;
        }

        protected virtual void PlayPlayerAnimation(object other)
        {
            if (!triggerPlayerAnimation || string.IsNullOrEmpty(playerAnimationTrigger))
                return;

            var animator = GetPlayerAnimator(other);

            if (!animator)
                return;

            if (resetPlayerAnimationTriggerBeforePlay)
                animator.ResetTrigger(playerAnimationTrigger);

            animator.SetTrigger(playerAnimationTrigger);
        }

        protected override void OnInteract(object other)
        {
            PlayPlayerAnimation(other);
        }

        public override void Interact(object other = null)
        {
            if (!interactive)
                return;

            if (disableNormalInteractionAction)
            {
                PlayPlayerAnimation(other);
                return;
            }

            base.Interact(other);
        }
    }
}