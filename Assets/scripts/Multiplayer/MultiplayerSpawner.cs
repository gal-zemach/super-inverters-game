using ExitGames.Client.Photon;
using Game;
using Photon.Pun;
using UnityEngine;

namespace Multiplayer
{
    // Slice 2: spawns one Player per peer with the right Framework.
    //
    // Reads the `hostColor` custom room property (set by MultiplayerBootstrap
    // when the host creates the room). The master client takes hostColor; the
    // joiner takes the opposite. Each peer calls PhotonNetwork.Instantiate
    // for its own player using the matching prefab name; the prefabs already
    // have player_framework baked into their PlayerState component.
    public class MultiplayerSpawner : MonoBehaviourPunCallbacks
    {
        public const string HostColorProperty = "hostColor";

        [Tooltip("Prefab name (under any Resources/ folder) for the BLACK player. Default 'BlackPlayer' resolves to Assets/Resources/BlackPlayer.prefab.")]
        [SerializeField] private string blackPrefabName = "BlackPlayer";

        [Tooltip("Prefab name (under any Resources/ folder) for the WHITE player. Default 'WhitePlayer' resolves to Assets/Resources/WhitePlayer.prefab.")]
        [SerializeField] private string whitePrefabName = "WhitePlayer";

        [Tooltip("World position used when spawning the BLACK player.")]
        [SerializeField] private Vector2 blackSpawnPosition = new Vector2(-3f, 1f);

        [Tooltip("World position used when spawning the WHITE player.")]
        [SerializeField] private Vector2 whiteSpawnPosition = new Vector2(3f, 1f);

        private bool spawned;

        public override void OnJoinedRoom()
        {
            TrySpawn();
        }

        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(HostColorProperty))
            {
                TrySpawn();
            }
        }

        private void TrySpawn()
        {
            if (spawned) return;

            if (!TryGetHostColor(out Framework hostColor))
            {
                Debug.Log("[Multiplayer] Spawner waiting: hostColor room property not set yet.");
                return;
            }

            Framework myColor = PhotonNetwork.IsMasterClient ? hostColor : Opposite(hostColor);
            string prefabName = myColor == Framework.BLACK ? blackPrefabName : whitePrefabName;
            Vector2 spawnPos = myColor == Framework.BLACK ? blackSpawnPosition : whiteSpawnPosition;

            PhotonNetwork.Instantiate(prefabName, spawnPos, Quaternion.identity);
            spawned = true;
            Debug.Log($"[Multiplayer] Spawned local '{prefabName}' as {myColor} at {spawnPos}.");
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
