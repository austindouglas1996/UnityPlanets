using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TerrainStore))]
public class Universe : MonoBehaviour
{
    [Header("Components")]
    public PlanetChunk ChunkPrefab;
    [HideInInspector] public TerrainStore ResourceStore;

    [Header("World")]
    public Transform Follower;

    [Header("Rendering")]
    public int PlanetChunkSize = 32;

    private void Start()
    {
        ResourceStore = GetComponent<TerrainStore>();
    }
}