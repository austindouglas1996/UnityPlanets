using UnityEngine;

public class LandMassChunkControllerFactory : IChunkControllerFactory
{
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
        newController.Initialize(new GenericChunkGenerator<HeightDensityMapGenerator>(), new LandMassChunkColorizer(), config, coordinates);

        return newController;
    }
}
