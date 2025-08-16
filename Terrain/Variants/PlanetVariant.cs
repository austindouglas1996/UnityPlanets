public class PlanetVariant : VariantBase<PlanetChunkConfiguration>
{
    protected override IChunkGenerator CreateGenerator() => new PlanetChunkGenerator(this);
    protected override IChunkLayout CreateLayout() => new PlanetChunkLayout(ChunkConfiguration);
}

public class PlanetChunkConfiguration : BaseChunkConfiguration
{
}

public class PlanetChunkLayout : BaseChunkLayout
{
    public PlanetChunkLayout(PlanetChunkConfiguration config)
       : base(config) { }
}

public class PlanetChunkGenerator : BaseChunkGenerator
{
    public PlanetChunkGenerator(IChunkServices services)
        : base(services.Configuration) 
    {
        this.generator = new MarchingCubesTerrainGenerator
            (services, services.ChunkManager.GenerateDensity, services.ChunkManager.MarchingCubes);
    }

    public override ITerrainGenerator Generator => generator;

    private MarchingCubesTerrainGenerator generator;
}