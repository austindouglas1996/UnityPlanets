public class ChunkModificationJob
{
    public ChunkModificationJob(ChunkData existingData, TerrainBrush brush, bool isAdding)
    {
        ExistingData = existingData;
        Brush = brush;
        IsAdding = isAdding;
    }

    public ChunkData ExistingData { get; private set; }
    public TerrainBrush Brush { get; private set; }
    public bool IsAdding { get; private set; }
}