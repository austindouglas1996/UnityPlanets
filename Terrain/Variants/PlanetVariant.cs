public class PlanetVariant : VariantBaseMono<PlanetChunkConfiguration>
{
    protected override IChunkGenerator CreateGenerator() => new PlanetChunkGenerator(chunkRenderer, this);
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
    public PlanetChunkGenerator(ChunkRendererMono renderer, IChunkServices services)
        : base(services.Configuration) 
    {
        this.generator = new MarchingCubesTerrainGenerator
            (services, renderer.MarchingCubes);
    }

    public override ITerrainGenerator Generator => generator;

    private MarchingCubesTerrainGenerator generator;
}