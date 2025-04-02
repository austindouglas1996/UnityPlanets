public class SphereChunkConfiguration : IChunkConfiguration
{
    public int ChunkSize { get; set; } = 32;
    public int MaxLoadRadius { get; set; } = 128;
    public ChunkType ChunkType => ChunkType.Sphere;

    public Planet Planet { get; }
}