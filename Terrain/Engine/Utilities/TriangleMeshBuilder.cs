using System.Collections.Generic;
using UnityEngine;

public static class TriangleMeshBuilder
{
    public static Mesh BuildMesh(IReadOnlyList<ChunkTriangleData> tris)
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

        mesh.SetVertices(verts);
        mesh.SetTriangles(indices, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    public static GameObject CreateGOMeshWithCollider(Mesh mesh)
    {
        GameObject go = new GameObject();

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        /* We may need this later.
        var renderer = go.AddComponent<MeshRenderer>();
        var debugMat = new Material(Shader.Find("Standard"));
        debugMat.color = UnityEngine.Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f, 0.5f, 0.5f);
        renderer.sharedMaterial = debugMat;
        */

        MeshCollider collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning |
                              MeshColliderCookingOptions.WeldColocatedVertices;

        return go;
    }
}