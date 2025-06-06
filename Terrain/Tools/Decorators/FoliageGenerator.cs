using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;

public class FoliageGenerator
{
    public class TrianglePOS
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Color Color;
    }

    private MeshBatchDrawer foliageDrawer;

    public float maxGrassHeight = 2.3f;
    public float grassDensity = 10f;

    private System.Random rand = new System.Random();

    public void ApplyMap(ChunkRenderData data, Matrix4x4 matrix, CancellationToken token = default)
    {
        List<TrianglePOS> positions = new List<TrianglePOS>();

        foliageDrawer = MeshBatchDrawer.Instance;

        LayerMask layerMask = LayerMask.GetMask("Default");

        positions = GetRandomPositionsInTriangles(data.Data, matrix, 1);

        foreach (TrianglePOS pos in positions)
        {
            if (Physics.Raycast(pos.Position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, layerMask))
            {
                pos.Position = new Vector3(pos.Position.x, hit.point.y, pos.Position.z);
            }
        }

        ProcessGrassPositions(data, positions, data.LOD);
    }

    private void ProcessGrassPositions(ChunkRenderData data, List<TrianglePOS> pos, int chunkLod)
    {
        try
        {
            foreach (TrianglePOS tria in pos)
            {
                float rockChance = 0.001f;
                float treeChance = 0.002f;

                float averageHeight = tria.Position.y;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, tria.Normal) * Quaternion.Euler(0, Random.Range(0, 360), 0);

                if (Random.value < rockChance)
                {
                    Vector3 rockScale = Vector3.one * Random.Range(0.2f, 11f);
                    GameObject rock = GenericStore.Instance.GetOneRandom("Rocks");
                    foliageDrawer.Add(data, rock, tria.Position, rotation, rockScale, tria.Color);
                }

                Vector3 scale = Vector3.one * Random.Range(0.7f, 1.4f);
                var grass = GenericStore.Instance.GetOneRandom("Grass");
                foliageDrawer.Add(data, grass, tria.Position, rotation, scale, tria.Color);

                if (Random.value < rockChance) // Flower spawn, only if rock didn't spawn
                {
                    Vector3 flowerScale = Vector3.one * Random.Range(1.3f, 2.5f);
                    GameObject flower = GenericStore.Instance.GetOneRandom("Flowers");
                    foliageDrawer.Add(data, flower, tria.Position, rotation, flowerScale, tria.Color);
                }

                if (Random.value < treeChance)
                {
                    Vector3 treeScale = Vector3.one * Random.Range(0.6f, 2f);
                    GameObject tree = GenericStore.Instance.GetOneRandom("Trees");
                    foliageDrawer.Add(data, tree, tria.Position, Quaternion.Euler(0, 0, 0), treeScale, tria.Color);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    private List<TrianglePOS> GetRandomPositionsInTriangles(ChunkData data, Matrix4x4 matrix, int multiply = 1, bool alignY = true)
    {
        List<TrianglePOS> positions = new List<TrianglePOS>();

        try
        {
            if (multiply <= 0)
                multiply = 1;

            int sizeX = data.DensityMap.SizeX;
            int sizeY = data.DensityMap.SizeY;
            int sizeZ = data.DensityMap.SizeZ;

            Vector3 chunkOrigin = matrix.MultiplyPoint3x4(Vector3.zero);

            for (int i = 0; i < data.MeshData.Triangles.Count; i += 3)
            {
                Vector3 localA = data.MeshData.Vertices[data.MeshData.Triangles[i]];
                Vector3 localB = data.MeshData.Vertices[data.MeshData.Triangles[i + 1]];
                Vector3 localC = data.MeshData.Vertices[data.MeshData.Triangles[i + 2]];

                Vector3 vertexA = matrix.MultiplyPoint3x4(localA);
                Vector3 vertexB = matrix.MultiplyPoint3x4(localB);
                Vector3 vertexC = matrix.MultiplyPoint3x4(localC);

                List<Vector3> localPositions = new List<Vector3>();

                for (int x = 0; x < multiply; x++)
                {
                    try
                    {
                        Vector3 triangleNormal = Vector3.Cross(vertexB - vertexA, vertexC - vertexA).normalized;
                        Vector3 position = RandomPointInTriangle(vertexA, vertexB, vertexC) + triangleNormal * 0.01f;

                        if (position.y < 225)
                            continue;

                        // Compute local voxel-space position
                        float voxelX = (position.x - chunkOrigin.x) / 16;
                        float voxelY = (position.y - chunkOrigin.y) / 16;
                        float voxelZ = (position.z - chunkOrigin.z) / 16;

                        int localX = Mathf.FloorToInt(voxelX);
                        int localY = Mathf.FloorToInt(voxelY);
                        int localZ = Mathf.FloorToInt(voxelZ);

                        // Respect foliage mask
                        //if (data.FoliageMask[localX, localY, localZ] <= 0f)
                        //continue;

                        Color A = data.MeshData.Colors[i];
                        Color B = data.MeshData.Colors[i + 1];
                        Color C = data.MeshData.Colors[i + 2];
                        Color D = (A + B + C) / 3f;

                        positions.Add(new TrianglePOS() { Position = position, Normal = triangleNormal, Color = D });
                        localPositions.Add(position);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }

        return positions;
    }

    /// <summary>
    /// Return a random position in the triangle.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="c"></param>
    /// <returns></returns>
    private Vector3 RandomPointInTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        float r1 = Mathf.Sqrt((float)rand.NextDouble());
        float r2 = (float)rand.NextDouble();
        return (1 - r1) * a + (r1 * (1 - r2)) * b + (r1 * r2) * c;
    }
}