using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Handles real-time terrain modification in the game world by applying user-selected brushes
/// (e.g., round brushes) to modify voxel-based terrain when the player clicks and holds the mouse.
/// Applies edits at a controlled rate using a cooldown timer to prevent excessive updates which considerably slows down the game.
/// </summary>
public class TerrainEditor : MonoBehaviour
{
    [SerializeField] public BrushType SelectedBrush;
    [SerializeField] public float modifyCooldown = 0.2f; // 200 ms
    [SerializeField] public ChunkManager chunkManager;

    private float lastModifyTime = 0f;

    void Update()
    {
        bool isAdding = Input.GetMouseButton(0);
        bool isRemoving = Input.GetMouseButton(1);

        if ((isAdding || isRemoving) && Time.time - lastModifyTime > modifyCooldown)
        {
            lastModifyTime = Time.time;
            TryModifyTerrain(isAdding);
        }
    }

    /// <summary>
    /// Try to modify the terrain.
    /// </summary>
    /// <param name="adding"></param>
    /// <returns></returns>
    private void TryModifyTerrain(bool adding)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        int layerMask = 1 << LayerMask.NameToLayer("Default");

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
        {
            chunkManager.ModifyTerrain(CreateBrush(hit.point), adding);
        }
    }

    /// <summary>
    /// Create a new brush for the terrain editing.
    /// </summary>
    /// <param name="worldPos"></param>
    /// <returns></returns>
    /// <exception cref="System.NotSupportedException"></exception>
    private TerrainBrush CreateBrush(Vector3 worldPos)
    {
        if (this.SelectedBrush == BrushType.Round)
        {
            return new RoundTerrainBrush(worldPos);
        }

        throw new System.NotSupportedException("Does not support that brush type.");
    }
}