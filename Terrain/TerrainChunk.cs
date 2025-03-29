using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using VHierarchy.Libs;
using static UnityEngine.Mesh;

public class TerrainChunk : MonoBehaviour
{
    public bool doneGenerating = false;
    public bool Regenerate = false;
    public int renderDetail = 6;

    private Vector2 coordinates;
    private Vector2 position;
    private MapGenerator generator;
    private TerrainThreadData terrainData;

    private async void OnValidate()
    {
        if (Regenerate)
        {
            Regenerate = false;
            await UpdateTerrainAsync();
        }
    }

    public async Task Generate(MapGenerator generator, Vector2 coord, int size, int renderDetail)
    {
        this.generator = generator;
        this.coordinates = coord;
        this.position = coord * size;

        this.name = $"TerrainChunk_{coord.x}_{coord.y}";
        this.AddComponent<MeshRenderer>();
        this.AddComponent<MeshFilter>();
        this.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        this.GetComponent<MeshRenderer>().material.SetFloat("_Smoothness", 0f);
        this.AddComponent<FoliageGenerator>();

        this.renderDetail = renderDetail;
        this.transform.position = new Vector3(this.position.x, 0, this.position.y) * 1f;
        this.transform.localScale = Vector3.one;

        await this.UpdateTerrainAsync();
    }

    public async Task UpdateTerrainAsync()
    {
        await Task.Run(() =>
        {
            MapData mapData = this.generator.GenerateMapData(this.coordinates, this.position);
            MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, this.generator.meshHeightMultiplier, this.generator.meshHeightCurve, renderDetail);

            // Store the results in a shared variable.
            terrainData = new TerrainThreadData(mapData, meshData, mapData.colourMap);
        });

        if (terrainData != null)
        {
            if (this.GetComponent<FoliageGenerator>() != null && renderDetail < 3)
            {
                //this.GetComponent<FoliageGenerator>().ApplyMap(this.generator, terrainData);
            }

            if (this.renderDetail > 12)
            {
                foreach (Transform child in this.transform)
                    child.gameObject.SetActive(false);
            }
            else
            {
                foreach (Transform child in this.transform)
                    child.gameObject.SetActive(true);
            }

            this.GetComponent<MeshFilter>().sharedMesh = terrainData.MeshData.CreateMesh();
            this.GetComponent<MeshRenderer>().material.mainTexture = TextureGenerator.TextureFromColourMap(terrainData.ColorMap, generator.MapChunkSize, generator.MapChunkSize);

            // Update collider.
            this.GetComponent<MeshCollider>().DestroyImmediate();
            this.AddComponent<MeshCollider>();

            this.doneGenerating = true;
        }
    }

    public void SetRenderDetail(int detail)
    {
        renderDetail = detail;
    }

    public void SetVisible(bool visible)
    {
        this.gameObject.SetActive(visible);
    }

    public bool IsVisible()
    {
        return this.gameObject.activeSelf;
    }

    public class TerrainThreadData
    {
        public TerrainThreadData(MapData mapData, MeshData meshData, Color[] colorMap)
        {
            this.MapData = mapData;
            this.MeshData = meshData;
            this.ColorMap = colorMap;
        }

        public MapData MapData { get; set; }
        public MeshData MeshData { get; set; }
        public Color[] ColorMap{ get; set; }
    }
}