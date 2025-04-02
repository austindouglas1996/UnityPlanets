using System.Threading.Tasks;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

public class PlanetChunkGenerator : IChunkGenerator
{
    private Planet planet;

    public PlanetChunkGenerator(Planet planet)
    {
        this.planet = planet;
    }

    public async Task<ChunkData> GenerateNewChunk(Vector3Int coordinates, IChunkConfiguration config)
    {
        var gen = new SphereDensityMapGenerator(planet.Center, planet.PlanetRadius, config.MapOptions);

        float[,,] map = gen.Generate(config.ChunkSize, coordinates);
        MeshData data = gen.GenerateMeshData(map, new Vector3(0, 0, 0));

        return new ChunkData(map, data);
    }

    public async Task UpdateChunkData(ChunkData data, IChunkConfiguration config)
    {
        var gen = new SphereDensityMapGenerator(planet.Center, planet.PlanetRadius, config.MapOptions);
        data.MeshData = gen.GenerateMeshData(data.DensityMap, new Vector3(0, 0, 0));
    }

    public Mesh GenerateMesh(ChunkData chunk, IChunkConfiguration config)
    {
        var gen = new SphereDensityMapGenerator(planet.Center, planet.PlanetRadius, config.MapOptions);
        return gen.GenerateMesh(chunk.MeshData);
    }
}