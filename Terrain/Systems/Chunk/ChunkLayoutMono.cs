using UnityEngine;

/// <summary>
/// Unity-facing driver for chunk layout.
/// Mirrors the follower's position into the layout each frame and (optionally)
/// samples camera/frustum data at a throttled cadence for culling/prioritization logic.
/// </summary>
public class ChunkLayoutMono : MonoBehaviour
{
    /// <summary>
    /// The transform that this chunk system follows, like the player.
    /// </summary>
    [Tooltip("The main character of the world. The object we should spawn chunks around.")]
    public Transform Follower;

    /// <summary>
    /// Manages the layout of the terrain.
    /// </summary>
    private IChunkLayout layout;

    /// <summary>
    /// Returns whether <see cref="Initialize(IChunkConfiguration, IChunkLayout, IChunkControllerFactory)"/> has been successful.
    /// </summary>
    private bool IsInitialized = false;

    /// <summary>
    /// Update various components.
    /// </summary>
    private void LateUpdate()
    {
        if (!IsInitialized || Follower == null)
            return;

        // This makes it so it is safe in a different thread as you cannot
        // access Transform in a different thread, there is just a very small delay.
        this.layout.FollowerWorldPosition = this.Follower.position;
    }

    /// <summary>
    /// Sets up the chunk manager with the required configuration, layout, and factory.
    /// </summary>
    /// <param name="configuration">Settings for chunk size and behavior.</param>
    /// <param name="layout">Logic to determine visible chunk positions.</param>
    /// <param name="factory">Factory that builds new chunk controllers.</param>
    /// <exception cref="System.ArgumentNullException">If any required dependency is missing.</exception>
    public void Initialize(IChunkLayout layout)
    {
        this.layout = layout;
        this.layout.Follower = this.Follower;
        this.layout.FollowerWorldPosition = this.Follower.position;

        this.IsInitialized = true;
    }
}