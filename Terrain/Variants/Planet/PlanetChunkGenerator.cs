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

    public override BaseMarchingCubeGenerator CreateMapGenerator()
    {
        return new SphereDensityMapGenerator(colorizer, planet.Center, planet.PlanetRadius, Configuration.DensityOptions);
    }
}