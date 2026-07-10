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

            var panelRect = new Rect(10, 10, 330, 296);
            GUI.DrawTexture(panelRect, PanelBg);
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label($"Grenade debug (Editor only) — '{spawnKey}' drops a pickup");
            GUILayout.Label(inv != null ? $"Grenades held: {inv.Count}" : "Grenades held: (no local player)");
            if (inv != null)
                inv.DebugAlwaysHaveGrenade = GUILayout.Toggle(
                    inv.DebugAlwaysHaveGrenade, " always have a grenade (turn OFF to test collection)");

            // Static overrides. Fallback constants MUST match the values baked in
            // Grenade.prefab / GrenadeProjectile — the slider re-assigns them every frame.
            float fuse = GrenadeProjectile.DebugFuseOverride > 0f
                ? GrenadeProjectile.DebugFuseOverride : 2.31f;
            GUILayout.Label($"Fuse seconds (next throw): {fuse:F2}");
            GrenadeProjectile.DebugFuseOverride = GUILayout.HorizontalSlider(fuse, 0.5f, 10f);
            GrenadeProjectile.DebugFuseLightAlwaysOn = GUILayout.Toggle(
                GrenadeProjectile.DebugFuseLightAlwaysOn, " fuse light always ON (visibility test)");
            float blink = GrenadeProjectile.DebugBlinkHzOverride > 0f
                ? GrenadeProjectile.DebugBlinkHzOverride : 1.5f;
            GUILayout.Label($"Fuse light blinks/sec (live): {blink:F1}");
            GrenadeProjectile.DebugBlinkHzOverride = GUILayout.HorizontalSlider(blink, 0.5f, 12f);

            float trailLife = GrenadeProjectile.DebugTrailLifetimeOverride > 0f
                ? GrenadeProjectile.DebugTrailLifetimeOverride : 0.45f;
            GUILayout.Label($"Trail decay seconds (next throw): {trailLife:F2}");
            GrenadeProjectile.DebugTrailLifetimeOverride = GUILayout.HorizontalSlider(trailLife, 0.1f, 2f);

            float trailRate = GrenadeProjectile.DebugTrailRateOverride >= 0f
                ? GrenadeProjectile.DebugTrailRateOverride : 40f;
            GUILayout.Label($"Trail particles/sec (next throw): {trailRate:F0}");
            GrenadeProjectile.DebugTrailRateOverride = GUILayout.HorizontalSlider(trailRate, 0f, 120f);
            GUILayout.EndArea();
        }
    }
}
#endif
