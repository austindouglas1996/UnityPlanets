public class PlanetChunkGenerator : GenericChunkGenerator
{
    private Planet planet;
    private PlanetChunkColorizer colorizer;

    public PlanetChunkGenerator(Planet planet, PlanetChunkColorizer colorizer)
        : base (planet.ChunkConfiguration)
    {
        this.planet = planet;
        this.colorizer = colorizer;
    }

    protected override BaseMarchingCubeGenerator Generator
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