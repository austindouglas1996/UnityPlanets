using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quick helpers for turning triangle data into a Unity mesh or GameObject.
/// </summary>
public static class TriangleMeshBuilder
{
    /// <summary>
    /// This should never be a problem, but just in case.
    /// </summary>
    private const int MaxPoolSize = 32;

    /// <summary>
    /// A static list to help with GC issues.
    /// </summary>
    private static readonly Queue<GameObject> colliderPool = new();

    /// <summary>
    /// Build a Unity <see cref="Mesh"/> from a list of triangle data.
    /// </summary>
    public static Mesh BuildMesh(IReadOnlyList<TriangleDataGPU> tris)
    {
        if (tris == null || tris.Count == 0) return null;

        int vCount = tris.Count * 3;
        var verts = new Vector3[vCount];
        var indices = new int[vCount];

        for (int i = 0, v = 0; i < tris.Count; i++)
        {
            var t = tris[i];

            Vector3 a = t.a, b = t.b, c = t.c;

            verts[v + 0] = a;
            verts[v + 1] = b;
            verts[v + 2] = c;

            indices[v + 0] = v + 0;
            indices[v + 1] = v + 1;
            indices[v + 2] = v + 2;

            v += 3;
        }

        var mesh = new Mesh
        {
            indexFormat = (vCount > 65535)
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16
        };

        mesh.SetVertices(verts,0, vCount);
        mesh.SetTriangles(indices, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Create or reuse an existing game object made for the purpose of a collision object. The
    /// object will contain nothing but a mesh filter an a collider.
    /// </summary>
    /// <param name="mesh"></param>
    /// <param name="existingGo">Using an existing GO will go around an available pool and reuse the object directly.</param>
    /// <returns></returns>
    public static GameObject CreateOrReuseCollider(Mesh mesh, GameObject existingGo)
    {
        if (mesh == null) return null;

        GameObject go = existingGo;

        if (go == null && colliderPool.Count > 0)
        {
            go = colliderPool.Dequeue();
            go.SetActive(true);
        }
        else if (go == null)
        {
            go = new GameObject("ChunkCollider");
            go.AddComponent<MeshFilter>();

            var coll = go.AddComponent<MeshCollider>(); 
            coll.cookingOptions =
                MeshColliderCookingOptions.EnableMeshCleaning |
                MeshColliderCookingOptions.WeldColocatedVertices;
        }

        var filter = go.GetComponent<MeshFilter>();
        var collider = go.GetComponent<MeshCollider>();

        filter.sharedMesh = mesh;
        collider.sharedMesh = mesh;

        return go;
    }

    /// <summary>
    /// Give up an existing GO to be used for other objects.
    /// </summary>
    /// <param name="go"></param>
    public static void RecycleCollider(GameObject go)
    {
        if (go == null) return;
        if (colliderPool.Count > MaxPoolSize)
        {
            UnityEngine.Object.Destroy(go);
            return;
        }


        go.SetActive(false);
        colliderPool.Enqueue(go);
    }
}