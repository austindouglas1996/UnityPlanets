using static MeshHelper;
using System.Collections.Generic;
using UnityEngine;

public static class MeshHelper
{
    public struct TrianglePOS
    {
        public Vector3 Position;
        public Vector3 Normal;
    }

    /// <summary>
    /// Create a list of random positions within triangles.
    /// </summary>
    /// <param name="transform">Transform to convert local vertices to world space.</param>
    /// <param name="multiply">Number of points per triangle.</param>
    /// <param name="alignY">Align points vertically using raycasting.</param>
    /// <param name="layerName">Physics layer for alignment checks.</param>
    /// <returns>List of random positions.</returns>
    public static List<TrianglePOS> GetRandomPositionsInTriangles(Mesh mesh, Transform transform, int multiply = 1, bool alignY = true, string layerName = "Default")
    {
        List<TrianglePOS> positions = new List<TrianglePOS>();

        if (multiply <= 0)
            multiply = 1;

        LayerMask layerMask = LayerMask.GetMask(layerName);

        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            Vector3 localA = mesh.vertices[mesh.triangles[i]];
            Vector3 localB = mesh.vertices[mesh.triangles[i + 1]];
            Vector3 localC = mesh.vertices[mesh.triangles[i + 2]];

            Vector3 vertexA = transform.TransformPoint(localA);
            Vector3 vertexB = transform.TransformPoint(localB);
            Vector3 vertexC = transform.TransformPoint(localC);

            List<Vector3> localPositions = new List<Vector3>();

            for (int x = 0; x < multiply; x++)
            {
                Vector3 triangleNormal = Vector3.Cross(vertexB - vertexA, vertexC - vertexA).normalized;
                Vector3 position = RandomPointInTriangle(vertexA, vertexB, vertexC) + triangleNormal * 0.01f;

                if (alignY && Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, layerMask))
                {
                    position.y = hit.point.y;
                }

                positions.Add(new TrianglePOS() { Position = position, Normal = triangleNormal });
                localPositions.Add(position);
            }
        }

        return positions;
    }

    private static Vector3 RandomPointInTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        float r1 = Mathf.Sqrt(Random.value);
        float r2 = Random.value;
        return (1 - r1) * a + (r1 * (1 - r2)) * b + (r1 * r2) * c;
    }
}