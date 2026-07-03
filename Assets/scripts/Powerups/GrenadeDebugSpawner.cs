using UnityEngine;

namespace Game.Powerups
{
    // TEMPORARY (Slice G2/G3): editor-only debug helper.
    //   * press the spawn key (H) to drop a real grenade pickup from the sky, so the actual
    //     collection path can be exercised on demand without waiting for the spawn interval;
    //   * on-screen sliders tune the local player's GrenadeAimController charge feel live.
    // Self-bootstraps and survives scene loads. Removed in Slice G6 polish.
    //
    // Slider edits apply to the spawned player INSTANCE (lost when Play stops); once the
    // feel is dialled in, bake the numbers into the Black/White player prefabs out of Play.
    public class GrenadeDebugSpawner : MonoBehaviour
    {
        [SerializeField] private KeyCode spawnKey = KeyCode.H;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("GrenadeDebugGranter (AUTO)");
            DontDestroyOnLoad(go);
            go.AddComponent<GrenadeDebugSpawner>();
            Debug.Log("[GrenadeDebug] bootstrapped. Press 'H' to drop a grenade pickup from " +
                      "the sky, walk into it to collect, hold 'G' to charge and release to throw.");
        }
#endif

        private void Update()
        {
            if (!Input.GetKeyDown(spawnKey)) return;

            PowerupSpawner spawner = Object.FindFirstObjectByType<PowerupSpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[GrenadeDebug] no PowerupSpawner in scene - are you in " +
                                 "level_1-multiplayer? (it lives on the Bootstrap object).");
                return;
            }
            spawner.DebugSpawnNow();
            Debug.Log("[GrenadeDebug] dropped a grenade pickup from the sky.");
        }

        private static GameObject FindLocalPlayer()
        {
            var players = Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                var pv = p.GetComponent<Photon.Pun.PhotonView>();
                if (pv == null || pv.IsMine) return p.gameObject;
            }
            return players.Length > 0 ? players[0].gameObject : null;
        }

        private void OnGUI()
        {
            GameObject player = FindLocalPlayer();
            GrenadeInventory inv = player != null ? player.GetComponent<GrenadeInventory>() : null;
            GrenadeAimController aim = player != null ? player.GetComponent<GrenadeAimController>() : null;
            PowerupSpawner spawner = Object.FindFirstObjectByType<PowerupSpawner>();

            GUILayout.BeginArea(new Rect(10, 10, 340, 130), GUI.skin.box);
            GUILayout.Label($"Grenade debug — '{spawnKey}' drop pickup, hold 'G' to throw");
            GUILayout.Label(inv != null ? $"HasGrenade: {inv.HasGrenade}" : "HasGrenade: (no local player)");
            if (spawner != null)
            {
                // Applies to the next dropped pickup — press '{spawnKey}' again to see it.
                GUILayout.Label($"Pickup fall speed: {spawner.FallSpeed:F2} u/s");
                spawner.FallSpeed = GUILayout.HorizontalSlider(spawner.FallSpeed, 0.5f, 15f);
            }
            if (aim != null)
            {
                GUILayout.Label($"Charge ramp / sec: {aim.ChargeRampPerSecond:F2}");
                aim.ChargeRampPerSecond = GUILayout.HorizontalSlider(aim.ChargeRampPerSecond, 0.3f, 4f);
            }
            GUILayout.EndArea();
        }
    }
}
