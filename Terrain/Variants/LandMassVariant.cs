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
}

[Serializable]
public class LandMassChunkConfiguration : BaseChunkConfiguration
{
}

public class LandMassChunkGenerator : BaseChunkGenerator
{
    private IChunkConfiguration configuration;
    private IChunkServices services;

    public LandMassChunkGenerator(IChunkServices services)
        : base(services.Configuration)
    {
        this.services = services;
        this.configuration = services.Configuration;
    }

    public override MarchingCubesGPUDispatcher Generator
    {
        get
        {
            if (this.generator == null)
                this.generator = new MarchingCubesGPUDispatcher(services, configuration, Configuration.DensityOptions);
            return this.generator;
        }
    }
    private MarchingCubesGPUDispatcher generator;
}

public class LandMassChunkLayout : BaseChunkLayout
{
    public LandMassChunkLayout(LandMassChunkConfiguration configuration)
        : base(configuration)
    {
    }
}