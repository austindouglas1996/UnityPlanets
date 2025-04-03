using System.Threading.Tasks;
using UnityEngine;

public class TerrainEditor : MonoBehaviour
{
    public float brushRadius = 5f;
    public float brushIntensity = 0.1f;
    public float isolevel = 0.5f;
    public Planet planet;
        /*
    void Update()
    {
        if (Input.GetMouseButton(0)) // Left click to add terrain
        {
            TryModifyTerrain(true);
        }
        else if (Input.GetMouseButton(1)) // Right click to remove terrain
        {
            TryModifyTerrain(false);
        }
    }

    private void TryModifyTerrain(bool adding)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        int layerMask = 1 << LayerMask.NameToLayer("Default");

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
        {
            Vector3 worldPos = hit.point;
            float brushRadius = this.brushRadius;

            Bounds brushBounds = new Bounds(worldPos, Vector3.one * brushRadius * 2);

            foreach (var chunk in planet.ActiveChunks)
            {
                if (chunk == null)
                    continue;

                // Each chunk's world bounds
                Vector3 chunkSize = new Vector3(universe.PlanetChunkSize, universe.PlanetChunkSize, universe.PlanetChunkSize);
                Bounds chunkBounds = new Bounds(chunk.transform.position + chunkSize * 0.5f, chunkSize);

                if (brushBounds.Intersects(chunkBounds))
                {
                    // Modify this chunk using world-space brush
                    chunk.ModifyMap(worldPos, brushRadius, brushIntensity, adding);
                }
            }
        }
    }
    */
}