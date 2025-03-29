using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.Mesh;

public class MeshData
{
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;

    int triangleIndex;

    public MeshData(int meshWidth, int meshHeight)
    {
        vertices = new Vector3[meshWidth * meshHeight];
        uvs = new Vector2[meshWidth * meshHeight];

        int numQuads = (meshWidth - 1) * (meshHeight - 1);
        triangles = new int[numQuads * 6]; // Each quad = 6 indices

    }

    public void AddTriangle(int a, int b, int c)
    {
        if (triangleIndex < triangles.Length - 2)
        {
            triangles[triangleIndex] = a;
            triangles[triangleIndex + 1] = b;
            triangles[triangleIndex + 2] = c;
            triangleIndex += 3;
        }
    }

    public Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        return mesh;
    }
}