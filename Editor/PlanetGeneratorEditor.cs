using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ChunkManager))]
public class PlanetGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // Draws the default inspector UI

        ChunkManager manager = (ChunkManager)target;

        if (GUILayout.Button("Rebuild"))
        {
            manager.Restart();
        }

        if (GUILayout.Button("ReColor"))
        {
            manager.UpdateChunkColors();
        }
    }
}