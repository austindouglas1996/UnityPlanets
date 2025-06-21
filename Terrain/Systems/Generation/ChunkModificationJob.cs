public class ChunkModificationJob
{
    public ChunkModificationJob(TerrainBrush brush, bool isAdding)
    {
        Brush = brush;
        IsAdding = isAdding;
    }
    public TerrainBrush Brush { get; private set; }
    public bool IsAdding { get; private set; }
}