using UnityEngine;

/// <summary>
/// Applies vertex colors to a mesh based on terrain data.
/// Used after mesh generation to color chunks (e.g., for height or biome visualization).
/// </summary>
public interface IChunkColorizer
{
    /// <summary>
    /// Retrieve the lerped color based on a position in world space.
    /// </summary>
    /// <param name="vertice"></param>
    /// <returns></returns>
    Color32 GetColorForVertice(Vector3 vertice);

    /// <summary>
    /// Applies generated chunk colors, along with colors found in <see cref="IChunkConfiguration.Modifiers"/>
    /// </summary>
    /// <param name="chunk"></param>
    /// <param name="localToWorld"></param>
    /// <param name="config"></param>
    void UpdateChunkColors(ChunkData chunk, Matrix4x4 localToWorld);
}
