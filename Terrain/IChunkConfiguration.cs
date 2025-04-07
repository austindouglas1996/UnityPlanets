/// <summary>
/// Contains config data for how chunks are sized, typed, and generated.
/// Passed into generators and factories.
/// </summary>
public interface IChunkConfiguration
{
    /// <summary>
    /// Size (in voxels) for one side of a chunk.
    /// </summary>
    int ChunkSize { get; }

    /// <summary>
    /// Density Options used for marching cubes generator.
    /// </summary>
    DensityMapOptions MapOptions { get; }
}
