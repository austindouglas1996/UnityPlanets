using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class ChunkRenderRegionManager
{
    private ChunkRenderRegion lod0;
    private List<ChunkRenderRegion> regions;
    private IChunkGenerator chunkGenerator;
    private Material material;

    public ChunkRenderRegionManager(IChunkGenerator gen, Material material)
    {
        this.chunkGenerator = gen;
        this.material = material;
        this.regions = new List<ChunkRenderRegion>();
        this.regions.Add(new ChunkRenderRegion());

        lod0 = new ChunkRenderRegion();
        lod0.isLod0 = true;
    }

    public void Add(ChunkKey key)
    {
        if (key.LODIndex == 0)
        {
            lod0.Add(key);
            return;
        }

        var currentRegion = regions.Last();
        if (currentRegion.MaxCapaciity)
        {
            currentRegion = new ChunkRenderRegion();
            regions.Add(currentRegion);
        }

        currentRegion.Add(key);
    }

    public bool Remove(ChunkKey key)
    {
        if (key.LODIndex == 0)
        {
            lod0.Remove(key);
            return true;
        }

        foreach (var region in regions)
        {
            if (region.Contains(key))
            {
                region.Remove(key);
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        lod0?.Dispose();

        foreach (var region in regions)
        {
            region.Dispose();
        }

        chunkGenerator.Dispose();
    }

    public void Update()
    {
        if (lod0.IsDirty)
        {
            lod0.Generate(chunkGenerator);
        }


        List<ChunkRenderRegion> toDelete = new List<ChunkRenderRegion>();

        int max3 = 12;

        foreach (var region in regions)
        {
            if (region.Removed > 100)
            {
                toDelete.Add(region);
                continue;
            }

            if (region.IsDirty)
            {
                region.Generate(chunkGenerator);
                max3--;
            }

            if (max3 == 0)
            {
                break;
            }
        }

        foreach (var region in toDelete)
        {
            regions.Remove(region);

            foreach (var item in region.Chunks)
                this.Add(item);

            region.Dispose();
        }
    }

    public void Draw()
    {
        lod0.Draw(material);

        foreach (var region in regions)
        {
            region.Draw(material);
        }
    }
}
