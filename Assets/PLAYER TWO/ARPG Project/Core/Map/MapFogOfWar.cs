using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Map/Map Fog Of War")]
    public class MapFogOfWar : Singleton<MapFogOfWar>
    {
        /// <summary>
        /// Represents the fog of war visibility state of a map cell.
        /// </summary>
        public enum FogState : byte
        {
            /// <summary>The cell has never been visited and is hidden on the map.</summary>
            Hidden = 0,

            /// <summary>The cell has been visited and is revealed on the map.</summary>
            Discovered = 1,
        }

        [Header("Fog Of War Settings")]
        [Tooltip("The number of cells along one axis of the square fog grid.")]
        public int resolution = 256;

        [Tooltip("How many times per second the fog of war is updated.")]
        [Range(1, 60)]
        public int updateRate = 10;

        [Header("Texture Settings")]
        [Tooltip("The shader used to blur and colorize the fog texture on the GPU.")]
        public Shader fogShader;

        [Tooltip("The color used to represent hidden areas on the fog overlay.")]
        public Color hiddenColor = new(0, 0, 0, 1);

        [Tooltip("The color used to represent discovered areas on the fog overlay.")]
        public Color discoveredColor = new(0, 0, 0, 0);

        [Header("Fog Edge Settings")]
        [Tooltip("The softness of the fog edges in cell units. Set to 0 to disable.")]
        [Min(0)]
        public int edgeSoftness = 2;

        [Tooltip(
            "Shifts the fog edge inward (higher = smaller visible area). 0.5 compensates for blur expansion."
        )]
        [Range(0f, 1f)]
        public float edgeBias = 0.5f;

        protected FogState[] m_cells;

        /// <summary>
        /// The flat array of fog states for the grid. Length equals resolution * resolution.
        /// Index a cell at (x, y) with: cells[y * resolution + x].
        /// The getter automatically reallocates the buffer if the resolution has changed at runtime.
        /// </summary>
        public FogState[] cells
        {
            get
            {
                var expected = resolution * resolution;

                if (m_cells == null || m_cells.Length != expected)
                    m_cells = new FogState[expected];

                return m_cells;
            }
            set { m_cells = value; }
        }

        /// <summary>
        /// All revealers currently registered with the fog of war system.
        /// </summary>
        public List<MapRevealer> revealers { get; protected set; } = new();

        protected RenderTexture m_renderTexture;
        protected Texture2D m_texture;
        protected byte[] m_bytes;
        protected Material m_blurMaterial;
        protected float m_updateInterval;
        protected float m_timeSinceLastUpdate;

        /// <summary>
        /// The GPU render texture representing the current fog state.
        /// Assign this to any number of <see cref="MapFogOfWarRenderer"/> instances.
        /// The getter automatically recreates the texture if the resolution has changed at runtime.
        /// </summary>
        public RenderTexture renderTexture
        {
            get
            {
                if (m_renderTexture == null || m_renderTexture.width != resolution)
                {
                    if (m_renderTexture != null)
                        m_renderTexture.Release();

                    m_renderTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32);
                }

                return m_renderTexture;
            }
        }

        /// <summary>
        /// The CPU-side texture used to upload fog cell states to the GPU.
        /// The getter automatically recreates the texture if the resolution has changed at runtime.
        /// </summary>
        protected Texture2D Texture
        {
            get
            {
                if (m_texture == null || m_texture.width != resolution)
                {
                    if (m_texture != null)
                        Destroy(m_texture);

                    m_texture = new Texture2D(resolution, resolution, TextureFormat.R8, false)
                    {
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp,
                    };
                }

                return m_texture;
            }
        }

        /// <summary>
        /// The raw byte buffer written to the fog texture each update.
        /// The getter automatically reallocates the buffer if the resolution has changed at runtime.
        /// </summary>
        protected byte[] Bytes
        {
            get
            {
                var expected = resolution * resolution;

                if (m_bytes == null || m_bytes.Length != expected)
                    m_bytes = new byte[expected];

                return m_bytes;
            }
        }

        /// <summary>
        /// The material used to blur and colorize the fog texture on the GPU.
        /// The getter lazily creates the material on first access.
        /// </summary>
        protected Material BlurMaterial
        {
            get
            {
                if (m_blurMaterial == null)
                    m_blurMaterial = new Material(fogShader);

                return m_blurMaterial;
            }
        }

        protected override void Initialize()
        {
            m_updateInterval = 1f / updateRate;
            InitializeTexture();
        }

        protected virtual void InitializeTexture()
        {
            RefreshTexture();
        }

        protected virtual void OnDestroy()
        {
            if (m_renderTexture != null)
                m_renderTexture.Release();

            if (m_texture != null)
                Destroy(m_texture);

            if (m_blurMaterial != null)
                Destroy(m_blurMaterial);
        }

        protected virtual void LateUpdate()
        {
            if (revealers.Count == 0)
                return;

            m_timeSinceLastUpdate += Time.deltaTime;

            if (m_timeSinceLastUpdate < m_updateInterval)
                return;

            m_timeSinceLastUpdate = 0f;

            foreach (var revealer in revealers)
            {
                if (revealer)
                    RevealAt(revealer.transform.position, revealer.viewRadius);
            }

            RefreshTexture();
        }

        /// <summary>
        /// Registers a <see cref="MapRevealer"/> to participate in fog of war updates.
        /// Revealers with <see cref="MapRevealer.RevealMode.Once"/> reveal immediately and are not kept in the list.
        /// </summary>
        public virtual void AddRevealer(MapRevealer revealer)
        {
            if (revealer.mode == MapRevealer.RevealMode.Once)
            {
                RevealAt(revealer.transform.position, revealer.viewRadius);
                RefreshTexture();
                return;
            }

            if (!revealers.Contains(revealer))
                revealers.Add(revealer);
        }

        /// <summary>
        /// Unregisters a <see cref="MapRevealer"/> from fog of war updates.
        /// </summary>
        public virtual void RemoveRevealer(MapRevealer revealer)
        {
            revealers.Remove(revealer);
        }

        /// <summary>
        /// Uploads the current cell states to the GPU and blits through the blur shader
        /// into the <see cref="renderTexture"/>.
        /// </summary>
        public virtual void RefreshTexture()
        {
            for (var i = 0; i < cells.Length; i++)
                Bytes[i] = cells[i] == FogState.Hidden ? byte.MinValue : byte.MaxValue;

            Texture.SetPixelData(Bytes, 0);
            Texture.Apply();

            BlurMaterial.SetFloat("_BlurSize", edgeSoftness / (2f * resolution));
            BlurMaterial.SetFloat("_EdgeBias", edgeBias);
            BlurMaterial.SetColor("_HiddenColor", hiddenColor);
            BlurMaterial.SetColor("_DiscoveredColor", discoveredColor);

            Graphics.Blit(Texture, renderTexture, BlurMaterial);
        }

        /// <summary>
        /// Converts a world-space position to integer grid coordinates (x, y).
        /// Coordinates may be outside [0, resolution - 1] if the position is outside map bounds.
        /// </summary>
        /// <param name="worldPos">The world-space position to convert.</param>
        /// <returns>Integer grid coordinates corresponding to the position.</returns>
        public virtual (int x, int y) WorldToCell(Vector3 worldPos)
        {
            var map = Map.instance;
            var half = resolution * 0.5f;
            var x = Mathf.FloorToInt((worldPos.x - map.center.x) / map.length * resolution + half);
            var y = Mathf.FloorToInt((worldPos.z - map.center.z) / map.length * resolution + half);
            return (x, y);
        }

        /// <summary>
        /// Returns the fog state of a cell at grid coordinate (x, y).
        /// Returns <see cref="FogState.Hidden"/> if the coordinate is out of bounds.
        /// </summary>
        /// <param name="x">The column index of the cell.</param>
        /// <param name="y">The row index of the cell.</param>
        public virtual FogState GetCell(int x, int y)
        {
            if (x < 0 || x >= resolution || y < 0 || y >= resolution)
                return FogState.Hidden;

            return cells[y * resolution + x];
        }

        /// <summary>
        /// Reveals all fog cells within <paramref name="radius"/> world units of
        /// <paramref name="worldPos"/>, marking them as <see cref="FogState.Discovered"/>.
        /// </summary>
        /// <param name="worldPos">The center of the reveal area in world space.</param>
        /// <param name="radius">The reveal radius in world units.</param>
        public virtual void RevealAt(Vector3 worldPos, float radius)
        {
            var (cx, cy) = WorldToCell(worldPos);
            var radiusInCells = radius / Map.instance.length * resolution;
            var radiusSq = radiusInCells * radiusInCells;
            var radiusCeil = Mathf.CeilToInt(radiusInCells);

            var xMin = Mathf.Max(0, cx - radiusCeil);
            var xMax = Mathf.Min(resolution - 1, cx + radiusCeil);
            var yMin = Mathf.Max(0, cy - radiusCeil);
            var yMax = Mathf.Min(resolution - 1, cy + radiusCeil);

            for (var y = yMin; y <= yMax; y++)
            {
                var dy = y - cy;

                for (var x = xMin; x <= xMax; x++)
                {
                    var dx = x - cx;

                    if (dx * dx + dy * dy <= radiusSq)
                        cells[y * resolution + x] = FogState.Discovered;
                }
            }
        }
    }
    
}
