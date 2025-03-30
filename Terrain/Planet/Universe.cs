using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TerrainStore))]
public class Universe : MonoBehaviour
{
    public Transform Follower;

    public int PlanetChunkSize = 32;
    public Vector2Int PlanetChunksToLoad = new Vector2Int(5, 5);

    public PlanetChunk ChunkPrefab;
    private List<Planet> Planets;

    public TerrainStore Store;
    private void Start()
    {
        Store = GetComponent<TerrainStore>();
    }
}