using System.Collections.Generic;
using UnityEngine;

public class MeshData
{
    public List<Vector3> Vertices = new();
    public List<int> Triangles = new();
    public List<Vector2> UVs = new();

    public MeshData(List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        Vertices = verts;
        Triangles = tris;
        UVs = uvs;
    }
}