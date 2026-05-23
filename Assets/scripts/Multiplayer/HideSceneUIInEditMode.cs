using System.Collections.Generic;
using UnityEngine;

namespace Multiplayer
{
    // Hides full-screen gameplay UI while editing the level so platforms and spawn gizmos stay visible.
    // UI is restored automatically when entering Play mode.
    [ExecuteAlways]
    public class HideSceneUIInEditMode : MonoBehaviour
    {
        [Tooltip("Object names to hide in Edit mode (BG, menus, etc.). Found anywhere in the loaded scene.")]
        [SerializeField] private string[] hideObjectNames =
        {
            "BG",
            "EndGameMenu",
            "CountDownAnimation",
            "PauseMenu",
        };

        private readonly List<GameObject> _hidden = new List<GameObject>();

        private void OnEnable() => Apply();
        private void OnDisable() => Restore();

        private void Apply()
        {
            Restore();
            if (Application.isPlaying) return;

            foreach (string objectName in hideObjectNames)
            {
                GameObject go = GameObject.Find(objectName);
                if (go == null || !go.activeSelf) continue;
                go.SetActive(false);
                _hidden.Add(go);
            }
        }

        private void Restore()
        {
            foreach (GameObject go in _hidden)
            {
                if (go != null)
                    go.SetActive(true);
            }
            _hidden.Clear();
        }
    }
}
