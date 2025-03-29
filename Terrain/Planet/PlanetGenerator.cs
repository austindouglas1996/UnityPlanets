using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetGenerator : MonoBehaviour
{
    [Header("World")]
    public int Radius = 128;
    public float Roughness = 0.05f;

    [Header("Chunk")]
    public int ChunkSize = 64;

    [Header("Noise")]
    public float threshold = 0.5f;
    public int octaves = 12;
    public float noise = 0.1f;

    [Header("Rendering")]
    public Material caveMaterial;
    public PlanetChunk ChunkPrefab;

    public Vector3 WorldCenter;
    public Dictionary<Vector3Int, PlanetChunk> Chunks = new Dictionary<Vector3Int, PlanetChunk>();

    public TerrainStore Store
    {
        get
        {
            return GetComponent<TerrainStore>();
        }
    }

    void Start()
    {
        this.Generate();
    }

    public void Generate()
    {
        while (transform.childCount != 0)
        {
            foreach (Transform child in transform)
            {
                DestroyImmediate(child.gameObject);
            }
        }

        Chunks.Clear();

        float diameter = Radius * 2f;
        int chunksPerAxis = Mathf.CeilToInt(diameter / ChunkSize);

        // Calculate total world size
        float worldSize = chunksPerAxis * ChunkSize;

        // Set planet center
        WorldCenter = new Vector3(worldSize * 0.5f, worldSize * 0.5f, worldSize * 0.5f);

        for (int x = 0; x < chunksPerAxis; x++)
        {
            for (int y = 0; y < chunksPerAxis; y++)
            {
                for (int z = 0; z < chunksPerAxis; z++)
                {
                    // Instantiate chunk
                    PlanetChunk ch = Instantiate(ChunkPrefab, new Vector3(x * ChunkSize, y * ChunkSize, z * ChunkSize), Quaternion.identity, this.transform);
                    ch.Generate(this, new Vector3Int(x, y, z), this.ChunkSize);
                    ch.GetComponent<MeshRenderer>().material = caveMaterial;

                    this.Chunks.Add(new Vector3Int(x, y, z), ch);

                    ch.GenerateTerrain();
                }
            }
        }
    }
}