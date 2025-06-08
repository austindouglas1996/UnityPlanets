using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using static UnityEditor.PlayerSettings;

/// <summary>
/// Manages all active chunks in the world. Handles loading, unloading, re-coloring,
/// and modifying terrain based on player movement and brush interactions.
/// </summary>
[RequireComponent(typeof(ChunkRenderer))]
public class ChunkManager : MonoBehaviour
{
    /// <summary>
    /// The transform that this chunk system follows, like the player.
    /// </summary>
    [HideInInspector] public Transform Follower;

    [SerializeField] public ComputeShader MarchingCubes;
    [SerializeField] public ComputeShader GenerateDensity;

    /// <summary>
    /// Services used to help with chunk generation and management.
    /// </summary>
    public IChunkServices Services { get; private set; }

    /// <summary>
    /// Handles the chunk rendering and logic.
    /// </summary>
    public ChunkRenderer Renderer { get; private set; }

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
        this.Services.Layout.FollowerWorldPosition = this.Follower.position;
    }

    /// <summary>
    /// Sets up the chunk manager with the required configuration, layout, and factory.
    /// </summary>
    /// <param name="configuration">Settings for chunk size and behavior.</param>
    /// <param name="layout">Logic to determine visible chunk positions.</param>
    /// <param name="factory">Factory that builds new chunk controllers.</param>
    /// <exception cref="System.ArgumentNullException">If any required dependency is missing.</exception>
    public void Initialize(Transform follower, IChunkServices services)
    {
        this.Follower = follower;
        this.Services = services;
        this.Renderer = this.GetComponent<ChunkRenderer>();

        this.Services.Layout.Follower = this.Follower;
        this.Services.Layout.FollowerWorldPosition = this.Follower.position;

        this.IsInitialized = true;
    }
}