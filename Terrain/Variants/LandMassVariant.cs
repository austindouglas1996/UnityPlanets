using System;
using UnityEngine;

public class LandMassVariant : VariantBase<LandMassChunkConfiguration>
{
    protected override void Awake()
    {
        base.Awake();
    }

    protected override IChunkGenerator CreateGenerator() => new LandMassChunkGenerator(this);
    protected override IChunkLayout CreateLayout() => new LandMassChunkLayout(ChunkConfiguration);
    protected override IChunkControllerFactory CreateFactory() => new LandMassChunkControllerFactory(200, this, chunkManager.transform);
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
    private IChunkConfiguration configuration;
    public LandMassChunkGenerator(IChunkServices services)
        : base(services.Configuration)
    {
        this.configuration = services.Configuration;
    }

    public override BaseMarchingCubeGenerator Generator
    {
        get
        {
            if (this.generator == null)
                this.generator = new HeightDensityMapGenerator(configuration, Configuration.DensityOptions);
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