using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Map))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Map/Map Baker")]
    public class MapBaker : MonoBehaviour
    {
        [Header("Bake Settings")]
        [Tooltip("The name of the generated map texture file.")]
        public string fileName = "Map Texture";

        [Tooltip("The resolution of the map texture.")]
        public Vector2Int resolution = new(1024, 1024);

        [Tooltip("The color of the background of the map (map borders).")]
        public Color backgroundColor = Color.black;

        [Tooltip("All the layers that will be captured by the map image.")]
        public LayerMask cullingMask = -1;

        protected Map m_map;

        public Map map
        {
            get
            {
                if (m_map == null)
                    m_map = GetComponent<Map>();

                return m_map;
            }
        }
    }
}
