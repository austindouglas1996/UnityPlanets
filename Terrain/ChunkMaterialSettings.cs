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

        // Color vertices based on their world-space height.
        Height = 2,

        // Color vertices based on temperature values.
        Temperature = 3,

        // Color vertices based on humidity values.
        Humidity = 4,

        // Show foliage coverage using grayscale values.
        Foliage = 5,

        // Visualize the vertex direction vector.
        Direction = 6
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
