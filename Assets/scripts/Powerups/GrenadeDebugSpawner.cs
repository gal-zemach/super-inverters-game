using UnityEngine;

namespace Game.Powerups
{
    // TEMPORARY (Slice G1): editor-only debug spawner for the grenade projectile.
    //
    // It SELF-BOOTSTRAPS at play start and survives scene loads (DontDestroyOnLoad),
    // so it works through the main_menu -> room -> level flow in BOTH editors with no
    // scene wiring to lose. Press the spawn key (default G) with the Game view focused
    // to lob a grenade from the mouse position, ignoring all match gating.
    //
    // Repurposed into a "grant grenade" key in Slice G2, then removed once the real
    // pickup path (G3+) works. Wrapped in UNITY_EDITOR so it never ships in a build.
    public class GrenadeDebugSpawner : MonoBehaviour
    {
        [SerializeField] private GrenadeProjectile grenadePrefab;
        [SerializeField] private KeyCode spawnKey = KeyCode.G;
        [Tooltip("Launch speed (world units/sec) in the player->mouse direction. Gravity turns it into an arc.")]
        [SerializeField] private float throwForce = 26f;
        [Tooltip("How far from the player centre the grenade spawns, along the aim direction.")]
        [SerializeField] private float spawnOffset = 1.2f;
        [SerializeField] private Framework debugFramework = Framework.WHITE;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("GrenadeDebugSpawner (AUTO)");
            DontDestroyOnLoad(go);
            var sp = go.AddComponent<GrenadeDebugSpawner>();
            sp.grenadePrefab = UnityEditor.AssetDatabase
                .LoadAssetAtPath<GrenadeProjectile>("Assets/Prefabs/Grenade.prefab");
            Debug.Log("[GrenadeDebug] spawner bootstrapped. prefabLoaded=" +
                      (sp.grenadePrefab != null) + ". Focus the Game view and press '" +
                      sp.spawnKey + "' to lob a grenade.");
        }
#endif

        private void Update()
        {
            if (!Input.GetKeyDown(spawnKey)) return;
            if (grenadePrefab == null)
            {
                Debug.LogWarning("[GrenadeDebug] grenadePrefab is null - nothing to spawn.");
                return;
            }

            // Re-acquire each press so it survives the menu->level camera swap.
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[GrenadeDebug] Camera.main is null - cannot read the aim.");
                return;
            }

            Transform player = FindLocalPlayer();
            if (player == null)
            {
                Debug.LogWarning("[GrenadeDebug] no local player found - are you in the level and spawned in?");
                return;
            }

            // Input.mousePosition reports the real pointer even when it is OUTSIDE the
            // Game view; fall back to screen centre so the aim stays sane.
            Vector3 mouse = Input.mousePosition;
            bool mouseOnScreen = new Rect(0f, 0f, Screen.width, Screen.height).Contains(mouse);
            Vector2 mouseWorld = mouseOnScreen
                ? (Vector2)cam.ScreenToWorldPoint(mouse)
                : (Vector2)cam.transform.position;

            // Throw FROM the player TOWARD the mouse azimuth; gravity turns it into an arc.
            Vector2 from = player.position;
            Vector2 dir = mouseWorld - from;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();

            Vector2 spawnPos = from + dir * spawnOffset;   // clear the player's own body
            Vector2 velocity = dir * throwForce;

            GrenadeProjectile g = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
            g.Init(debugFramework, velocity);

            Debug.Log($"[GrenadeDebug] thrown from {from} toward {mouseWorld} | dir={dir} force={throwForce} mouseOnScreen={mouseOnScreen}");
        }

        // Locates the local avatar: the PlayerManager whose PhotonView IsMine
        // (or any, when running offline). Debug-only lookup.
        private static Transform FindLocalPlayer()
        {
            var players = Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                var pv = p.GetComponent<Photon.Pun.PhotonView>();
                if (pv == null || pv.IsMine) return p.transform;
            }
            return players.Length > 0 ? players[0].transform : null;
        }

        // On-screen debug controls so the throw feel can be tuned live during Play.
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 120), GUI.skin.box);
            GUILayout.Label($"Grenade debug — aim with mouse, press '{spawnKey}' to throw");
            GUILayout.Label($"Throw force: {throwForce:F1}");
            throwForce = GUILayout.HorizontalSlider(throwForce, 5f, 60f);
            GUILayout.Label($"Spawn offset: {spawnOffset:F2}");
            spawnOffset = GUILayout.HorizontalSlider(spawnOffset, 0.5f, 3f);
            GUILayout.EndArea();
        }
    }
}
