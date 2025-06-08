using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// A container for storing raw mesh data before it's built into a Unity Mesh.
/// </summary>
public class MeshData
{
    /// <summary>
    /// Create a new MeshData container with verts, tris, and UVs.
    /// </summary>
    public MeshData(List<Vector3> verts, List<int> tris, List<Vector3> normals, List<Vector2> uvs)
    {
        Vertices = verts;
        Triangles = tris;
        Normals = normals;
        UVs = uvs;
    }

    /// <summary>
    /// Used to return an empty instance. May be redundant if not cached.
    /// </summary>
    public MeshData Empty => new MeshData(null, null, null, null);

    // Final mesh components to be converted into a Unity mesh
    public List<Vector3> Vertices = new();
    public List<int> Triangles = new();
    public List<Vector3> Normals = new();
    public List<Vector2> UVs = new();

    // Vertex colors (same length as verts)
    public Color32[] Colors;

    /// <summary>
    /// Whether this MeshData is missing any geometry.
    /// </summary>
    public bool IsEmpty => Vertices.Count == 0 || Triangles.Count == 0;

    /// <summary>
    /// Whether this MeshData has at least one triangle.
    /// </summary>
    public bool IsRenderable => Triangles.Count >= 3;

    /// <summary>
    /// Builds the final Unity mesh from this data set.
    /// </summary>
    public Mesh GenerateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = Vertices.ToArray();
        mesh.triangles = Triangles.ToArray();
        mesh.normals = Normals.ToArray();
        mesh.uv = UVs.ToArray();
        mesh.colors32 = Colors;

        // Might be unnecessary now days.
        mesh.RecalculateBounds();

        return mesh;
    }
}
