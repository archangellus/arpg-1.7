using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public struct SpacePoint
    {
        public Vector3 position;
        public Quaternion rotation;

        public static SpacePoint Zero => new(Vector3.zero, Quaternion.identity);

        public SpacePoint(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }
    }
}
