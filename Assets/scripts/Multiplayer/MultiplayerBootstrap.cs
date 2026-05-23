using System;
using ExitGames.Client.Photon;
using Game;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Multiplayer
{
    // Connects to Photon from the lobby scene. Host creates a room; guest joins by
    // code (or ?room= URL / editor override). Colors are assigned automatically.
    // When the room is full, the master loads the game scene for both peers.
    public class MultiplayerBootstrap : MonoBehaviourPunCallbacks
    {
        public const string RoomParam = "room";

        [Header("Editor-only join override")]
        [Tooltip("Optional: auto-join this room on Play (full URL or room code). Leave empty to use lobby UI.")]
        [SerializeField] private string editorRoomOverride = "";

        [Header("Room setup")]
        [Tooltip("Color assigned to the host (master) when they create a room. Joiners always get the opposite.")]
        [SerializeField] private Framework roomMasterColor = Framework.BLACK;

        [Header("Game scene")]
        [SerializeField] private string gameSceneName = MultiplayerSceneNames.GameSceneName;

        private string pendingRoomToJoin;
        private string joinSource;
        private bool pendingCreateRoom;

        public bool IsConnectedToMaster => PhotonNetwork.IsConnected;
        public bool IsInRoom => PhotonNetwork.InRoom;
        public bool HasQueuedJoin => !string.IsNullOrEmpty(pendingRoomToJoin);

        private void Start()
        {
            if (!IsLobbyScene()) return;

            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.GameVersion = Application.version;

            pendingRoomToJoin = ReadRoomFromUrl(Application.absoluteURL);
            joinSource = string.IsNullOrEmpty(pendingRoomToJoin) ? null : "URL";
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(pendingRoomToJoin) && !string.IsNullOrEmpty(editorRoomOverride))
            {
                pendingRoomToJoin = ParseRoomCode(editorRoomOverride);
                joinSource = "editor override";
                Debug.Log($"[Multiplayer] Editor override: will join room '{pendingRoomToJoin}'.");
            }
#endif

            if (PhotonNetwork.IsConnected)
            {
                OnConnectedToMaster();
                return;
            }

            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("[Multiplayer] Connecting to Photon...");
        }

        public void CreateRoom()
        {
            if (!IsLobbyScene()) return;
            pendingCreateRoom = true;
            pendingRoomToJoin = null;
            TryCreateRoom();
        }

        public void JoinRoomCode(string input)
        {
            if (!IsLobbyScene()) return;
            string code = ParseRoomCode(input);
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("[Multiplayer] Join failed: no room code in input.");
                return;
            }

            pendingRoomToJoin = code;
            pendingCreateRoom = false;
            joinSource = "lobby UI";
            TryJoinRoom();
        }

        public static string BuildShareUrl(string roomName)
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url))
            {
                return $"(editor) ?{RoomParam}={roomName}";
            }
            int q = url.IndexOf('?');
            string baseUrl = q >= 0 ? url.Substring(0, q) : url;
            return $"{baseUrl}?{RoomParam}={Uri.EscapeDataString(roomName)}";
        }

        public static string ParseRoomCode(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            string trimmed = input.Trim();
            string fromUrl = ReadRoomFromUrl(trimmed);
            return string.IsNullOrEmpty(fromUrl) ? trimmed : fromUrl;
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log($"[Multiplayer] Connected to master ({PhotonNetwork.CloudRegion}).");

            if (PhotonNetwork.InRoom) return;

            if (!string.IsNullOrEmpty(pendingRoomToJoin))
            {
                TryJoinRoom();
                return;
            }

            if (pendingCreateRoom)
            {
                TryCreateRoom();
            }
        }

        public override void OnJoinedRoom()
        {
            string room = PhotonNetwork.CurrentRoom.Name;
            int count = PhotonNetwork.CurrentRoom.PlayerCount;
            int max = PhotonNetwork.CurrentRoom.MaxPlayers;
            Debug.Log($"[Multiplayer] Joined room '{room}' ({count}/{max} players).");

            MultiplayerColorAssignment.ClaimForLocalPlayer();

            if (PhotonNetwork.IsMasterClient)
            {
                Debug.Log($"[Multiplayer] Share: {BuildShareUrl(room)}");
            }

            TryLoadGameScene();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log($"[Multiplayer] Player joined: actor #{newPlayer.ActorNumber}. Room now {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}.");
            TryLoadGameScene();
        }

        private void TryJoinRoom()
        {
            if (!PhotonNetwork.IsConnectedAndReady || string.IsNullOrEmpty(pendingRoomToJoin)) return;
            if (PhotonNetwork.InRoom) return;

            Debug.Log($"[Multiplayer] Joining room '{pendingRoomToJoin}' (from {joinSource}).");
            PhotonNetwork.JoinRoom(pendingRoomToJoin);
        }

        private void TryCreateRoom()
        {
            if (!PhotonNetwork.IsConnectedAndReady || !pendingCreateRoom) return;
            if (PhotonNetwork.InRoom) return;

            Framework color = roomMasterColor;
            string roomName = GenerateRoomCode();
            var options = new RoomOptions
            {
                MaxPlayers = 2,
                IsVisible = false,
                IsOpen = true,
                CustomRoomProperties = new Hashtable
                {
                    { MultiplayerSpawner.HostColorProperty, (int)color }
                },
                CustomRoomPropertiesForLobby = new[] { MultiplayerSpawner.HostColorProperty }
            };
            Debug.Log($"[Multiplayer] Hosting room '{roomName}' as {color}.");
            PhotonNetwork.CreateRoom(roomName, options);
        }

        private void TryLoadGameScene()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (string.IsNullOrEmpty(gameSceneName)) return;
            if (PhotonNetwork.CurrentRoom == null) return;
            if (PhotonNetwork.CurrentRoom.PlayerCount < PhotonNetwork.CurrentRoom.MaxPlayers) return;
            if (SceneManager.GetActiveScene().name == gameSceneName) return;

            Debug.Log($"[Multiplayer] Room full. Loading game scene '{gameSceneName}'.");
            PhotonNetwork.LoadLevel(gameSceneName);
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

        private static bool IsLobbyScene()
        {
            return SceneManager.GetActiveScene().name == MultiplayerSceneNames.LobbySceneName;
        }

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

        private static string GenerateRoomCode()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        }
    }
}
