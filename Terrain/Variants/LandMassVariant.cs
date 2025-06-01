using System;
using UnityEngine;

public class LandMassVariant : VariantBase<LandMassChunkConfiguration>
{
    protected override IChunkColorizer CreateColorizer() => new LandMassChunkColorizer(ChunkConfiguration);
    protected override IChunkGenerator CreateGenerator() => new LandMassChunkGenerator(this);
    protected override IChunkLayout CreateLayout() => new LandMassChunkLayout(ChunkConfiguration);
    protected override IChunkControllerFactory CreateFactory() => new LandMassChunkControllerFactory(200, this, chunkManager.transform);
}

public class LandMassChunkColorizer : GenericChunkColorizer
{
    public LandMassChunkColorizer(IChunkConfiguration configuration) : base(configuration)
    {
    }
}

[Serializable]
public class LandMassChunkConfiguration : GenericChunkConfiguration
{
}

public class LandMassChunkControllerFactory : GenericChunkControllerFactory
{
    public LandMassChunkControllerFactory(int preloadChunks, IChunkServices services, Transform parent)
        : base(preloadChunks, services, parent)
    {

    }
}

public class LandMassChunkGenerator : GenericChunkGenerator
{
    private IChunkColorizer colorizer;
    public LandMassChunkGenerator(IChunkServices services)
        : base(services.Configuration)
    {
        this.colorizer = services.Colorizer;
    }

    protected override BaseMarchingCubeGenerator Generator
    {
        get
        {
            if (this.generator == null)
                this.generator = new HeightDensityMapGenerator(colorizer, Configuration.DensityOptions);
            return this.generator;
        }
    }
    private HeightDensityMapGenerator generator;
}

public class LandMassChunkLayout : GenericChunkLayout
{
    public LandMassChunkLayout(LandMassChunkConfiguration configuration)
        : base(configuration)
    {
    }
}