using UnityEngine;

/// <summary>
/// Provides utilities for modifying density maps with brushes.
/// </summary>
public static class DensityMapModifier
{
    /// <summary>
    /// Modifies a density map using a spherical brush at a world-space hit point.
    /// </summary>
    /// <param name="densityMap">The density map to modify (3D grid of floats).</param>
    /// <param name="chunkCoordinates">The coordinates of the chunk in chunk space.</param>
    /// <param name="hitPoint">The world-space point the brush is applied at.</param>
    /// <param name="radius">The radius of the brush in world units.</param>
    /// <param name="intensity">The intensity of the brush effect (scaled by distance).</param>
    /// <param name="add">True to add density, false to subtract.</param>
    public static void ModifyMapWithSphereBrush(ref float[,,] densityMap, Vector3Int chunkCoordinates, Vector3 hitPoint, float radius, float intensity, bool add)
    {
        int width = densityMap.GetLength(0) - 1;
        int height = densityMap.GetLength(1) - 1;
        int depth = densityMap.GetLength(2) - 1;

        Vector3 chunkWorldOrigin = new Vector3(
            chunkCoordinates.x * width,
            chunkCoordinates.y * height,
            chunkCoordinates.z * depth);

        for (int x = 0; x <= width; x++)
        {
            for (int y = 0; y <= height; y++)
            {
                for (int z = 0; z <= depth; z++)
                {
                    Vector3 voxelWorldPos = chunkWorldOrigin + new Vector3(x, y, z);
                    float dist = Vector3.Distance(voxelWorldPos, hitPoint);
                    if (dist > radius) continue;

                    float falloff = 1 - (dist / radius);
                    float mod = intensity * falloff;

                    if (add)
                        densityMap[x, y, z] += mod;
                    else
                        densityMap[x, y, z] -= mod;

                    densityMap[x, y, z] = Mathf.Clamp(densityMap[x, y, z], 0f, 1f);
                }
            }
        }
    }
}