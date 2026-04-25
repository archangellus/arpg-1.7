using System.Collections;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Character/Character Preview")]
    public class CharacterPreview : Singleton<CharacterPreview>
    {
        [Header("Character Preview Settings")]
        [Tooltip("The slot where the character preview will be displayed.")]
        public Transform characterSlot;

        protected Coroutine m_instantiateRoutine;
        protected Entity m_entity;

        /// <summary>
        /// Preview the character in the character slot.
        /// </summary>
        /// <param name="character">The character to preview.</param>
        public virtual void Preview(CharacterInstance character)
        {
            Clear();
            m_instantiateRoutine = StartCoroutine(InstantiateEntity(character));
        }

        /// <summary>
        /// Clear the character preview.
        /// </summary>
        public virtual void Clear()
        {
            if (!m_entity)
                return;

            StopInstantiation();
            DestroyImmediate(m_entity.gameObject);
        }

        protected virtual void StopInstantiation()
        {
            if (m_instantiateRoutine == null)
                return;

            StopCoroutine(m_instantiateRoutine);
            m_instantiateRoutine = null;
        }

        protected virtual IEnumerator InstantiateEntity(CharacterInstance character)
        {
            m_entity = character.Instantiate();
            m_entity.enabled = false;
            m_entity.inputs.enabled = false;
            m_entity.Teleport(characterSlot.position, characterSlot.rotation);
            m_entity.gameObject.SetActive(false);
            yield return new WaitForEndOfFrame();
            m_entity.gameObject.SetActive(true);
        }
    }
}
