using System;
using ExitGames.Client.Photon;
using Game;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Multiplayer
{
    // Slice 1 + 2 of the multiplayer integration plan. See AGENT_CONTEXT.md.
    //
    // Drop this on a single GameObject in a scene. On Start it:
    //   - connects to Photon using PhotonServerSettings
    //   - on the host (no ?room=XYZ in the URL) creates a fresh room and logs
    //     the share URL; the room is tagged with hostColor as a custom property
    //   - on the joiner (?room=XYZ present) joins that room
    //
    // MultiplayerSpawner reads hostColor and instantiates the Player prefab
    // for each peer with the correct Framework.
    public class MultiplayerBootstrap : MonoBehaviourPunCallbacks
    {
        private const string RoomParam = "room";

        [Header("Editor-only join override")]
        [Tooltip("In the Editor, set this on the joiner instance to the room code logged by the host. Leave empty to host. Ignored in WebGL builds (URL ?room= takes over there).")]
        [SerializeField] private string editorRoomOverride = "";

        [Header("Host color choice")]
        [Tooltip("Color the host takes when creating the room. The joiner takes the opposite. Ignored on the joiner side.")]
        [SerializeField] private Framework hostColor = Framework.BLACK;

        private string pendingRoomToJoin;
        private string joinSource;

        private void Start()
        {
            if (PhotonNetwork.IsConnected)
            {
                OnConnectedToMaster();
                return;
            }

            pendingRoomToJoin = ReadRoomFromUrl(Application.absoluteURL);
            joinSource = string.IsNullOrEmpty(pendingRoomToJoin) ? null : "URL";
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(pendingRoomToJoin) && !string.IsNullOrEmpty(editorRoomOverride))
            {
                string trimmed = editorRoomOverride.Trim();
                // Be tolerant of the user pasting the full share string (e.g.
                // "(editor) ?room=52F4AD" or a real WebGL URL) rather than
                // just the room code. Fall back to the raw input otherwise.
                pendingRoomToJoin = ReadRoomFromUrl(trimmed) ?? trimmed;
                joinSource = "editor override";
                Debug.Log($"[Multiplayer] Editor override: will join room '{pendingRoomToJoin}'.");
            }
#endif
            PhotonNetwork.GameVersion = Application.version;
            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("[Multiplayer] Connecting to Photon...");
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log($"[Multiplayer] Connected to master ({PhotonNetwork.CloudRegion}).");

            if (!string.IsNullOrEmpty(pendingRoomToJoin))
            {
                Debug.Log($"[Multiplayer] Joining room '{pendingRoomToJoin}' (from {joinSource}).");
                PhotonNetwork.JoinRoom(pendingRoomToJoin);
                return;
            }

            string roomName = GenerateRoomCode();
            var options = new RoomOptions
            {
                MaxPlayers = 2,
                IsVisible = false,
                IsOpen = true,
                CustomRoomProperties = new Hashtable
                {
                    { MultiplayerSpawner.HostColorProperty, (int)hostColor }
                },
                CustomRoomPropertiesForLobby = new[] { MultiplayerSpawner.HostColorProperty }
            };
            Debug.Log($"[Multiplayer] Hosting new room '{roomName}' as {hostColor}.");
            PhotonNetwork.CreateRoom(roomName, options);
        }

        public override void OnJoinedRoom()
        {
            string room = PhotonNetwork.CurrentRoom.Name;
            int actor = PhotonNetwork.LocalPlayer.ActorNumber;
            int count = PhotonNetwork.CurrentRoom.PlayerCount;
            int max = PhotonNetwork.CurrentRoom.MaxPlayers;
            Debug.Log($"[Multiplayer] Joined room '{room}' as actor #{actor} ({count}/{max} players).");

            if (PhotonNetwork.IsMasterClient)
            {
                Debug.Log($"[Multiplayer] Share this URL with a friend: {BuildShareUrl(room)}");
            }
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log($"[Multiplayer] Player joined: actor #{newPlayer.ActorNumber}. Room now {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}.");
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            Debug.Log($"[Multiplayer] Player left: actor #{otherPlayer.ActorNumber}.");
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[Multiplayer] Join failed (code {returnCode}): {message}.");
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"[Multiplayer] Create failed (code {returnCode}): {message}.");
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning($"[Multiplayer] Disconnected: {cause}.");
        }

        // Pulls `room` out of the WebGL page URL's query string. Returns null in
        // the Editor (where Application.absoluteURL is empty) or when missing.
        private static string ReadRoomFromUrl(string absoluteUrl)
        {
            if (string.IsNullOrEmpty(absoluteUrl)) return null;
            int q = absoluteUrl.IndexOf('?');
            if (q < 0 || q == absoluteUrl.Length - 1) return null;

            string query = absoluteUrl.Substring(q + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);

            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string key = pair.Substring(0, eq);
                if (!string.Equals(key, RoomParam, StringComparison.OrdinalIgnoreCase)) continue;
                string value = pair.Substring(eq + 1);
                return string.IsNullOrEmpty(value) ? null : Uri.UnescapeDataString(value);
            }
            return null;
        }

        private static string BuildShareUrl(string roomName)
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url))
            {
                return $"(editor — no URL) ?{RoomParam}={roomName}";
            }
            int q = url.IndexOf('?');
            string baseUrl = q >= 0 ? url.Substring(0, q) : url;
            return $"{baseUrl}?{RoomParam}={Uri.EscapeDataString(roomName)}";
        }

        private static string GenerateRoomCode()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        }
    }
}
