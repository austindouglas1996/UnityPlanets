using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public class ChunkRenderRegion
{
    public List<ChunkKey> Chunks = new List<ChunkKey>(128);
    private GPUSet set;

    public bool IsDirty { get; private set; } = false; 
    private int delayFrames = -1;

    public bool MaxCapaciity => Chunks.Count == 128;
    public int Removed = 0;

    public void Add(ChunkKey key)
    {
        Chunks.Add(key);
        MarkDirty();
    }

    public void Remove(ChunkKey key)
    {
        Chunks.Remove(key);
        MarkDirty();

        Removed++;
    }

    public bool Contains(ChunkKey key)
    {
        return Chunks.Contains(key);
    }

    public void Dispose()
    {
        if (this.set == null)
            return;

        this.set.Dispose();
    }

    public void Generate(IChunkGenerator chunkGenerator)
    {
        if (!IsDirty) return;

        if (delayFrames > 0)
        {
            delayFrames--;
            return;
        }

        set?.Dispose();
        set = chunkGenerator.DispatchGeneration(this.Chunks);

        this.IsDirty = false;
        delayFrames = -1;
    }

    public void Draw(Material vertexMat)
    {
        if (set == null)
            return;

        vertexMat.SetBuffer("_TriangleBuffer", set.Triangle);
        vertexMat.SetPass(0);
        Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles,set.Args,0);
    }

    private void MarkDirty()
    {
        IsDirty = true;
        delayFrames = 10; // Delay regeneration by 5 frames
    }
}