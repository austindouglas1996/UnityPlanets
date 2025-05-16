using UnityEngine;

/// <summary>
/// Applies vertex colors to a mesh based on terrain data.
/// Used after mesh generation to color chunks (e.g., for height or biome visualization).
/// </summary>
public interface IChunkColorizer
{
    /// <summary>
    /// Applies colors to the given mesh based on the chunk's data and transform.
    /// </summary>
    /// <param name="meshData">The mesh data to apply colors to.</param>
    /// <param name="localToWorld">Transform matrix used to convert positions for color calculations.</param>
    /// <param name="configuration">Chunk configuration info.</param>
    /// <returns>An array of colors to assign to the mesh.</returns>
    Color[] GenerateVertexColors(MeshData meshData, Matrix4x4 localToWorld, IChunkConfiguration configuration);

    /// <summary>
    /// Applies generated chunk colors, along with colors found in <see cref="IChunkConfiguration.Modifiers"/>
    /// </summary>
    /// <param name="chunk"></param>
    /// <param name="localToWorld"></param>
    /// <param name="config"></param>
    void UpdateChunkColors(ChunkData chunk, Matrix4x4 localToWorld, IChunkConfiguration config);
}
