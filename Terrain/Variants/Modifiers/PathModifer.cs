using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static UnityEngine.Mesh;
using static UnityEngine.Rendering.STP;

public class PathEntry
{
    public Vector2 WorldStart;
    public Vector2 WorldEnd;

    public float InnerRadius = 1f;
    public float OuterRadius = 4f;

    public Color Color = new Color(0.36f, 0.25f, 0.15f);

    public Vector2 Dir => (WorldEnd - WorldStart).normalized;
    public float Length => Vector2.Distance(WorldStart, WorldEnd);
}

public class PathModifer : IModifyDensity, IModifyColor, IModifyFoliageMask
{
    private List<PathEntry> paths = new();

    public PathModifer()
    {
        // Central dirt path across the middle
        paths.Add(new PathEntry
        {
            WorldStart = new Vector2(-100f, -100f),
            WorldEnd = new Vector2(100f, 100f),
            InnerRadius = 2f,
            OuterRadius = 6f,
            Color = new Color(0.36f, 0.25f, 0.15f) // Dirt
        });

        // Curved rural trail (manual bend by segments)
        paths.Add(new PathEntry
        {
            WorldStart = new Vector2(-90f, 40f),
            WorldEnd = new Vector2(-10f, 80f),
            InnerRadius = 1.5f,
            OuterRadius = 4f,
            Color = new Color(0.42f, 0.3f, 0.2f) // Dusty path
        });

        paths.Add(new PathEntry
        {
            WorldStart = new Vector2(-10f, 80f),
            WorldEnd = new Vector2(50f, 60f),
            InnerRadius = 1.5f,
            OuterRadius = 4f,
            Color = new Color(0.42f, 0.3f, 0.2f)
        });

        // Hidden forest footpath
        paths.Add(new PathEntry
        {
            WorldStart = new Vector2(60f, -40f),
            WorldEnd = new Vector2(20f, -90f),
            InnerRadius = 1f,
            OuterRadius = 3f,
            Color = new Color(0.3f, 0.2f, 0.12f) // Shady trail
        });

    }

    public void ModifyColor(ref Color[] vertexColors, MeshData meshData, Matrix4x4 localToWorld, IChunkConfiguration config)
    {
        foreach (var path in paths)
        {
            ModifyColor(path, ref vertexColors, meshData, localToWorld, config);
        }
    }

    public void ModifyDensity(ref DensityMap densityMap, Vector3Int coordinates, DensityMapOptions options)
    {
        foreach (var path in paths)
        {
            ModifyDensity(path, ref densityMap, coordinates, options);
        }
    }

    public void ModifyFoliageMask(ref float[,,] mask, Vector3Int coordinates)
    {
        foreach (var path in paths)
        {
            ModifyFoliageMask(path, ref mask, coordinates);
        }
    }

    private void ModifyColor(PathEntry path, ref Color[] vertexColors, MeshData meshData, Matrix4x4 localToWorld, IChunkConfiguration config)
    {
        for (int i = 0; i < meshData.Vertices.Count; i++)
        {
            Vector3 worldPos = localToWorld.MultiplyPoint3x4(meshData.Vertices[i]);
            Vector2 world2D = new Vector2(worldPos.x, worldPos.z);

            Vector2 toPoint = world2D - path.WorldStart;
            float projection = Vector2.Dot(toPoint, path.Dir);

            Vector2 closestPoint;
            if (projection <= 0)
                closestPoint = path.WorldStart;
            else if (projection >= path.Length)
                closestPoint = path.WorldEnd;
            else
                closestPoint = path.WorldStart + path.Dir * projection;

            float distToPath = Vector2.Distance(world2D, closestPoint);

            float t = Mathf.InverseLerp(path.OuterRadius, path.InnerRadius, distToPath);
            t = Mathf.SmoothStep(0f, 1f, t);

            if (t > 0f)
            {
                vertexColors[i] = Color.Lerp(vertexColors[i], path.Color, t);
            }
        }
    }

    private void ModifyDensity(PathEntry path, ref DensityMap densityMap, Vector3Int coordinates, DensityMapOptions options)
    {
        int sizeX = densityMap.SizeX;
        int sizeY = densityMap.SizeY;
        int sizeZ = densityMap.SizeZ;

        for (int x = 0; x < sizeX; x++)
            for (int y = 0; y < sizeY; y++)
                for (int z = 0; z < sizeZ; z++)
                {
                    float worldX = coordinates.x * (sizeX - 1) + x;
                    float worldY = coordinates.y * (sizeY - 1) + y;
                    float worldZ = coordinates.z * (sizeZ - 1) + z;

                    Vector2 world2d = new Vector2(worldX, worldZ);

                    // Path direction
                    Vector2 toPoint = world2d - path.WorldStart;
                    float projection = Vector2.Dot(toPoint, path.Dir);
                    Vector2 closestPoint = path.WorldStart;

                    // Find closest point.
                    if (projection > 0 && projection < path.Length)
                        closestPoint = path.WorldStart + path.Dir * projection;
                    else if (projection >= path.Length)
                        closestPoint = path.WorldEnd;
                    float distToPath = Vector2.Distance(world2d, closestPoint);

                    if (distToPath <= path.OuterRadius)
                    {
                        float t = Mathf.InverseLerp(path.OuterRadius, path.InnerRadius, distToPath);
                        t = Mathf.SmoothStep(0f, 1f, t);

                        densityMap[x, y, z] += t * 0.6f;
                    }
                }
    }

    private void ModifyFoliageMask(PathEntry path, ref float[,,] mask, Vector3Int coordinates)
    {
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
                    Vector2 toPoint = world2d - path.WorldStart;
                    float projection = Vector2.Dot(toPoint, path.Dir);
                    Vector2 closestPoint = path.WorldStart;

                    // Find closest point.
                    if (projection > 0 && projection < path.Length)
                        closestPoint = path.WorldStart + path.Dir * projection;
                    else if (projection >= path.Length)
                        closestPoint = path.WorldEnd;
                    float distToPath = Vector2.Distance(world2d, closestPoint);

                    if (distToPath <= path.InnerRadius)
                    {
                        mask[x, y, z] = 0f;
                    }
                }
    }
}
