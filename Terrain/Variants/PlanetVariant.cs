using System;
using UnityEngine;

public class PlanetVariant : VariantBase<PlanetChunkConfiguration>
{
    [Tooltip("Controls the size of the planet radius")]
    public int PlanetRadius = 32;

    [Tooltip("You better have some very good reasons for modifying this.")]
    public Vector3 Center { get; private set; } = Vector3.zero;

    protected override IChunkColorizer CreateColorizer()
    {
        return new PlanetChunkColorizer(ChunkConfiguration);
    }

    protected override IChunkGenerator CreateGenerator()
    {
        return new PlanetChunkGenerator(this, (PlanetChunkColorizer)colorizer);
    }

    protected override IChunkLayout CreateLayout()
    {
        return new PlanetChunkLayout(this, (PlanetChunkGenerator)generator, ChunkConfiguration);
    }

    protected override IChunkControllerFactory CreateFactory()
    {
        return new PlanetChunkControllerFactory(this, 200, this, chunkManager.transform);
    }
}

public class PlanetChunkColorizer : GenericChunkColorizer
{
    public PlanetChunkColorizer(PlanetChunkConfiguration config)
        : base(config)
    {
    }
}

[Serializable]
public class PlanetChunkConfiguration : GenericChunkConfiguration
{
    [SerializeField] private int maxLoadRadius = 128;
    [SerializeField] private int surfaceBuffer = 12;

    public int MaxLoadRadius => maxLoadRadius;
    public int SurfaceBuffer => surfaceBuffer;
}

public class PlanetChunkControllerFactory : GenericChunkControllerFactory
{
    public PlanetChunkControllerFactory(PlanetVariant planet, int preloadChunks, IChunkServices services, Transform parent)
        : base(preloadChunks, services, parent)
    {
    }
}

public class PlanetChunkGenerator : GenericChunkGenerator
{
    private PlanetVariant planet;
    private PlanetChunkColorizer colorizer;

    public PlanetChunkGenerator(PlanetVariant planet, PlanetChunkColorizer colorizer)
        : base(planet.ChunkConfiguration)
    {
        this.planet = planet;
        this.colorizer = colorizer;
    }

    public override BaseMarchingCubeGenerator Generator
    {
        get
        {
            if (generator == null)
                generator = new SphereDensityMapGenerator(colorizer, planet.Center, planet.PlanetRadius, Configuration.DensityOptions);
            return generator;
        }
    }
    private SphereDensityMapGenerator generator;
}

public class PlanetChunkLayout : GenericChunkLayout
{
    public PlanetChunkLayout(PlanetVariant planet, PlanetChunkGenerator generator, PlanetChunkConfiguration configuration)
        : base(configuration)
    {
    }
}