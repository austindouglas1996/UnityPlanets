using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting.Antlr3.Runtime;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.InputSystem;

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

            var gen = CreateGenerator(config);
            float[,,] map = gen.Generate(config.ChunkSize, coordinates);
            MeshData data = gen.GenerateMeshData(map, Vector3.zero);

            return new ChunkData(map, data);
        }, token);
    }

    public Task ModifyChunkData(ChunkData data, IChunkConfiguration config, TerrainBrush brush, Vector3Int chunkPos, bool addingOrSubtracting, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();

            CreateGenerator(config).ModifyMapWithBrush(brush, ref data.DensityMap, chunkPos, brush.WorldHitPoint, addingOrSubtracting);
        }, token);
    }

    public Task UpdateChunkData(ChunkData data, IChunkConfiguration config, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            data.MeshData = CreateGenerator(config).GenerateMeshData(data.DensityMap, Vector3.zero);
        }, token);
    }

    public Mesh GenerateMesh(ChunkData chunk, IChunkConfiguration config)
    {
        return CreateGenerator(config).GenerateMesh(chunk.MeshData);
    }

    private SphereDensityMapGenerator CreateGenerator(IChunkConfiguration config)
    {
        return new SphereDensityMapGenerator(planet.Center, planet.PlanetRadius, config.MapOptions);
    }
}