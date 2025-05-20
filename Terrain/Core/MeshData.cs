using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MeshData
{
    public MeshData(List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        Vertices = verts;
        Triangles = tris;
        UVs = uvs;
    }

    public MeshData Empty
    {
        get { return new MeshData(null, null, null); }
    }

    public List<Vector3> Vertices = new();
    public List<int> Triangles = new();
    public List<Vector3> Normals = new();
    public List<Vector2> UVs = new();

    public Color32[] Colors;

    /// <summary>
    /// Returns whether this <see cref="MeshData"/> is an empty collection.
    /// </summary>
    public bool IsEmpty => Vertices.Count == 0 || Triangles.Count == 0;

    /// <summary>
    /// Converts processed MeshData into a Unity Mesh object.
    /// </summary>
    /// <param name="data">The mesh data to convert.</param>
    /// <returns>A generated Unity Mesh.</returns>
    public Mesh GenerateMesh(DensityMap densityMap)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = Vertices.ToArray();
        mesh.triangles = Triangles.ToArray();
        mesh.normals = Normals.ToArray();
        mesh.uv = UVs.ToArray();
        mesh.colors = null;
        mesh.colors32 = Colors;

        // Hmm should we keep this?
        mesh.RecalculateBounds();

        return mesh;
    }
}