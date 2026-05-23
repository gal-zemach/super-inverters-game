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
        private bool resumeJoinAfterLeave;
        private bool resumeCreateAfterLeave;
        private int joinRetryCount;
        private Coroutine joinRetryCoroutine;

        /// <summary>Room name we closed as host before LeaveRoom; cleared after OnLeftRoom confirms.</summary>
        private string _hostedRoomPendingCloseConfirm;

        /// <summary>Last join/create error for lobby UI (cleared on success).</summary>
        public string LastLobbyError { get; private set; }

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

        /// <summary>Leave the current Photon room and return to the create/join lobby UI.</summary>
        public void LeaveCurrentRoom()
        {
            pendingCreateRoom = false;
            pendingRoomToJoin = null;
            joinSource = null;
            joinRetryCount = 0;
            LastLobbyError = null;
            if (joinRetryCoroutine != null)
            {
                StopCoroutine(joinRetryCoroutine);
                joinRetryCoroutine = null;
            }

            if (PhotonNetwork.InRoom)
            {
                CloseHostedRoomIfMaster();
                PhotonNetwork.LeaveRoom();
            }
        }

        /// <summary>Disconnect from Photon and load the main menu scene.</summary>
        public void ReturnToMainMenu()
        {
            if (PhotonNetwork.InRoom)
            {
                CloseHostedRoomIfMaster();
                PhotonNetwork.LeaveRoom();
            }

            if (PhotonNetwork.IsConnected)
                PhotonNetwork.Disconnect();

            SceneManager.LoadScene("main_menu");
        }

        /// <summary>Master closes the room so no one can join, then leave removes it (EmptyRoomTtl=0).</summary>
        private void CloseHostedRoomIfMaster()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

            var room = PhotonNetwork.CurrentRoom;
            if (room == null) return;

            _hostedRoomPendingCloseConfirm = room.Name;
            room.IsOpen = false;
            room.IsVisible = false;
            Debug.Log(
                $"[Multiplayer] RoomClose: closing hosted room '{room.Name}' before leave " +
                $"(IsOpen=false, IsVisible=false, EmptyRoomTtl=0 on create).");
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
            Debug.Log($"[Multiplayer] Connected to master (region: {PhotonNetwork.CloudRegion}).");
            LastLobbyError = null;
            joinRetryCount = 0;

            if (PhotonNetwork.InRoom)
            {
                // Stale room from a prior session, or need to switch rooms — handled below.
                if (!string.IsNullOrEmpty(pendingRoomToJoin))
                    TryJoinRoom();
                else if (pendingCreateRoom)
                    TryCreateRoom();
                return;
            }

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
            LastLobbyError = null;
            joinRetryCount = 0;
            if (joinRetryCoroutine != null)
            {
                StopCoroutine(joinRetryCoroutine);
                joinRetryCoroutine = null;
            }

            string room = PhotonNetwork.CurrentRoom.Name;
            int count = PhotonNetwork.CurrentRoom.PlayerCount;
            int max = PhotonNetwork.CurrentRoom.MaxPlayers;
            Debug.Log($"[Multiplayer] Joined room '{room}' ({count}/{max} players).");

            MultiplayerColorAssignment.ClearLocalColorClaim();
            MultiplayerColorAssignment.ClaimForLocalPlayer();

            if (PhotonNetwork.IsMasterClient)
            {
                Debug.Log($"[Multiplayer] Share: {BuildShareUrl(room)}");
            }

            TryLoadGameScene();
        }

        public override void OnLeftRoom()
        {
            MultiplayerColorAssignment.ClearLocalColorClaim();

            if (!string.IsNullOrEmpty(_hostedRoomPendingCloseConfirm))
            {
                string closedRoom = _hostedRoomPendingCloseConfirm;
                _hostedRoomPendingCloseConfirm = null;
                Debug.Log(
                    $"[Multiplayer] RoomCloseConfirm: host backed out of '{closedRoom}'. " +
                    $"Left Photon room successfully (InRoom={PhotonNetwork.InRoom}). " +
                    $"Server should delete the room immediately (EmptyRoomTtl=0). " +
                    $"A join attempt with code '{closedRoom}' should fail with GameDoesNotExist.");
            }

            if (!IsLobbyScene()) return;

            if (resumeJoinAfterLeave)
            {
                resumeJoinAfterLeave = false;
                TryJoinRoom();
                return;
            }

            if (resumeCreateAfterLeave)
            {
                resumeCreateAfterLeave = false;
                TryCreateRoom();
            }
        }

        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(MultiplayerSpawner.HostColorProperty))
                MultiplayerColorAssignment.ClaimForLocalPlayer();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log($"[Multiplayer] Player joined: actor #{newPlayer.ActorNumber}. Room now {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}.");
            TryLoadGameScene();
        }

        private void TryJoinRoom()
        {
            if (string.IsNullOrEmpty(pendingRoomToJoin)) return;

            if (!PhotonNetwork.IsConnectedAndReady)
            {
                Debug.Log("[Multiplayer] Join queued — waiting for Photon connection.");
                return;
            }

            if (PhotonNetwork.InRoom)
            {
                string current = PhotonNetwork.CurrentRoom?.Name;
                if (string.Equals(current, pendingRoomToJoin, StringComparison.OrdinalIgnoreCase))
                    return;

                Debug.Log($"[Multiplayer] In room '{current}' — leaving to join '{pendingRoomToJoin}'.");
                resumeJoinAfterLeave = true;
                CloseHostedRoomIfMaster();
                PhotonNetwork.LeaveRoom();
                return;
            }

            Debug.Log($"[Multiplayer] Joining room '{pendingRoomToJoin}' (from {joinSource}, region {PhotonNetwork.CloudRegion}).");
            if (!PhotonNetwork.JoinRoom(pendingRoomToJoin))
            {
                LastLobbyError = "Could not send join request — not ready yet. Retrying…";
                Debug.LogWarning("[Multiplayer] JoinRoom rejected by client (not ready). Will retry on connect.");
            }
        }

        private void TryCreateRoom()
        {
            if (!pendingCreateRoom) return;

            if (!PhotonNetwork.IsConnectedAndReady)
            {
                Debug.Log("[Multiplayer] Create queued — waiting for Photon connection.");
                return;
            }

            if (PhotonNetwork.InRoom)
            {
                if (PhotonNetwork.IsMasterClient)
                    return;

                Debug.Log("[Multiplayer] In someone else's room — leaving before hosting.");
                resumeCreateAfterLeave = true;
                CloseHostedRoomIfMaster();
                PhotonNetwork.LeaveRoom();
                return;
            }

            Framework color = roomMasterColor;
            string roomName = GenerateRoomCode();
            var options = new RoomOptions
            {
                MaxPlayers = 2,
                IsVisible = false,
                IsOpen = true,
                EmptyRoomTtl = 0,
                PlayerTtl = 0,
                CustomRoomProperties = new Hashtable
                {
                    { MultiplayerSpawner.HostColorProperty, (int)color }
                },
                CustomRoomPropertiesForLobby = new[] { MultiplayerSpawner.HostColorProperty }
            };
            Debug.Log($"[Multiplayer] Hosting room '{roomName}' as {color} (region {PhotonNetwork.CloudRegion}).");
            if (!PhotonNetwork.CreateRoom(roomName, options))
            {
                LastLobbyError = "Could not send create request — not ready yet. Retrying…";
                Debug.LogWarning("[Multiplayer] CreateRoom rejected by client (not ready). Will retry on connect.");
            }
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
            LastLobbyError = $"Join failed ({returnCode}): {message}";
            Debug.LogWarning($"[Multiplayer] {LastLobbyError} (region {PhotonNetwork.CloudRegion})");

            // 32758 = GameDoesNotExist — host may still be registering the room.
            const short gameDoesNotExist = 32758;
            if (returnCode == gameDoesNotExist && !string.IsNullOrEmpty(pendingRoomToJoin) && joinRetryCount < 8)
            {
                joinRetryCount++;
                if (joinRetryCoroutine != null)
                    StopCoroutine(joinRetryCoroutine);
                joinRetryCoroutine = StartCoroutine(RetryJoinAfterDelay(1.5f));
            }
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            LastLobbyError = $"Create failed ({returnCode}): {message}";
            Debug.LogError($"[Multiplayer] {LastLobbyError} (region {PhotonNetwork.CloudRegion})");
            pendingCreateRoom = false;
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            LastLobbyError = $"Disconnected: {cause}";
            Debug.LogWarning($"[Multiplayer] {LastLobbyError}");
        }

        private System.Collections.IEnumerator RetryJoinAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            joinRetryCoroutine = null;
            if (!IsLobbyScene() || string.IsNullOrEmpty(pendingRoomToJoin)) yield break;
            Debug.Log($"[Multiplayer] Retrying join '{pendingRoomToJoin}' (attempt {joinRetryCount}).");
            TryJoinRoom();
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
