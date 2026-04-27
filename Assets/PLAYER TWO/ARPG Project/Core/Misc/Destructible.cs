using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Destructible")]
    public class Destructible : MonoBehaviour
    {
        public UnityEvent OnHit;
        public UnityEvent OnDestruct;

        [Header("General Settings")]
        [Tooltip("The amount of hits it can take before breaking.")]
        public int hitPoints = 100;

        [Tooltip("The minimum duration in seconds between hits.")]
        public float maxHitRate = 0.15f;

        [Tooltip("The collider trigger used to give this Destructible a bigger clicking area.")]
        public Collider triggerCollider;

        [Header("Breaking Settings")]
        [Tooltip("If true, this Game Object is automatically disabled when destroyed.")]
        public bool disableOnBreak = true;

        [Tooltip("The duration in seconds before disabling this Game Object.")]
        public float disableDelay;

        [Tooltip("A reference to the Game Object used when the object is not destroyed.")]
        public GameObject regularObject;

        [Tooltip("A reference to the Game Object used when the object is destroyed.")]
        public GameObject crackedObject;

        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when this object gets hit.")]
        public AudioClip hitAudio;

        [Tooltip("The Audio Clip that plays when this object gets destroyed.")]
        public AudioClip breakingAudio;

        protected float m_lastHitTime;

        protected WaitForSeconds m_waitForDisableDelay;

        protected const string k_crackedObjectParentName = "Cracked Objects";

        protected GameAudio m_audio => GameAudio.instance;

        protected virtual void InitializeWaits()
        {
            m_waitForDisableDelay = new WaitForSeconds(disableDelay);
        }

        protected virtual void InitializeRigidbody()
        {
            if (!TryGetComponent(out Rigidbody _))
            {
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
            }
        }

        protected virtual void InitializeTag() => tag = GameTags.Destructible;

        /// <summary>
        /// Damages this Destructible object.
        /// </summary>
        /// <param name="amount">The amount of damage received.</param>
        public virtual void Damage(int amount)
        {
            if (hitPoints == 0 || Time.time <= m_lastHitTime + maxHitRate)
                return;

            if (hitPoints > 0)
                m_audio.PlayEffect(hitAudio);

            m_lastHitTime = Time.time;
            hitPoints = Mathf.Max(hitPoints - amount, 0);
            OnHit.Invoke();

            if (hitPoints == 0)
                StartCoroutine(DestroyRoutine());
        }

        protected virtual IEnumerator DestroyRoutine()
        {
            if (crackedObject)
            {
                regularObject.SetActive(false);
                crackedObject.SetActive(true);
            }

            m_audio.PlayEffect(breakingAudio);
            triggerCollider.SafeCall(c => c.enabled = false);
            OnDestruct.Invoke();

            if (disableOnBreak)
            {
                yield return m_waitForDisableDelay;
                gameObject.SetActive(false);
            }
        }

        protected virtual void Start()
        {
            InitializeWaits();
            InitializeRigidbody();
            InitializeTag();
        }
    }
}
