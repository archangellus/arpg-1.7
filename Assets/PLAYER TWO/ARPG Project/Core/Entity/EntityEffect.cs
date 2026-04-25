using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Abstract base for all entity effects. Holds shared display and particle data only.
    /// Use <see cref="EntityBuff"/> for positive stat modifiers
    /// or <see cref="EntityDebuff"/> for negative ones.
    /// </summary>
    public abstract class EntityEffect : ScriptableObject
    {
        [Header("General")]
        [Tooltip("The display name of this effect. Used for labels and UI.")]
        public string effectName;

        [Tooltip("The icon representing this effect in the UI.")]
        public Sprite icon;

        [Min(0f)]
        [Tooltip("The duration in seconds this effect remains active. Set to 0 for permanent.")]
        public float duration = 5f;

        [Header("Particle")]
        [Tooltip("Particle system prefab instantiated on the entity while this effect is active.")]
        public GameObject particlePrefab;

        [Tooltip("Local position offset applied to the particle instance relative to the entity.")]
        public Vector3 particlePositionOffset;

        [Tooltip(
            "Local rotation offset applied to the particle instance, expressed as Euler angles."
        )]
        public Vector3 particleRotationOffset;

        /// <summary>
        /// Returns a human-readable string describing all stat modifiers this effect applies.
        /// Used by UI components to display effect details without manual description fields.
        /// </summary>
        public abstract string GetModifiersText();
    }
}
