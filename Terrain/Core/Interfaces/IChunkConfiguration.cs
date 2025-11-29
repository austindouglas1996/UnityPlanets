using UnityTerrainGenerator.EditorSupport;
using UnityTerrainGenerator.Engine;

namespace UnityTerrainGenerator.Core
{
    /// <summary>
    /// Contains config data for how chunks are sized, typed, and generated.
    /// Passed into generators and factories.
    /// </summary>
    public interface IChunkConfiguration
    {
        /// <summary>
        /// Simple debug options mostly for Unity editor.
        /// </summary>
        DebugOptions DebugOptions { get; }

        /// <summary>
        /// Density Options used for marching cubes generator.
        /// </summary>
        TerrainDensityOptions DensityOptions { get; }

        /// <summary>
        /// Density Options used for planet marching cubes.
        /// </summary>
        PlanetDensityOptions PlanetOptions { get; }

        /// <summary>
        /// Biomes to use when generating chunks.
        /// </summary>
        BiomeLibraryAsset BiomeLibrary { get; }

        /// <summary>
        /// Used for LOD thresholds on chunks for rendering.
        /// </summary>
        ChunkLODThresholdAsset LODThresholds { get; }
    }

}