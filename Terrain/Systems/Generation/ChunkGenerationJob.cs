using System;

/// <summary>
/// Represents a single chunk generation request, containing its unique key 
/// and a callback to invoke when processing is complete.
/// </summary>
public class ChunkGenerationJob
{
    /// <summary>
    /// Creates a new chunk generation job.
    /// </summary>
    /// <param name="key">The unique identifier for the chunk to generate.</param>
    /// <param name="action">
    /// A callback invoked when the job completes. 
    /// The boolean parameter indicates success (<c>true</c>) or failure (<c>false</c>).
    /// </param>
    public ChunkGenerationJob(ChunkKey key, Action<bool> action)
    {
        this.Key = key;
        this.OnDone = action;
    }

    /// <summary>
    /// The unique identifier for the chunk.
    /// </summary>
    public ChunkKey Key;

    /// <summary>
    /// The completion callback for this job.
    /// </summary>
    public Action<bool> OnDone;

    /// <summary>
    /// Returns whether this job contains a collection of related keys.
    /// </summary>
    public bool IsEdge = false;
}
