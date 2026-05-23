#if UNITY_EDITOR
using Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MultiplayerSceneEditor
{
    private const string MpLevelScene = "level_1-multiplayer";

    static MultiplayerSceneEditor()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.name != MpLevelScene) return;
        EditorApplication.delayCall += () =>
        {
            if (SceneManager.GetActiveScene().name != MpLevelScene) return;
            var spawner = Object.FindObjectOfType<MultiplayerSpawner>();
            if (spawner != null)
                MultiplayerSpawnerEditor.FrameSpawnPoints(spawner);
        };
    }

    [MenuItem("Multiplayer/Frame level_1-multiplayer spawn area")]
    private static void FrameFromMenu()
    {
        var spawner = Object.FindObjectOfType<MultiplayerSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("No MultiplayerSpawner in scene.");
            return;
        }
        MultiplayerSpawnerEditor.FrameSpawnPoints(spawner);
    }
}
#endif
