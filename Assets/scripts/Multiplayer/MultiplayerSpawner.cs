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
        public const string HostColorProperty = "hostColor";
        public const string MyFrameworkProperty = "myFramework";

        [Tooltip("Prefab name (under any Resources/ folder) for the BLACK player. Default 'BlackPlayer' resolves to Assets/Resources/BlackPlayer.prefab.")]
        [SerializeField] private string blackPrefabName = "BlackPlayer";

        [Tooltip("Prefab name (under any Resources/ folder) for the WHITE player. Default 'WhitePlayer' resolves to Assets/Resources/WhitePlayer.prefab.")]
        [SerializeField] private string whitePrefabName = "WhitePlayer";

        [Tooltip("World position used when spawning the BLACK player.")]
        [SerializeField] private Vector2 blackSpawnPosition = new Vector2(-3f, 1f);

        [Tooltip("World position used when spawning the WHITE player.")]
        [SerializeField] private Vector2 whiteSpawnPosition = new Vector2(3f, 1f);

        [Tooltip("Only spawn when this scene is active. Prevents the lobby Bootstrap (which is also a copy of this) from spawning a player that PhotonNetwork.LoadLevel would immediately destroy.")]
        [SerializeField] private string targetSceneName = "level_1-multiplayer";

        private bool spawned;

        private void Start()
        {
            // Post-LoadLevel case: we're now in the game scene, already in a
            // room. OnJoinedRoom won't fire here, so kick off the spawn from
            // Start instead.
            if (PhotonNetwork.InRoom && IsInTargetScene())
            {
                TrySpawn();
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

        // Editor-only visual feedback so you can see where players will spawn
        // without running the game. Compiled out of player builds automatically.
        private void OnDrawGizmos()
        {
            DrawSpawnGizmo(blackSpawnPosition, Color.black, "BLACK SPAWN");
            DrawSpawnGizmo(whiteSpawnPosition, Color.white, "WHITE SPAWN");
        }

        private static void DrawSpawnGizmo(Vector2 position, Color fill, string label)
        {
            Vector3 p = new Vector3(position.x, position.y, 0f);

            // Solid colored sphere at the spawn point.
            Gizmos.color = fill;
            Gizmos.DrawSphere(p, 0.5f);

            // Yellow wire ring around it for contrast against any background.
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(p, 0.6f);

            // Vertical drop line indicating "player falls here onto whatever
            // is below" — helps when you're aligning to a platform.
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(p, p + Vector3.down * 2f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.Label(p + new Vector3(0.7f, 0.5f, 0f), label);
#endif
        }

        private void TrySpawn()
        {
            if (spawned) return;

            if (!TryGetHostColor(out Framework hostColor))
            {
                Debug.Log("[Multiplayer] Spawner waiting: hostColor room property not set yet.");
                return;
            }

            Framework myColor = PickMyColor(hostColor);

            // Claim it before spawning so the next peer to join sees it.
            var props = new Hashtable { { MyFrameworkProperty, (int)myColor } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            string prefabName = myColor == Framework.BLACK ? blackPrefabName : whitePrefabName;
            Vector2 spawnPos = myColor == Framework.BLACK ? blackSpawnPosition : whiteSpawnPosition;

            PhotonNetwork.Instantiate(prefabName, spawnPos, Quaternion.identity);
            spawned = true;
            Debug.Log($"[Multiplayer] Spawned local '{prefabName}' as {myColor} at {spawnPos}.");
        }

        private static Framework PickMyColor(Framework hostColor)
        {
            // Scan other players for a claimed framework. If anyone has claimed,
            // take whichever color isn't taken — this is what makes rejoin work.
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (player.IsLocal) continue;
                if (player.CustomProperties.TryGetValue(MyFrameworkProperty, out object raw) && raw is int frameworkInt)
                {
                    return Opposite((Framework)frameworkInt);
                }
            }

            // No one has claimed — fall back to the hostColor heuristic.
            return PhotonNetwork.IsMasterClient ? hostColor : Opposite(hostColor);
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

        private static Framework Opposite(Framework f)
        {
            return f == Framework.BLACK ? Framework.WHITE : Framework.BLACK;
        }
    }
}
