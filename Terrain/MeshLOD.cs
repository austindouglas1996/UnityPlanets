using System.Collections.Generic;
using UnityEngine;

public class MeshLOD
{
    public MeshLOD(int index, Mesh mesh, Material material)
    {
        this.LODIndex = index;
        this.Mesh = mesh;

        this.Mat = material;
        this.Mat.enableInstancing = true;
    }

    public int LODIndex { get; set; }
    public Mesh Mesh { get; set; }
    public Material Mat { get; set; }

    public static List<MeshLOD> Extract(GameObject go)
    {
        LODGroup group = go.GetComponent<LODGroup>();
        if (group == null)
            throw new System.ArgumentNullException("GO does not contain a LODGroup to extract mesh.");

        List<MeshLOD> results = new List<MeshLOD>();

        LOD[] lods = group.GetLODs();
        for (int i = 0; i < lods.Length; i++)
        {
            if (lods[i].renderers.Length > 0)
            {
                MeshFilter meshFilter = lods[i].renderers[0].GetComponent<MeshFilter>();
                if (meshFilter == null)
                    throw new System.ArgumentNullException("Failed to retrieve mesh during LODGroup extraction.");

                results.Add(new MeshLOD(i, meshFilter.sharedMesh, lods[i].renderers[0].sharedMaterial));
            }
        }

        return results;
    }
}