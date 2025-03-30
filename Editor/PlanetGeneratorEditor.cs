using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Planet))]
public class PlanetGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // Draws the default inspector UI

        Planet generator = (Planet)target;

        if (GUILayout.Button("Regenerate"))
        {
            generator.Rebuild();
        }
    }
}