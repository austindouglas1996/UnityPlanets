using UnityEngine;

public class PlanetChunkControllerFactory : IChunkControllerFactory
{
    private Planet planet;

    public PlanetChunkControllerFactory(Planet planet)
    {
        this.planet = planet;
    }

    public ChunkController CreateChunkController(Vector3Int coordinates, IChunkConfiguration config, Transform parent)
    {
        Vector3 pos = new Vector3(
            coordinates.x * config.ChunkSize,
            coordinates.y * config.ChunkSize,
            coordinates.z * config.ChunkSize);

        GameObject newChunk = new GameObject("PlanetChunk");
        newChunk.transform.position = pos;
        newChunk.transform.parent = parent;

        ChunkController newController = newChunk.AddComponent<ChunkController>();
        newController.Initialize(new GenericChunkGenerator<SphereDensityMapGenerator>(), new PlanetChunkColorizer(planet), config, coordinates);

        return newController;
    }
}
