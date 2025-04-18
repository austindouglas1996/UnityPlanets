using UnityEngine;

public class PathModifer : IModifyDensity, IModifyColor, IModifyFoliageMask
{
    private readonly Vector2 pathStart = new Vector2(100f, 97f);
    private readonly Vector2 pathEnd = new Vector2(-73f, -96f);
    private readonly float pathInnerRadius = 1f;
    private readonly float pathOuterRadius = 4f;

    private readonly Color dirtPathColor = new Color(0.36f, 0.25f, 0.15f); // rich brown dirt

    public void ModifyColor(ref Color[] vertexColors, MeshData meshData, Matrix4x4 localToWorld, IChunkConfiguration config)
    {
        Vector2 pathDir = (pathEnd - pathStart).normalized;
        float pathLength = Vector2.Distance(pathStart, pathEnd);

        for (int i = 0; i < meshData.Vertices.Count; i++)
        {
            Vector3 worldPos = localToWorld.MultiplyPoint3x4(meshData.Vertices[i]);
            Vector2 world2D = new Vector2(worldPos.x, worldPos.z);

            Vector2 toPoint = world2D - pathStart;
            float projection = Vector2.Dot(toPoint, pathDir);

            Vector2 closestPoint;
            if (projection <= 0)
                closestPoint = pathStart;
            else if (projection >= pathLength)
                closestPoint = pathEnd;
            else
                closestPoint = pathStart + pathDir * projection;

            float distToPath = Vector2.Distance(world2D, closestPoint);

            float t = Mathf.InverseLerp(pathOuterRadius, pathInnerRadius, distToPath);
            t = Mathf.SmoothStep(0f, 1f, t);

            if (t > 0f)
            {
                vertexColors[i] = Color.Lerp(vertexColors[i], dirtPathColor, t);
            }
        }
    }

    public void ModifyDensity(ref float[,,] densityMap, Vector3Int coordinates, DensityMapOptions options)
    {
        Vector2 pathDir = (pathEnd - pathStart).normalized;
        float pathLength = Vector2.Distance(pathStart, pathEnd);

        int sizeX = densityMap.GetLength(0);
        int sizeY = densityMap.GetLength(1);
        int sizeZ = densityMap.GetLength(2);

        for (int x = 0; x < sizeX; x++)
            for (int y = 0; y < sizeY; y++)
                for (int z = 0; z < sizeZ; z++)
                {
                    float worldX = coordinates.x * (sizeX - 1) + x;
                    float worldY = coordinates.y * (sizeY - 1) + y;
                    float worldZ = coordinates.z * (sizeZ - 1) + z;

                    Vector2 world2d = new Vector2(worldX, worldZ);

                    // Path direction
                    Vector2 toPoint = world2d - pathStart;
                    float projection = Vector2.Dot(toPoint, pathDir);
                    Vector2 closestPoint = pathStart;

                    // Find closest point.
                    if (projection > 0 && projection < pathLength)
                        closestPoint = pathStart + pathDir * projection;
                    else if (projection >= pathLength)
                        closestPoint = pathEnd;
                    float distToPath = Vector2.Distance(world2d, closestPoint);

                    if (distToPath <= pathOuterRadius)
                    {
                        float t = Mathf.InverseLerp(pathOuterRadius, pathInnerRadius, distToPath);
                        t = Mathf.SmoothStep(0f, 1f, t);

                        densityMap[x, y, z] += (y - worldY) * t * 0.6f;
                    }
                }
    }

    public void ModifyFoliageMask(ref float[,,] mask, Vector3Int coordinates)
    {
        Vector2 pathDir = (pathEnd - pathStart).normalized;
        float pathLength = Vector2.Distance(pathStart, pathEnd);

        int sizeX = mask.GetLength(0);
        int sizeY = mask.GetLength(1);
        int sizeZ = mask.GetLength(2);

        for (int x = 0; x < sizeX; x++)
            for (int y = 0; y < sizeY; y++)
                for (int z = 0; z < sizeZ; z++)
                {
                    float worldX = coordinates.x * (sizeX - 1) + x;
                    float worldY = coordinates.y * (sizeY - 1) + y;
                    float worldZ = coordinates.z * (sizeZ - 1) + z;

                    Vector2 world2d = new Vector2(worldX, worldZ);

                    // Path direction
                    Vector2 toPoint = world2d - pathStart;
                    float projection = Vector2.Dot(toPoint, pathDir);
                    Vector2 closestPoint = pathStart;

                    // Find closest point.
                    if (projection > 0 && projection < pathLength)
                        closestPoint = pathStart + pathDir * projection;
                    else if (projection >= pathLength)
                        closestPoint = pathEnd;
                    float distToPath = Vector2.Distance(world2d, closestPoint);

                    if (distToPath <= pathInnerRadius)
                    {
                        mask[x, y, z] = 0f;
                    }
                }
    }
}
