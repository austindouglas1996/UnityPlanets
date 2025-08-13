using UnityEngine;
using static UnityEngine.Rendering.STP;

public interface IChunkServices
{
    public IChunkConfiguration Configuration { get; }
    public IChunkLayout Layout { get; }
    public IChunkGenerator Generator { get; }
    public ChunkManager ChunkManager { get; }
}