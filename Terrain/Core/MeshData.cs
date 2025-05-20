using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MeshData
{
    public MeshData(int lodIndex, List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        LODIndex = lodIndex;
        Vertices = verts;
        Triangles = tris;
        UVs = uvs;
    }

    public int LODIndex = -1;
    public List<Vector3> Vertices = new();
    public List<int> Triangles = new();
    public List<Vector3> Normals = new();
    public List<Vector2> UVs = new();

    public Color[] VerticeColors;

    public bool IsEmpty => Vertices.Count == 0 || Triangles.Count == 0;
}