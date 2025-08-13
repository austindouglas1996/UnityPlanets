/// <summary>
/// Central access point for all chunk-related systems and settings.
/// </summary>
public interface IChunkServices
{
    /// <summary>
    /// Current chunk configuration/settings.
    /// </summary>
    public IChunkConfiguration Configuration { get; }

    /// <summary>
    /// Handles chunk grid layout and positioning.
    /// </summary>
    public IChunkLayout Layout { get; }

    /// <summary>
    /// Generates chunk data.
    /// </summary>
    public IChunkGenerator Generator { get; }

    /// <summary>
    /// Manages active chunks in the world.
    /// </summary>
    public ChunkManager ChunkManager { get; }
}
