using ExitGames.Client.Photon;
using Game;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

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
