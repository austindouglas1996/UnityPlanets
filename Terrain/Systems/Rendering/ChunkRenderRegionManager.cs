using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class ChunkRenderRegionManager
{
    private List<ChunkRenderRegion> regions;
    private IChunkGenerator chunkGenerator;
    private Material material;

    public ChunkRenderRegionManager(IChunkGenerator gen, Material material)
    {
        this.chunkGenerator = gen;
        this.material = material;
        this.regions = new List<ChunkRenderRegion>();
        this.regions.Add(new ChunkRenderRegion());
    }

    public void Add(ChunkContext context)
    {
        var currentRegion = regions.Last();
        if (currentRegion.MaxCapaciity)
        {
            currentRegion = new ChunkRenderRegion();
            regions.Add(currentRegion);
        }

        currentRegion.Add(context);
    }

    public bool Remove(ChunkContext context)
    {
        foreach (var region in regions)
        {
            if (region.Contains(context))
            {
                region.Remove(context);
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        foreach (var region in regions)
        {
            region.Dispose();
        }
    }

    public void Update(CancellationToken token)
    {
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
                region.Generate(chunkGenerator, token);
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

            foreach (var item in region.Chunks.Values)
                this.Add(item);

            region.Dispose();
        }
    }

    public void Draw()
    {
        foreach (var region in regions)
        {
            region.Draw(material);
        }
    }
}
