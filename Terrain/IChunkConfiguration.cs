public enum ChunkType
{
    Sphere,
    Land
}

public interface IChunkConfiguration
{
    int ChunkSize { get; }
    ChunkType ChunkType { get; }
}

public class SphereChunkConfiguration : IChunkConfiguration
{
    public int ChunkSize => throw new System.NotImplementedException();

    public ChunkType ChunkType => ChunkType.Sphere;

    public Planet Planet { get; }
}