using System.Collections.Generic;
using UnityEngine;

public class MeshData
{
    public List<Vector3> Vertices = new List<Vector3>();
    public List<int> Triangles = new List<int>();
    public List<Vector2> UVs = new List<Vector2>();

    public MeshData(List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        Vertices = verts;
        Triangles = tris;
        UVs = uvs;
    }
}