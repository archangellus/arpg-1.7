using System;

namespace PLAYERTWO.ARPGProject.UnitySerializer
{
    [Serializable]
    public class Vector3
    {
        public float x, y, z;

        public Vector3(UnityEngine.Vector3 vector)
        {
            x = vector.x;
            y = vector.y;
            z = vector.z;
        }

        public UnityEngine.Vector3 ToUnity()
        {
            return new UnityEngine.Vector3(x, y, z);
        }
    }
}
