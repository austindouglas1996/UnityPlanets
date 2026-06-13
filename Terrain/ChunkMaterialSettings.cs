namespace GingerVoxelSystem
{
    using UnityEngine;

    /// <summary>
    /// A list of available debug styles available when rendering chunks.
    /// </summary>
    public enum ChunkOverlay
    {
        // Use the material's default colors.
        None = 0,

        // Visualize chunks by their LOD level.
        LOD = 1,

        // The calculated normal lighting.
        Normals = 7,

        // Colors based on Vertex.
        Vertex = 8
    }

    /// <summary>
    /// Provides debug-related material settings for chunk rendering.
    /// Allows switching overlay modes without regenerating chunks,
    /// which is normally required when using debug options.
    /// </summary>
    public class ChunkMaterialSettings : MonoBehaviour
    {
        [Tooltip("Debug overlay to visualize chunk data (LOD, height, temperature, etc.).")]
        public ChunkOverlay Overlay;

        [Tooltip("Material used when rendering chunks. Supports setting debug overlay modes.")]
        [SerializeField] public Material BaseMaterial;

        private void OnValidate()
        {
            if (Application.isPlaying && BaseMaterial != null)
                this.BaseMaterial.SetInt("Overlay", (int)Overlay);
        }
    }
}
