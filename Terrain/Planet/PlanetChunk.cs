using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetChunk : MonoBehaviour
{
    public Vector3Int Coordinates;
    public Planet Planet;

    private PlanetChunkThreadData threadData;
    public int RenderDetail { get; set; } = -1;

    private bool updateScheduled = false;
    private bool updateRequested = false;

    private void Start()
    {
        this.AddComponent<FoliageGenerator>();
    }

    /// <summary>
    /// Start the initial generation of the planet.
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="coordinates"></param>
    /// <returns></returns>
    public async Task Generate(Planet owner, Vector3Int coordinates)
    {
        this.Planet = owner;
        this.Coordinates = coordinates;

        this.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Shader Graphs/VertexColor"));
        this.GetComponent<MeshRenderer>().material.SetFloat("_Smoothness", 0f);

        this.name = $"Chunk X:{coordinates.x} Y:{coordinates.y} Z:{coordinates.z}";
        await this.UpdateAsync(true);
    }

    /// <summary>
    /// Modify the terrain map using a brush. Basic modification.
    /// </summary>
    /// <param name="hitPoint"></param>
    /// <param name="radius"></param>
    /// <param name="intensity"></param>
    /// <param name="adding"></param>
    public void ModifyMap(Vector3 hitPoint, float radius, float intensity, bool adding = true)
    {
        DensityMapModifier.ModifyMapWithSphereBrush(ref threadData.MapData.DensityMap, this.Coordinates, hitPoint, radius, intensity, adding);
        ScheduleUpdate();
    }

    /// <summary>
    /// Schedule an update for the chunk to be updated. This helps with modifying the terrain but not wanting an extra every single tiny edit.
    /// </summary>
    private async void ScheduleUpdate()
    {
        if (updateScheduled)
        {
            updateRequested = true;
            return;
        }

        updateScheduled = true;

        await Task.Delay(50); // debounce window
        await UpdateAsync();

        updateScheduled = false;

        if (updateRequested)
        {
            updateRequested = false;
            ScheduleUpdate();
        }
    }

    /// <summary>
    /// Update the chunk data. Processing the density map, and recreating the mesh and its colors.
    /// </summary>
    /// <param name="initial"></param>
    /// <returns></returns>
    public async Task UpdateAsync(bool initial = false)
    {
        SphereDensityMapGenerator mapGenerator = new(Planet.Center, Planet.Radius, Planet.MapOptions);

        Color[] colors = null;
        Matrix4x4 localToWorld = transform.localToWorldMatrix;

        await Task.Run(() =>
        {
            ChunkData mapData;

            if (initial || threadData == null)
            {
                float[,,] densityMap = mapGenerator.Generate(Planet.Universe.PlanetChunkSize, this.Coordinates);
                MeshData meshData = mapGenerator.GenerateMeshData(densityMap, new Vector3(0, 0, 0));
                mapData = new ChunkData(densityMap, meshData);
            }
            else
                mapData = threadData.MapData;

            mapData.MeshData = mapGenerator.GenerateMeshData(mapData.DensityMap, new Vector3(0, 0, 0));

            colors = new Color[mapData.MeshData.Vertices.Count];
            for (int i = 0; i < mapData.MeshData.Vertices.Count; i++)
            {
                Vector3 worldPos = localToWorld.MultiplyPoint3x4(mapData.MeshData.Vertices[i]);
                float distance = (worldPos - Planet.Center).magnitude;
                float normalized = Mathf.InverseLerp(Planet.StartSurfaceColorRadius, Planet.EndSurfaceColorRadius, distance);
                Color vertexColor = Planet.MapOptions.SurfaceColorRange.Evaluate(normalized);
                colors[i] = vertexColor;
            }

            threadData = new PlanetChunkThreadData(mapData);
        });

        if (threadData == null)
        {
            return;
        }

        //this.GetComponent<FoliageGenerator>().ApplyMap(Planet.Universe.Store, threadData.Cube);

        Mesh newMesh = mapGenerator.GenerateMesh(threadData.MapData.MeshData);

        newMesh.colors = colors;

        this.GetComponent<MeshFilter>().mesh = newMesh;
        this.GetComponent<MeshCollider>().sharedMesh = newMesh;
    }

    /// <summary>
    /// Set the chunk visible. Helps with knowing whether to continue to render/update content.
    /// </summary>
    /// <param name="visible"></param>
    public void SetVisible(bool visible)
    {
        this.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Container for transferring data through threads.
    /// </summary>
    public class PlanetChunkThreadData
    {
        public PlanetChunkThreadData(ChunkData mapData)
        {
            this.MapData = mapData;
        }

        public ChunkData MapData { get; set; }
    }
}