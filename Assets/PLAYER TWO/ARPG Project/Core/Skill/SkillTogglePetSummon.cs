using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Toggle Pet Summon Skill", menuName = "PLAYER TWO/ARPG Project/Skills/Toggle Pet Summon Skill")]
    public class SkillTogglePetSummon : Skill
    {
        [Header("Pet Summon Settings")]
        [Tooltip("Pet prefab to spawn/despawn when the skill is used.")]
        public GameObject petPrefab;

        [Tooltip("Spawn distance in front of the caster.")]
        public float spawnDistanceFromCaster = 1.5f;

        private static readonly Dictionary<int, GameObject> activePetsByCaster = new();

        public override GameObject Cast(Entity caster, Vector3 position, Quaternion rotation)
        {
            if (caster == null)
                return null;

            int casterId = caster.GetInstanceID();

            if (activePetsByCaster.TryGetValue(casterId, out var existingPet) && existingPet != null)
            {
                Destroy(existingPet);
                activePetsByCaster.Remove(casterId);
                UpdateCasterUi(caster, false);
                PlaySkillSound();
                return null;
            }

            if (petPrefab == null)
                return null;

            var forward = caster.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.001f)
                forward = Vector3.forward;

            var spawnPosition = caster.transform.position + forward.normalized * spawnDistanceFromCaster;
            var pet = Instantiate(petPrefab, spawnPosition, Quaternion.identity);

            if (pet.TryGetComponent(out AIController aiController))
                aiController.playerTAG = caster.tag;

            activePetsByCaster[casterId] = pet;
            UpdateCasterUi(caster, true);
            PlaySkillSound();

            return pet;
        }

        private void UpdateCasterUi(Entity caster, bool petActive)
        {
            var toggleUI = caster.GetComponent<PetSkillToggleUI>();
            if (toggleUI != null)
                toggleUI.SetPetActiveState(petActive);
        }

        private void PlaySkillSound()
        {
            if (sound != null && GameAudio.instance != null)
                GameAudio.instance.PlayEffect(sound);
        }
    }
}
