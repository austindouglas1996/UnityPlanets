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