using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class FoliageGenerator : MonoBehaviour
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

    private GenericStore Store;

    private System.Random rand;

    private void Awake()
    {
        this.rand = new System.Random();
        this.Store = GenericStore.Instance;
    }

    private void Update()
    {
        if (foliageDrawer != null)
            foliageDrawer.Update();
    }

    public async Task ApplyMap(ChunkData data, CancellationToken token = default)
    {
        List<TrianglePOS> positions = new List<TrianglePOS>();

        foliageDrawer = null;
        foliageDrawer = new MeshBatchDrawer(Camera.main);

        Matrix4x4 matrix = this.transform.localToWorldMatrix;

        LayerMask layerMask = LayerMask.GetMask("Default");

        await Task.Run(() =>
        {
            positions = GetRandomPositionsInTriangles(data, matrix);

        }, token);

        foreach (TrianglePOS pos in positions)
        {
            if (Physics.Raycast(pos.Position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, layerMask))
            {
                pos.Position = new Vector3(pos.Position.x, hit.point.y, pos.Position.z);
            }
        }

        ProcessGrassPositions(positions);
    }

    private void ProcessGrassPositions(List<TrianglePOS> pos)
    {
        foreach (TrianglePOS tria in pos)
        {
            float rockChance = 0.001f;
            float treeChance = 0.005f;

            float averageHeight = tria.Position.y;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, tria.Normal) * Quaternion.Euler(0, Random.Range(0, 360), 0);

            if (Random.value < rockChance)
            {
                Vector3 rockScale = Vector3.one * Random.Range(0.2f, 11f);
                foliageDrawer.Add(this.Store.GetOneRandom("Rocks"), tria.Position, rotation, rockScale, tria.Color);
            }

            Vector3 scale = Vector3.one * Random.Range(0.7f, 1.4f);
            foliageDrawer.Add(this.Store.GetOneRandom("Grass"), tria.Position, rotation, scale, tria.Color);

            if (Random.value < rockChance) // Flower spawn, only if rock didn't spawn
            {
                Vector3 flowerScale = Vector3.one * Random.Range(1.3f, 2.5f);
                foliageDrawer.Add(this.Store.GetOneRandom("Flowers"), tria.Position, rotation, flowerScale, tria.Color);
            }

            if (Random.value < treeChance)
            {
                //foliageDrawer.Add(this.Store.GetOneRandom("Trees"), tria.Position, Quaternion.Euler(0,0,0), scale, tria.Color);
            }
        }
    }

    private List<TrianglePOS> GetRandomPositionsInTriangles(ChunkData data, Matrix4x4 matrix, int multiply = 1, bool alignY = true)
    {
        List<TrianglePOS> positions = new List<TrianglePOS>();

        if (multiply <= 0)
            multiply = 1;

        int sizeX = data.FoliageMask.GetLength(0);
        int sizeY = data.FoliageMask.GetLength(1);
        int sizeZ = data.FoliageMask.GetLength(2);

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
                Vector3 triangleNormal = Vector3.Cross(vertexB - vertexA, vertexC - vertexA).normalized;
                Vector3 position = RandomPointInTriangle(vertexA, vertexB, vertexC) + triangleNormal * 0.01f;

                // Convert world position to local chunk voxel indices
                int localX = Mathf.RoundToInt(position.x - chunkOrigin.x);
                int localY = Mathf.RoundToInt(position.y - chunkOrigin.y);
                int localZ = Mathf.RoundToInt(position.z - chunkOrigin.z);

                // Bounds check
                if (localX < 0 || localY < 0 || localZ < 0 ||
                    localX >= sizeX || localY >= sizeY || localZ >= sizeZ)
                    continue;

                // Respect foliage mask
                if (data.FoliageMask[localX, localY, localZ] <= 0f)
                    continue;

                Color A = data.VerticeColors[i];
                Color B = data.VerticeColors[i + 1];
                Color C = data.VerticeColors[i + 2]; 
                Color D = (A + B + C) / 3f;

                positions.Add(new TrianglePOS() { Position = position, Normal = triangleNormal, Color = D });
                localPositions.Add(position);
            }
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