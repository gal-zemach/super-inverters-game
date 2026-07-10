#if UNITY_EDITOR
using UnityEngine;

namespace Game.Powerups
{
    // TESTING ONLY — the entire file is compiled out of real builds (#if UNITY_EDITOR),
    // so nothing here can leak into the shipped WebGL build. Re-added after the tuning
    // strip for two live-testing needs:
    //   * spawnKey (H) drops a real pickup from the sky, exercising the collection path
    //     on demand instead of waiting out the spawn interval;
    //   * a toggle for GrenadeInventory.DebugAlwaysHaveGrenade. Leave it OFF when testing
    //     collection — while you already hold a grenade the single-slot rule makes every
    //     pickup a no-op, which looks exactly like "collecting doesn't work".
    public class GrenadeDebugSpawner : MonoBehaviour
    {
        [SerializeField] private KeyCode spawnKey = KeyCode.H;

        private static Texture2D s_panelBg;
        private static Texture2D PanelBg
        {
            get
            {
                if (s_panelBg == null)
                {
                    s_panelBg = new Texture2D(1, 1);
                    s_panelBg.SetPixel(0, 0, new Color(0.13f, 0.13f, 0.13f, 1f));
                    s_panelBg.Apply();
                }
                return s_panelBg;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("GrenadeDebugSpawner (AUTO)");
            DontDestroyOnLoad(go);
            go.AddComponent<GrenadeDebugSpawner>();
            Debug.Log("[GrenadeDebug] bootstrapped (Editor only). 'H' drops a pickup; " +
                      "toggle 'always have a grenade' OFF to test collection.");
        }

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

        private static GrenadeInventory FindLocalInventory()
        {
            var players = Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                var pv = p.GetComponent<Photon.Pun.PhotonView>();
                if (pv == null || pv.IsMine) return p.GetComponent<GrenadeInventory>();
            }
            return null;
        }

        private void OnGUI()
        {
            GrenadeInventory inv = FindLocalInventory();
            PowerupSpawner spawner = Object.FindFirstObjectByType<PowerupSpawner>();

            var panelRect = new Rect(10, 10, 330, 208);
            GUI.DrawTexture(panelRect, PanelBg);
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label($"Grenade debug (Editor only) — '{spawnKey}' drops a pickup");
            GUILayout.Label(inv != null ? $"Grenades held: {inv.Count}" : "Grenades held: (no local player)");
            if (inv != null)
                inv.DebugAlwaysHaveGrenade = GUILayout.Toggle(
                    inv.DebugAlwaysHaveGrenade, " always have a grenade (turn OFF to test collection)");

            if (spawner != null)
            {
                GUILayout.Label($"Pickup fall speed (next pickup): {spawner.FallSpeed:F2} u/s");
                spawner.FallSpeed = GUILayout.HorizontalSlider(spawner.FallSpeed, 0.5f, 15f);
            }

            // Fallback constant MUST match the radius baked in GrenadePickup.prefab.
            float radius = GrenadePickup.DebugColliderRadiusOverride > 0f
                ? GrenadePickup.DebugColliderRadiusOverride : 0.25f;
            GUILayout.Label($"Pickup hitbox radius (live): {radius:F2}");
            GrenadePickup.DebugColliderRadiusOverride = GUILayout.HorizontalSlider(radius, 0.1f, 1.5f);

            // Fallback MUST match collectAnimSeconds' serialized default in GrenadePickup.
            float collectAnim = GrenadePickup.DebugCollectAnimOverride > 0f
                ? GrenadePickup.DebugCollectAnimOverride : 0.6f;
            GUILayout.Label($"Collect whiten seconds (next pickup): {collectAnim:F1}");
            GrenadePickup.DebugCollectAnimOverride = GUILayout.HorizontalSlider(collectAnim, 0.2f, 8f);
            GUILayout.EndArea();
        }
    }
}
#endif
