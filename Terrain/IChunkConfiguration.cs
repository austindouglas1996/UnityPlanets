public interface IChunkConfiguration
{
    int ChunkSize { get; }
    ChunkType ChunkType { get; }
    DensityMapOptions MapOptions { get; }
}