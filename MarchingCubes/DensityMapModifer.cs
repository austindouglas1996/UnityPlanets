using UnityEngine;

public static class DensityMapModifier
{
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