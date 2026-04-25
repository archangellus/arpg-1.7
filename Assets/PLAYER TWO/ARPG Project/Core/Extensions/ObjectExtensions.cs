using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public static class ObjectExtensions
    {
        /// <summary>
        /// Safely gets a value from a Object using Unity's null check.
        /// </summary>
        public static TResult SafeGet<TUnity, TResult>(
            this TUnity obj,
            System.Func<TUnity, TResult> getter
        )
            where TUnity : Object
        {
            return obj ? getter(obj) : default;
        }

        /// <summary>
        /// Safely calls an action on a Object using Unity's null check.
        /// </summary>
        public static void SafeCall<TUnity>(this TUnity obj, System.Action<TUnity> action)
            where TUnity : Object
        {
            if (obj)
                action(obj);
        }
    }
}
