using System;
using UnityEngine;

/// <summary>
/// Thin “lane switch” over two <see cref="ChunkRenderBucketCollection"/>s:
/// one for LOD0, one for everything else. I hand it <see cref="ChunkKey"/>s and
/// it routes them to the right pool; I call <see cref="Update"/> / <see cref="Draw(Material)"/> once.
/// No Unity-specific bits here except the draw material.
/// </summary>
/// <remarks>
/// - Not thread-safe; call from the main Unity thread.
/// - LOD0 physics/collider hookups should live in your MonoBehaviour; this just routes keys.
/// </remarks>
public class ChunkRenderLayers : IDisposable
{
    private ChunkRenderBucketCollection lod0;
    private ChunkRenderBucketCollection main;

    /// <summary>
    /// The material shader used for generation.
    /// </summary>
    private Material ChunkMaterial;

    /// <summary>
    /// Build two lanes with their own capacities/thresholds.
    /// </summary>
    /// <param name="chunkGenerator">Shared generator used by both lanes.</param>
    /// <param name="mainCap">Max items per non-LOD0 bucket (default: 128).</param>
    /// <param name="mainThres">Removals before a non-LOD0 bucket regenerates.</param>
    /// <param name="lod0Cap">Max items per LOD0 bucket (keep this small: 24–32 is nice).</param>
    /// <param name="lod0Thres">Removals before an LOD0 bucket regenerates.</param>
    public ChunkRenderLayers(IChunkGenerator chunkGenerator, int mainCap, int mainThres, int lod0Cap, int lod0Thres)
    {
        lod0 = new ChunkRenderBucketCollection(chunkGenerator, lod0Cap, lod0Thres);
        main = new ChunkRenderBucketCollection(chunkGenerator, mainCap, mainThres);

        ChunkMaterial = new Material(Shader.Find("Custom/URP_CustomLitGPU"));
        ChunkMaterial.SetFloat("_Smoothness", 0f);
        ChunkMaterial.SetFloat("_UseVertexColor", 1f);
    }

    /// <summary>
    /// Route a key into the right lane by its <see cref="ChunkKey.LODIndex"/>.
    /// Collection handles dedupe; this is a simple forwarder.
    /// </summary>
    public void Add(ChunkKey key)
    {
        if (key.LODIndex == 0)
            lod0.Add(key);
        else
            main.Add(key);
    }

    /// <summary>
    /// Remove a key from whichever lane owns it.
    /// Returns true if the key existed and was removed.
    /// </summary>
    public bool Remove(ChunkKey key)
    {
        if (key.LODIndex == 0)
            return lod0.Remove(key);
        else
            return main.Remove(key);
    }

    /// <summary>
    /// Tick both lanes. Buckets handle their own debounce/regeneration internally.
    /// </summary>
    public void Update()
    {
        lod0.Update();
        main.Update();
    }

    /// <summary>
    /// Draw both lanes. Caller should have already done <c>mat.SetPass(0)</c> once this frame.
    /// Buckets will bind their buffers and issue <c>DrawProceduralIndirect</c>.
    /// </summary>
    public void Draw()
    {
        lod0.Draw(this.ChunkMaterial);
        main.Draw(this.ChunkMaterial);
    }

    /// <summary>
    /// Dispose both lanes and release GPU memory.
    /// Safe to call during teardown.
    /// </summary>
    public void Dispose()
    {
        lod0.Dispose();
        main.Dispose();
    }

    /// <summary>
    /// Tell both lanes to regenerate. 
    /// <paramref name="force"/> skips their small debounce (use sparingly).
    /// </summary>
    public void MarkAsDirty(bool force)
    {
        lod0.MarkAsDirty(force);
        main.MarkAsDirty(force);
    }
}
