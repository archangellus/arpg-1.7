using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Damage Text")]
    public class DamageText : MonoBehaviour
    {
        [Header("Text Settings")]
        [Tooltip("A reference to the Text component.")]
        public Text damageText;

        [Tooltip("The text used when the damage is zero.")]
        public string missText = "Miss";

        [Tooltip("The text used when the Entity is immune to the incoming damage type.")]
        public string immuneText = "Immune";

        [Header("Color Settings")]
        [Tooltip("The color of the text for misses and immunity.")]
        public Color neutralColor = Color.white;

        [Tooltip("The color of the text when receiving damage.")]
        public Color regularColor = Color.yellow;

        [Tooltip("The color of the text when the damage is critical.")]
        public Color criticalColor = Color.red;

        [Tooltip("The color of the text when receiving fire damage.")]
        public Color fireColor = new(1f, 0.5f, 0f);

        [Tooltip("The color of the text when receiving ice damage.")]
        public Color iceColor = new(0.4f, 0.7f, 1f);

        [Tooltip("The color of the text when receiving poison damage.")]
        public Color poisonColor = new(0.6f, 0.2f, 0.8f);

        [Tooltip("The color of the text when damage is reflected back to the attacker.")]
        public Color reflectedColor = new(0f, 0.9f, 0.4f);

        protected float m_lifeTime;

        /// <summary>
        /// The transform of the object receiving damage.
        /// </summary>
        public Transform target { get; set; }

        /// <summary>
        /// Sets the damage text from an <see cref="EntityDamageInfo"/>, applying the correct
        /// color based on elemental damage type and whether the hit was critical.
        /// </summary>
        /// <param name="info">The damage info from the resolved hit.</param>
        public virtual void SetText(EntityDamageInfo info)
        {
            damageText.text = info.amount > 0 ? info.amount.ToString() : missText;
            damageText.color = GetColor(info);
        }

        /// <summary>
        /// Sets the text to the miss string with the neutral color.
        /// </summary>
        public virtual void SetMissText()
        {
            damageText.text = missText;
            damageText.color = neutralColor;
        }

        /// <summary>
        /// Sets the text to the immune string with the neutral color.
        /// </summary>
        public virtual void SetImmuneText()
        {
            damageText.text = immuneText;
            damageText.color = neutralColor;
        }

        protected virtual Color GetColor(EntityDamageInfo info)
        {
            if (info.amount <= 0)
                return neutralColor;

            if (info.isReflected)
                return reflectedColor;

            return info.primaryDamageType switch
            {
                DamageType.Fire => fireColor,
                DamageType.Ice => iceColor,
                DamageType.Poison => poisonColor,
                _ => info.critical ? criticalColor : regularColor,
            };
        }

        protected virtual void LateUpdate()
        {
            if (m_lifeTime > 0.5f)
            {
                Destroy(this.gameObject);
            }

            m_lifeTime += Time.deltaTime;
            transform.position += Vector3.up * Time.deltaTime;
        }
    }
}
