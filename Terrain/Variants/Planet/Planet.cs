using UnityEngine;

public class Planet : ChunkVariantBase<PlanetChunkConfiguration>
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
