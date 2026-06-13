using GingerVoxelSystem.EditorSupport;
using GingerVoxelSystem.Engine.Options;

namespace GingerVoxelSystem.Core
{
    /// <summary>
    /// Contains config data for how chunks are sized, typed, and generated.
    /// Passed into generators and factories.
    /// </summary>
    public interface IChunkConfiguration
    {
        /// <summary>
        /// Density Options used for marching cubes generator.
        /// </summary>
        TerrainDensityOptions DensityOptions { get; }

        /// <summary>
        /// Used for LOD thresholds on chunks for rendering.
        /// </summary>
        ChunkLODThresholdAsset LODThresholds { get; }
    }

}