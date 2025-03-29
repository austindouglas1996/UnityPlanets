using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PlanetRenderer))]
public class Planet : MonoBehaviour
{
    [Header("World")]
    public Universe Universe;
    public int Radius = 128;
    public float SurfaceRoughness = 0.05f;

    [Header("Noise")]
    public float Threshold = 0.5f;
    public int Octaves = 12;
    public float Noise = 0.1f;

    public Vector3 Center;

    public PlanetRenderer Renderer;

    private void Start()
    {
        Renderer = GetComponent<PlanetRenderer>();    
    }

    public PlanetMapData GenerateMap(Vector3Int coordinates)
    {
        PlanetMapData newMap = new PlanetMapData();
        newMap.DensityMap = MarchingCubes.GenerateRoundMap(Universe.PlanetChunkSize, coordinates, Center, Radius);
        return newMap;
    }
}
