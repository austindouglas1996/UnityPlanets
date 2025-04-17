using System.Collections.Generic;
using UnityEngine;

public class MeshData
{
    public List<Vector3> Vertices = new();
    public List<int> Triangles = new();
    public List<Vector2> UVs = new();
    public List<Vector3> Normals = new();

    public MeshData(List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Vector3> normals)
    {
        Vertices = verts;
        Triangles = tris;
        UVs = uvs;
        Normals = normals;
    }
}