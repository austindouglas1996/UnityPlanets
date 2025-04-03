using System.Threading;
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

    public Task<ChunkData> GenerateNewChunk(Vector3Int coordinates, IChunkConfiguration config, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();

            var gen = new SphereDensityMapGenerator(planet.Center, planet.PlanetRadius, config.MapOptions);
            float[,,] map = gen.Generate(config.ChunkSize, coordinates);
            MeshData data = gen.GenerateMeshData(map, Vector3.zero);

            return new ChunkData(map, data);
        }, token);
    }

    public Task UpdateChunkData(ChunkData data, IChunkConfiguration config, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();

            var gen = new SphereDensityMapGenerator(planet.Center, planet.PlanetRadius, config.MapOptions);
            data.MeshData = gen.GenerateMeshData(data.DensityMap, Vector3.zero);
        }, token);
    }

    public Mesh GenerateMesh(ChunkData chunk, IChunkConfiguration config)
    {
        var gen = new SphereDensityMapGenerator(planet.Center, planet.PlanetRadius, config.MapOptions);
        return gen.GenerateMesh(chunk.MeshData);
    }
}