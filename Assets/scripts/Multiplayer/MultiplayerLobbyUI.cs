using Game;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Multiplayer
{
    // Lobby waiting room: host creates a room and shares link; guest joins by code.
    // Colors are assigned automatically (host vs joiner).
    public class MultiplayerLobbyUI : MonoBehaviourPunCallbacks
    {
        private const string CopyLabel = "Copy";
        private const string CopiedLabel = "Copied!";

        private MultiplayerBootstrap bootstrap;
        private Font uiFont;

        private GameObject rootPanel;
        private Text statusText;
        private GameObject hostPanel;
        private GameObject guestPanel;
        private GameObject sharePanel;
        private Text urlText;
        private Text copyButtonText;
        private InputField roomInput;
        private string currentShareUrl;

        private void Awake()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>();
            if (bootstrap == null)
                bootstrap = FindObjectOfType<MultiplayerBootstrap>();

            uiFont = GetUIFont();
            BuildUI();
            RefreshUI();
        }

        private void Update()
        {
            if (rootPanel != null && rootPanel.activeSelf)
                RefreshUI();
        }

        public override void OnConnectedToMaster()
        {
            RefreshUI();
        }

        public override void OnJoinedRoom()
        {
            RefreshUI();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            RefreshUI();
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            RefreshUI();
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            SetStatus($"Join failed: {message}");
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            SetStatus($"Could not create room: {message}");
        }

        private void RefreshUI()
        {
            if (rootPanel == null) return;

            bool inLobbyScene = SceneManager.GetActiveScene().name == MultiplayerSceneNames.LobbySceneName;
            if (!inLobbyScene)
            {
                rootPanel.SetActive(false);
                return;
            }

            rootPanel.SetActive(true);

            bool connected = PhotonNetwork.IsConnected;
            bool inRoom = PhotonNetwork.InRoom;
            bool roomFull = inRoom && PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers;

            if (roomFull)
            {
                SetStatus("Starting game...");
                hostPanel.SetActive(false);
                guestPanel.SetActive(false);
                sharePanel.SetActive(false);
                return;
            }

            if (!connected)
            {
                SetStatus("Connecting to Photon...");
                hostPanel.SetActive(false);
                guestPanel.SetActive(false);
                sharePanel.SetActive(false);
                return;
            }

            if (inRoom)
            {
                int count = PhotonNetwork.CurrentRoom.PlayerCount;
                int max = PhotonNetwork.CurrentRoom.MaxPlayers;
                bool isMaster = PhotonNetwork.IsMasterClient;
                string colorLine = MultiplayerColorAssignment.TryGetLocalAssignedColor(out Framework assigned)
                    ? $"You are playing as {MultiplayerColorAssignment.ColorLabel(assigned)}."
                    : "Assigning your color...";

                hostPanel.SetActive(false);
                guestPanel.SetActive(false);

                if (isMaster)
                {
                    currentShareUrl = MultiplayerBootstrap.BuildShareUrl(PhotonNetwork.CurrentRoom.Name);
                    urlText.text = currentShareUrl;
                    copyButtonText.text = CopyLabel;
                    sharePanel.SetActive(true);
                    SetStatus($"{colorLine} Waiting for opponent ({count}/{max}) — share the link below");
                }
                else
                {
                    sharePanel.SetActive(false);
                    SetStatus($"{colorLine} Waiting for match to start ({count}/{max})");
                }
                return;
            }

            sharePanel.SetActive(false);
            bool autoJoinPending = bootstrap != null && bootstrap.HasQueuedJoin;
            if (autoJoinPending)
            {
                SetStatus("Joining room...");
                hostPanel.SetActive(false);
                guestPanel.SetActive(false);
                return;
            }

            SetStatus("Create a room to host, or join with a room code");
            hostPanel.SetActive(true);
            guestPanel.SetActive(true);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private void OnCreateRoom() => bootstrap?.CreateRoom();

        private void OnJoinClicked()
        {
            if (roomInput == null || bootstrap == null) return;
            bootstrap.JoinRoomCode(roomInput.text);
            RefreshUI();
        }

        private void OnCopyClicked()
        {
            if (string.IsNullOrEmpty(currentShareUrl)) return;
            GUIUtility.systemCopyBuffer = currentShareUrl;
            CancelInvoke(nameof(ResetCopyLabel));
            copyButtonText.text = CopiedLabel;
            Invoke(nameof(ResetCopyLabel), 1.5f);
            Debug.Log($"[Multiplayer] Copied to clipboard: {currentShareUrl}");
        }

        private void ResetCopyLabel()
        {
            if (copyButtonText != null) copyButtonText.text = CopyLabel;
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("LobbyCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            rootPanel = CreateCenteredPanel(canvasGo.transform, "LobbyPanel",
                new Vector2(680, 520), new Color(0.12f, 0.14f, 0.18f, 0.95f));

            statusText = CreateStretchText(rootPanel.transform, "Status", "Connecting...",
                new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.98f), 22, TextAnchor.MiddleCenter);

            hostPanel = CreateStretchPanel(rootPanel.transform, "HostPanel",
                new Vector2(0.06f, 0.52f), new Vector2(0.94f, 0.84f), new Color(0.2f, 0.22f, 0.28f, 1f));
            CreateStretchText(hostPanel.transform, "HostLabel", "Host a game",
                new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.96f), 22, TextAnchor.MiddleCenter);
            CreateStretchButton(hostPanel.transform, "CreateRoom", "Create room",
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.48f),
                new Color(0.25f, 0.55f, 0.95f, 1f), OnCreateRoom);

            guestPanel = CreateStretchPanel(rootPanel.transform, "GuestPanel",
                new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.48f), new Color(0.2f, 0.22f, 0.28f, 1f));
            CreateStretchText(guestPanel.transform, "GuestLabel", "Join a game",
                new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.96f), 22, TextAnchor.MiddleCenter);
            roomInput = CreateStretchInputField(guestPanel.transform, "RoomInput",
                new Vector2(0.04f, 0.08f), new Vector2(0.68f, 0.68f));
            var joinBtn = CreateStretchButton(guestPanel.transform, "JoinButton", "Join",
                new Vector2(0.72f, 0.08f), new Vector2(0.96f, 0.68f),
                new Color(0.25f, 0.55f, 0.95f, 1f), OnJoinClicked);

            sharePanel = CreateStretchPanel(rootPanel.transform, "SharePanel",
                new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.18f), new Color(0.96f, 0.96f, 0.96f, 1f));
            CreateStretchText(sharePanel.transform, "ShareLabel", "Share:",
                new Vector2(0.04f, 0.1f), new Vector2(0.14f, 0.9f), 20, TextAnchor.MiddleLeft,
                new Color(0.2f, 0.2f, 0.2f, 1f));
            urlText = CreateStretchText(sharePanel.transform, "UrlText", "",
                new Vector2(0.16f, 0.1f), new Vector2(0.72f, 0.9f), 18, TextAnchor.MiddleLeft,
                new Color(0.1f, 0.1f, 0.1f, 1f));
            urlText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var copyBtn = CreateStretchButton(sharePanel.transform, "CopyButton", CopyLabel,
                new Vector2(0.74f, 0.1f), new Vector2(0.96f, 0.9f),
                new Color(0.25f, 0.55f, 0.95f, 1f), OnCopyClicked);
            copyButtonText = copyBtn.GetComponentInChildren<Text>();

            hostPanel.SetActive(false);
            guestPanel.SetActive(false);
            sharePanel.SetActive(false);
        }

        private static GameObject CreateCenteredPanel(Transform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return go;
        }

        private static GameObject CreateStretchPanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            ApplyStretch(rect: go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            return go;
        }

        private static Text CreateStretchText(Transform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax, int fontSize, TextAnchor align, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = GetUIFont();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = color ?? Color.white;
            text.resizeTextForBestFit = false;
            ApplyStretch(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            return text;
        }

        private static Button CreateStretchButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bgColor,
            UnityEngine.Events.UnityAction onClick, Color? textColor = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bgColor;
            go.GetComponent<Button>().onClick.AddListener(onClick);
            ApplyStretch(go.GetComponent<RectTransform>(), anchorMin, anchorMax);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = GetUIFont();
            text.text = label;
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = textColor ?? Color.white;
            ApplyStretch(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            return go.GetComponent<Button>();
        }

        private static InputField CreateStretchInputField(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Color.white;
            ApplyStretch(go.GetComponent<RectTransform>(), anchorMin, anchorMax);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = GetUIFont();
            text.fontSize = 20;
            text.color = Color.black;
            text.supportRichText = false;
            ApplyStretch(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(12, 8), new Vector2(-12, -8));

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGo.transform.SetParent(go.transform, false);
            var placeholder = placeholderGo.GetComponent<Text>();
            placeholder.font = GetUIFont();
            placeholder.text = "Room code or paste link";
            placeholder.fontSize = 18;
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            placeholder.fontStyle = FontStyle.Italic;
            ApplyStretch(placeholderGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(12, 8), new Vector2(-12, -8));

            var input = go.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private static void ApplyStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = offsetMin ?? Vector2.zero;
            rect.offsetMax = offsetMax ?? Vector2.zero;
        }

        private static Font cachedFont;

        private static Font GetUIFont()
        {
            if (cachedFont != null) return cachedFont;
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (cachedFont == null) cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return cachedFont;
        }
    }
}
