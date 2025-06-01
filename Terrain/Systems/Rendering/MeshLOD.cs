using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// Represents a single LOD entry for a mesh and material pair.
/// </summary>
public class MeshLOD
{
    /// <summary>
    /// Creates a new mesh LOD entry.
    /// </summary>
    /// <param name="index">The LOD level (0 = highest detail).</param>
    /// <param name="mesh">The mesh to use at this LOD.</param>
    /// <param name="material">The material used with the mesh.</param>
    public MeshLOD(int index, Mesh mesh, Material material)
    {
        this.LODIndex = index;
        this.Mesh = mesh;
        this.Mat = material;

        // Ensure GPU instancing is enabled for this material (better performance with large instances).
        if (this.Mat != null)
            this.Mat.enableInstancing = true;
    }

    /// <summary>
    /// LOD level index (0 = highest detail).
    /// </summary>
    public int LODIndex { get; set; }

    /// <summary>
    /// The mesh associated with this LOD level.
    /// </summary>
    public Mesh Mesh { get; set; }

    /// <summary>
    /// The material used to render the mesh.
    /// </summary>
    public Material Mat { get; set; }

    /// <summary>
    /// Extracts LOD meshes and materials from a GameObject.
    /// Supports both LODGroup and single mesh setups.
    /// </summary>
    /// <param name="go">GameObject to extract from.</param>
    /// <returns>A list of MeshLOD entries, one for each available LOD.</returns>
    public static List<MeshLOD> Extract(GameObject go)
    {
        List<MeshLOD> results = new List<MeshLOD>();

        LODGroup group = go.GetComponent<LODGroup>();

        // If there's no LODGroup, fallback to default MeshRenderer + MeshFilter.
        if (group == null)
        {
            MeshFilter filter = go.GetComponent<MeshFilter>();
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();

            results.Add(new MeshLOD(1, filter.sharedMesh, renderer.sharedMaterial));
            return results;
        }

        // Extract LODs from LODGroup.
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
