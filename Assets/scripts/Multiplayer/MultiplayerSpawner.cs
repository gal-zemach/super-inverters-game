using System.Collections.Generic;
using ExitGames.Client.Photon;
using Game;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Multiplayer
{
    // Slice 2: spawns one Player per peer with the right Framework.
    //
    // Each peer claims its color via a `myFramework` player custom property
    // when it spawns. New joiners scan the existing PlayerList for any peer
    // that has already claimed and take whichever color isn't claimed. Falls
    // back to the `hostColor` heuristic (master client takes hostColor, joiner
    // takes the opposite) only when no one in the room has claimed yet.
    //
    // This handles the original-host disconnect/rejoin case: the original
    // joiner stays in the room with myFramework=WHITE; when the original host
    // rejoins (no longer master), they see WHITE is claimed and take BLACK,
    // instead of the previous code which would have given them WHITE again
    // because hostColor + IsMasterClient said so.
    public class MultiplayerSpawner : MonoBehaviourPunCallbacks
    {
        private const int SpawnPointCount = 3;

        public const string HostColorProperty = "hostColor";
        public const string MyFrameworkProperty = "myFramework";

        [Tooltip("Prefab name (under any Resources/ folder) for the BLACK player. Default 'BlackPlayer' resolves to Assets/Resources/BlackPlayer.prefab.")]
        [SerializeField] private string blackPrefabName = "BlackPlayer";

        [Tooltip("Prefab name (under any Resources/ folder) for the WHITE player. Default 'WhitePlayer' resolves to Assets/Resources/WhitePlayer.prefab.")]
        [SerializeField] private string whitePrefabName = "WhitePlayer";

        [Tooltip("World positions for BLACK spawns (one chosen at random per life).")]
        [SerializeField] private Vector2[] blackSpawnPositions =
        {
            new Vector2(-30.7f, 34.89f),
            new Vector2(-10.7f, 34.89f),
            new Vector2(-40.7f, 44.89f),
        };

        [Tooltip("World positions for WHITE spawns (one chosen at random per life).")]
        [SerializeField] private Vector2[] whiteSpawnPositions =
        {
            new Vector2(38.12f, 23.59f),
            new Vector2(20.12f, 23.59f),
            new Vector2(30.7f, 34.89f),
        };

        [Tooltip("Only spawn when this scene is active. Prevents the lobby Bootstrap (which is also a copy of this) from spawning a player that PhotonNetwork.LoadLevel would immediately destroy.")]
        [SerializeField] private string targetSceneName = MultiplayerSceneNames.GameSceneName;

#if UNITY_EDITOR
        public Vector2[] BlackSpawnPositionsForEditor => blackSpawnPositions;
        public Vector2[] WhiteSpawnPositionsForEditor => whiteSpawnPositions;
#endif

        private bool spawned;
        private Vector2? _cachedLocalSpawnPosition;

        // Prevents two TrySpawn calls in the same frame (Start + room props).
        private static bool s_spawnInProgress;

        private Coroutine _spawnCoroutine;

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureSpawnArrayLength(ref blackSpawnPositions, new Vector2(-3f, 1f));
            EnsureSpawnArrayLength(ref whiteSpawnPositions, new Vector2(3f, 1f));
        }

        private static void EnsureSpawnArrayLength(ref Vector2[] positions, Vector2 fallback)
        {
            if (positions == null || positions.Length == 0)
            {
                positions = new Vector2[SpawnPointCount];
                for (int i = 0; i < SpawnPointCount; i++)
                    positions[i] = fallback;
                return;
            }

            if (positions.Length == SpawnPointCount) return;

            var resized = new Vector2[SpawnPointCount];
            for (int i = 0; i < SpawnPointCount; i++)
                resized[i] = i < positions.Length ? positions[i] : fallback;
            positions = resized;
        }
#endif

        private void Start()
        {
            // Post-LoadLevel case: we're now in the game scene, already in a
            // room. OnJoinedRoom won't fire here, so kick off the spawn from
            // Start instead.
            if (PhotonNetwork.InRoom && IsInTargetScene())
            {
                RequestSpawn(forceRespawn: true);
            }
        }

        public override void OnJoinedRoom()
        {
            // Only spawn when we're in the game scene. In the lobby scene this
            // is a no-op; the master will load the game scene when both peers
            // are present, and Start above will fire spawn there.
            if (IsInTargetScene())
            {
                TrySpawn();
            }
        }

        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(HostColorProperty) && IsInTargetScene())
            {
                TrySpawn();
            }
        }

        private bool IsInTargetScene()
        {
            return SceneManager.GetActiveScene().name == targetSceneName;
        }

        private void OnDrawGizmos()
        {
            DrawSpawnGizmos(blackSpawnPositions, Color.black, "BLACK SPAWN");
            DrawSpawnGizmos(whiteSpawnPositions, Color.white, "WHITE SPAWN");
        }

        private static void DrawSpawnGizmos(Vector2[] positions, Color fill, string prefix)
        {
            if (positions == null) return;
            for (int i = 0; i < positions.Length; i++)
                DrawSpawnGizmo(positions[i], fill, $"{prefix} {i + 1}");
        }

        private static void DrawSpawnGizmo(Vector2 position, Color fill, string label)
        {
            Vector3 p = new Vector3(position.x, position.y, 0f);

            Gizmos.color = fill;
            Gizmos.DrawSphere(p, 0.35f);

            if (SpawnPlatformPreview.TryGetLandingPoint(position, out Vector2 land, out _))
            {
                Vector3 landing = new Vector3(land.x, land.y + 0.3f, 0f);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(landing, 0.55f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(p, landing);
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(p, p + Vector3.down * 2f);
            }

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.Label(p + new Vector3(0.7f, 0.5f, 0f), label);
#endif
        }

        public void ForceRespawn()
        {
            RequestSpawn(forceRespawn: true);
        }

        private void RequestSpawn(bool forceRespawn)
        {
            if (_spawnCoroutine != null)
                StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = StartCoroutine(SpawnWhenReady(forceRespawn));
        }

        private System.Collections.IEnumerator SpawnWhenReady(bool forceRespawn)
        {
            DestroyStaleLocalNetworkedPlayers();
            spawned = false;

            const float timeout = 3f;
            float elapsed = 0f;
            while (FindLocalNetworkedPlayer() != null && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (FindLocalNetworkedPlayer() != null)
            {
                DestroyStaleLocalNetworkedPlayers();
                yield return null;
            }

            TrySpawn(forceRespawn);
            _spawnCoroutine = null;
        }

        public void ResetLocalPlayerToSpawnPosition()
        {
            var local = FindLocalNetworkedPlayer();
            if (local == null) return;

            var state = local.GetComponent<PlayerState>();
            if (state == null) return;

            Vector2 spawnPos = GetCachedOrPickSpawnPosition(state.player_framework, forceNewPick: false);

            var rb = local.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.position = spawnPos;
            }
            else
            {
                local.transform.position = spawnPos;
            }

            EnsureSpawnPlatformPainted(state.player_framework, spawnPos);
        }

        private Vector2 GetCachedOrPickSpawnPosition(Framework color, bool forceNewPick)
        {
            if (!forceNewPick && _cachedLocalSpawnPosition.HasValue)
                return _cachedLocalSpawnPosition.Value;

            Vector2 picked = PickSpawnPosition(color, forceNewPick: true);
            _cachedLocalSpawnPosition = picked;
            return picked;
        }

        private Vector2 PickSpawnPosition(Framework color, bool forceNewPick)
        {
            Vector2[] positions = color == Framework.BLACK ? blackSpawnPositions : whiteSpawnPositions;
            if (positions == null || positions.Length == 0)
            {
                Debug.LogWarning($"[Multiplayer] No spawn positions configured for {color}.");
                return color == Framework.BLACK ? new Vector2(-3f, 1f) : new Vector2(3f, 1f);
            }

            if (!forceNewPick && _cachedLocalSpawnPosition.HasValue)
                return _cachedLocalSpawnPosition.Value;

            int index = Random.Range(0, positions.Length);
            return positions[index];
        }

        private static void EnsureSpawnPlatformPainted(Framework playerColor, Vector2 spawnPos)
        {
            var gameManager = Object.FindObjectOfType<GameManager>();
            gameManager?.EnsureSpawnPlatformMatchesPlayer(playerColor, spawnPos);
        }

        public bool IsLocalPlayer(string killedPlayerName)
        {
            string baseName = killedPlayerName.Replace("(Clone)", "").Trim();
            var local = FindLocalNetworkedPlayer();
            if (local == null) return false;
            return local.name.Replace("(Clone)", "").Trim().StartsWith(baseName);
        }

        private static GameObject FindLocalNetworkedPlayer()
        {
            foreach (var go in GameObject.FindGameObjectsWithTag(Values.PLAYER_TAG))
            {
                var pv = go.GetComponentInChildren<PhotonView>();
                if (pv != null && pv.IsMine) return go;
            }
            return null;
        }

        private static void DestroyStaleLocalNetworkedPlayers()
        {
            var toDestroy = new List<GameObject>();
            foreach (var go in GameObject.FindGameObjectsWithTag(Values.PLAYER_TAG))
            {
                var pv = go.GetComponentInChildren<PhotonView>();
                if (pv != null && pv.IsMine) toDestroy.Add(go);
            }

            if (toDestroy.Count == 0) return;

            foreach (var go in toDestroy)
            {
                PhotonNetwork.Destroy(go);
            }
        }

        private void TrySpawn(bool forceRespawn = false)
        {
            if (spawned && FindLocalNetworkedPlayer() == null)
                spawned = false;

            if (spawned || s_spawnInProgress) return;

            if (!forceRespawn && FindLocalNetworkedPlayer() != null)
            {
                spawned = true;
                return;
            }

            if (!TryGetHostColor(out Framework hostColor))
            {
                Debug.Log("[Multiplayer] Spawner waiting: hostColor room property not set yet.");
                return;
            }

            Framework myColor = MultiplayerColorAssignment.TryGetLocalClaimedColor(out Framework claimed)
                ? claimed
                : MultiplayerColorAssignment.ClaimForLocalPlayer();

            string prefabName = myColor == Framework.BLACK ? blackPrefabName : whitePrefabName;
            Vector2 spawnPos = GetCachedOrPickSpawnPosition(myColor, forceNewPick: true);

            s_spawnInProgress = true;
            try
            {
                PhotonNetwork.Instantiate(prefabName, spawnPos, Quaternion.identity);
                spawned = true;
                EnsureSpawnPlatformPainted(myColor, spawnPos);
                Debug.Log($"[Multiplayer] Spawned local '{prefabName}' as {myColor} at {spawnPos}.");
            }
            finally
            {
                s_spawnInProgress = false;
            }
        }

        private static bool TryGetHostColor(out Framework framework)
        {
            framework = Framework.BLACK;
            var props = PhotonNetwork.CurrentRoom?.CustomProperties;
            if (props == null || !props.TryGetValue(HostColorProperty, out object raw)) return false;
            if (!(raw is int frameworkInt)) return false;
            framework = (Framework)frameworkInt;
            return true;
        }
    }
}
