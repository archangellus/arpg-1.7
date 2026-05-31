using System;
using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class PetSummonOwnership : MonoBehaviour
    {
        private static readonly HashSet<PetSummonOwnership> s_activePets = new();

        public static event Action<bool> onActivePetChanged;

        public int ownerEntityId;

        public static bool HasActivePet
        {
            get
            {
                RemoveMissingPets();
                return s_activePets.Count > 0;
            }
        }

        public static Transform activePetTransform
        {
            get
            {
                RemoveMissingPets();

                foreach (var pet in s_activePets)
                    return pet.transform;

                return null;
            }
        }

        protected virtual void OnEnable()
        {
            var wasActive = HasActivePet;
            s_activePets.Add(this);
            RaiseActiveChangedIfNeeded(wasActive);
        }

        protected virtual void OnDisable()
        {
            var wasActive = HasActivePet;
            s_activePets.Remove(this);
            RaiseActiveChangedIfNeeded(wasActive);
        }

        private static void RaiseActiveChangedIfNeeded(bool wasActive)
        {
            var isActive = HasActivePet;

            if (wasActive != isActive)
                onActivePetChanged?.Invoke(isActive);
        }

        private static void RemoveMissingPets()
        {
            s_activePets.RemoveWhere(pet => !pet);
        }
    }
}
