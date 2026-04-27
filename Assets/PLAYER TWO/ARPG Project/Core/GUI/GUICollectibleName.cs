using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Collectible Name")]
    public class GUICollectibleName
        : MonoBehaviour,
            IPointerClickHandler,
            IPointerEnterHandler,
            IPointerExitHandler
    {
        [Tooltip("A reference to the Text component.")]
        public Text itemName;

        [Tooltip("Sprite displayed on the highlight image when the pointer is not hovering.")]
        public Sprite normalSprite;

        [Tooltip(
            "Sprite displayed on the highlight image when the pointer hovers over this label."
        )]
        public Sprite hoverSprite;

        [Tooltip("The offset applied this object is instantiated.")]
        public Vector3 offset = new Vector3(0, 0.5f, 0);

        /// <summary>The Collectible this label is tracking.</summary>
        public Collectible target => m_target;

        /// <summary>The RectTransform of this label.</summary>
        public RectTransform rectTransform => transform as RectTransform;

        /// <summary>The world Transform this label is tracking for screen-space positioning.</summary>
        public Transform worldTransform => m_worldTransform;

        /// <summary>
        /// Vertical screen-pixel offset applied on top of the raw world-to-screen position
        /// to push this label clear of overlapping neighbors. Written by GUICollectibles.
        /// </summary>
        public float overlapOffset;

        /// <summary>
        /// Spawn order assigned by GUICollectibles; used as a stable tiebreaker
        /// when multiple labels share the same screen Y position.
        /// </summary>
        public int spawnOrder;

        protected Collectible m_target;
        protected ItemInstance m_item;
        protected Transform m_worldTransform;
        protected Camera m_camera;
        protected Image m_image;
        protected CanvasGroup m_canvasGroup;

        protected virtual void Awake()
        {
            m_image = GetComponent<Image>();
            InitializeCanvasGroup();
        }

        protected virtual void InitializeCamera() => m_camera = Camera.main;

        protected virtual void InitializeCanvasGroup()
        {
            if (!TryGetComponent(out m_canvasGroup))
                m_canvasGroup = gameObject.AddComponent<CanvasGroup>();

            m_canvasGroup.alpha = 0;
        }

        /// <summary>
        /// Hides this label and disables pointer interaction without removing it from the tracked list.
        /// </summary>
        public virtual void Hide()
        {
            m_canvasGroup.alpha = 0;
            m_canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Sets the Collectible and its text color.
        /// </summary>
        /// <param name="collectible">The Collectible you want to set.</param>
        /// <param name="color">The color of the text.</param>
        public virtual void SetCollectible(Collectible collectible, Color color)
        {
            if (!collectible)
                return;

            m_target = collectible;
            m_worldTransform = collectible.transform;
            itemName.color = color;
            itemName.text = collectible.GetName();
        }

        /// <summary>
        /// Sets an Item Instance and the world Transform to track in screen space.
        /// </summary>
        /// <param name="item">The Item Instance whose name will be displayed.</param>
        /// <param name="worldTransform">The world Transform used for screen-space positioning.</param>
        /// <param name="color">The color of the text.</param>
        public virtual void SetItem(ItemInstance item, Transform worldTransform, Color color)
        {
            m_item = item;
            m_worldTransform = worldTransform;
            itemName.color = color;
            itemName.text = item != null && item.data != null ? item.data.name : string.Empty;
        }

        /// <summary>
        /// Assigns the tracked Collectible as the interactive target of the player,
        /// causing them to move toward and collect it.
        /// </summary>
        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (!Level.instance || GUI.instance.selected)
                return;

            var player = Level.instance.player;

            if (!player)
                return;

            Interactive interactive = m_target;

            if (!interactive && m_worldTransform)
                interactive = m_worldTransform.GetComponent<Interactive>();

            if (!interactive)
                return;

            player.targetInteractive = interactive;
            player.MoveTo(interactive.transform.position);
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            if (m_image && !GUI.instance.selected)
                m_image.sprite = hoverSprite;
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            if (m_image)
                m_image.sprite = normalSprite;
        }

        protected virtual void Start()
        {
            InitializeCamera();
            StartCoroutine(FadeInRoutine());
        }

        /// <summary>
        /// Fades the label in from alpha 0 to 1, using the <see cref="GUICollectibles.showUpFadeTime"/>
        /// as the duration so all labels share the same timing setting.
        /// </summary>
        protected virtual IEnumerator FadeInRoutine()
        {
            float duration = GUICollectibles.instance
                ? GUICollectibles.instance.showUpFadeTime
                : 0.15f;

            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                m_canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / duration);
                yield return null;
            }

            m_canvasGroup.alpha = 1f;
        }

        protected virtual void LateUpdate()
        {
            if (!m_worldTransform)
                Destroy(gameObject);
        }
    }
}
