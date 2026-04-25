using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Map/Map Revealer")]
    public class MapRevealer : MonoBehaviour
    {
        /// <summary>
        /// Controls how often this revealer contributes to the fog of war.
        /// </summary>
        public enum RevealMode
        {
            /// <summary>Reveals the fog around its position on every map update tick.</summary>
            Continuous,

            /// <summary>Reveals the fog once when the revealer is registered, then stops.</summary>
            Once,
        }

        [Header("Revealer Settings")]
        [Tooltip("The world-unit radius around this object that is revealed on the map.")]
        public float viewRadius = 20f;

        [Tooltip(
            "Continuous reveals on every map update. "
                + "Once reveals a single time when the revealer is registered."
        )]
        public RevealMode mode = RevealMode.Continuous;

        protected virtual void Start()
        {
            if (MapFogOfWar.instance)
                MapFogOfWar.instance.AddRevealer(this);
        }

        protected virtual void OnDestroy()
        {
            if (MapFogOfWar.instance)
                MapFogOfWar.instance.RemoveRevealer(this);
        }
    }
}
