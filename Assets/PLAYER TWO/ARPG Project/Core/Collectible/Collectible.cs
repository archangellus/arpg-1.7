using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider))]
    public abstract class Collectible : Interactive
    {
        [Header("GUI Name Settings")]
        [Tooltip("The color of the text on the GUI Collectible.")]
        public Color nameColor = Color.white;

        public UnityEvent onCollect;

        protected virtual void InitializeCanvas()
        {
            if (GUICollectibles.instance)
                GUICollectibles.instance.Add(this);
        }

        protected override void InitializeTag() => tag = GameTags.Collectible;

        /// <summary>
        /// Collects this Collectible.
        /// </summary>
        /// <param name="other">The object that is collecting this Collectible.</param>
        public virtual void Collect(object other)
        {
            onCollect.Invoke();

            if (GUICollectibles.instance)
                GUICollectibles.instance.Remove(this);

            Destroy(gameObject);
        }

        protected override void OnInteract(object other)
        {
            if (other is Entity && TryCollect((other as Entity).inventory.instance))
                Collect(other);
        }

        /// <summary>
        /// Returns the name of the Item on the Collectible.
        /// </summary>
        public abstract string GetName();

        /// <summary>
        /// Returns the color used to display this Collectible's name label.
        /// Defaults to <see cref="nameColor"/>; subclasses can override to drive color from data.
        /// </summary>
        public virtual Color GetNameColor() => nameColor;

        protected abstract bool TryCollect(Inventory inventory);

        protected override void Start()
        {
            base.Start();
            InitializeCanvas();
        }
    }
}
