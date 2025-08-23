using System;
using UnityEngine;

public class LandMassVariant : VariantBaseMono<LandMassChunkConfiguration>
{
    protected override IChunkGenerator CreateGenerator() => new LandMassChunkGenerator(chunkRenderer, this);
    protected override IChunkLayout CreateLayout() => new LandMassChunkLayout(ChunkConfiguration);
}

[Serializable]
public class LandMassChunkConfiguration : BaseChunkConfiguration
{
}

public class LandMassChunkGenerator : BaseChunkGenerator
{
    public LandMassChunkGenerator(ChunkRendererMono renderer, IChunkServices services)
        : base(services.Configuration)
    {
        this.generator = new MarchingCubesTerrainGenerator(services, renderer.MarchingCubes);
    }

    public override ITerrainGenerator Generator
    {
        get { return generator; }
    }
    private MarchingCubesTerrainGenerator generator;
}

public class LandMassChunkLayout : BaseChunkLayout
{
    public LandMassChunkLayout(LandMassChunkConfiguration configuration)
        : base(configuration)
    {
    }
}