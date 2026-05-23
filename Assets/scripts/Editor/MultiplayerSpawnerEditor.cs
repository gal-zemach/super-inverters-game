using Multiplayer;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MultiplayerSpawner))]
public class MultiplayerSpawnerEditor : Editor
{
    private const float PlayerFootOffsetY = SpawnPlatformPreview.PlayerFootOffsetY;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var spawner = (MultiplayerSpawner)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Scene editing", EditorStyles.boldLabel);

        if (GUILayout.Button("Frame spawn points in Scene view"))
            FrameSpawnPoints(spawner);

        EditorGUILayout.HelpBox(
            "In the Scene view (not Game view): drag the colored spawn handles. " +
            "The green ring shows where the player lands (platform below). " +
            "Yellow line = drop from spawn height to landing.",
            MessageType.Info);
    }

    private void OnSceneGUI()
    {
        var spawner = (MultiplayerSpawner)target;
        if (spawner == null) return;

        Physics2D.SyncTransforms();

        DrawSpawnHandles(spawner, spawner.BlackSpawnPositionsForEditor, Color.black, "Black");
        DrawSpawnHandles(spawner, spawner.WhiteSpawnPositionsForEditor, Color.white, "White");
    }

    private static void DrawSpawnHandles(MultiplayerSpawner spawner, Vector2[] positions, Color color, string label)
    {
        if (positions == null) return;

        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 spawn = new Vector3(positions[i].x, positions[i].y, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(spawn, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(spawner, "Move spawn point");
                positions[i] = new Vector2(moved.x, moved.y);
                EditorUtility.SetDirty(spawner);
            }

            bool hasLanding = SpawnPlatformPreview.TryGetLandingPoint(positions[i], out Vector2 land, out _);
            Vector3 landing = hasLanding
                ? new Vector3(land.x, land.y + PlayerFootOffsetY, 0f)
                : spawn;

            Handles.color = new Color(color.r, color.g, color.b, 1f);
            Handles.DrawSolidDisc(spawn, Vector3.forward, 0.35f);

            if (hasLanding)
            {
                Handles.color = Color.green;
                Handles.DrawWireDisc(landing, Vector3.forward, 0.55f);
                Handles.DrawDottedLine(spawn, landing, 4f);
                Handles.Label(landing + Vector3.up * 0.8f, $"{label} spawn {i + 1} lands here");
            }
            else
            {
                Handles.color = Color.red;
                Handles.Label(spawn + Vector3.up * 0.5f, $"{label} spawn {i + 1}: NO PLATFORM");
            }
        }
    }

    public static void FrameSpawnPoints(MultiplayerSpawner spawner)
    {
        var bounds = new Bounds(Vector3.zero, Vector3.one);
        bool init = false;

        void Encapsulate(Vector2[] pts)
        {
            if (pts == null) return;
            foreach (var p in pts)
            {
                var v = new Vector3(p.x, p.y, 0f);
                if (!init) { bounds = new Bounds(v, Vector3.zero); init = true; }
                else bounds.Encapsulate(v);
                if (SpawnPlatformPreview.TryGetLandingPoint(p, out Vector2 land, out _))
                    bounds.Encapsulate(new Vector3(land.x, land.y, 0f));
            }
        }

        Encapsulate(spawner.BlackSpawnPositionsForEditor);
        Encapsulate(spawner.WhiteSpawnPositionsForEditor);

        if (!init)
        {
            bounds = new Bounds(new Vector3(0f, 25f, 0f), new Vector3(80f, 40f, 1f));
        }
        else
        {
            bounds.Expand(8f);
        }

        var view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            view.in2DMode = true;
            view.Frame(bounds, false);
            view.Repaint();
        }
    }
}
