using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/Entity Effects GUI")]
    public class EntityEffectsGUI : MonoBehaviour
    {
        [Header("Containers")]
        [Tooltip("The RectTransform that holds the list of buff icons.")]
        public RectTransform buffsContainer;

        [Tooltip("The RectTransform that holds the list of debuff icons.")]
        public RectTransform debuffsContainer;

        [Header("Prefab")]
        [Tooltip("The base prefab instantiated for each effect icon.")]
        public EntityEffectIcon iconPrefab;

        protected Entity m_entity;
        protected EntityEffectManager m_effectManager;

        protected List<EntityEffectIcon> m_buffIcons = new();
        protected List<EntityEffectIcon> m_debuffIcons = new();

        protected virtual void InitializeEntity() => m_entity = Level.instance.player;

        protected virtual void InitializeManager() =>
            m_effectManager = m_entity.GetComponent<EntityEffectManager>();

        protected virtual void InitializeCallbacks() =>
            m_effectManager.onEffectsChanged.AddListener(Refresh);

        /// <summary>
        /// Returns an icon from the pool at the given index, creating a new instance if needed.
        /// </summary>
        protected virtual EntityEffectIcon GetOrCreate(
            List<EntityEffectIcon> pool,
            RectTransform container,
            int index
        )
        {
            if (index < pool.Count)
            {
                pool[index].gameObject.SetActive(true);
                return pool[index];
            }

            var icon = Instantiate(iconPrefab, container);
            pool.Add(icon);
            return icon;
        }

        /// <summary>
        /// Refreshes all effect icons based on the current active effects from the effect manager.
        /// </summary>
        public virtual void Refresh()
        {
            var buffIndex = 0;
            var debuffIndex = 0;

            foreach (var instance in m_effectManager.activeEffects)
            {
                if (instance.data is EntityBuff)
                {
                    var icon = GetOrCreate(m_buffIcons, buffsContainer, buffIndex);
                    icon.SetEffect(instance);
                    buffIndex++;
                }
                else
                {
                    var icon = GetOrCreate(m_debuffIcons, debuffsContainer, debuffIndex);
                    icon.SetEffect(instance);
                    debuffIndex++;
                }
            }

            for (int i = buffIndex; i < m_buffIcons.Count; i++)
                m_buffIcons[i].gameObject.SetActive(false);

            for (int i = debuffIndex; i < m_debuffIcons.Count; i++)
                m_debuffIcons[i].gameObject.SetActive(false);
        }

        protected virtual void Start()
        {
            InitializeEntity();
            InitializeManager();
            InitializeCallbacks();
            Refresh();
        }
    }
}
