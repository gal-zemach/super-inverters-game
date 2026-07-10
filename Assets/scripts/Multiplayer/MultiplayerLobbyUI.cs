using Game;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utils;

namespace Multiplayer
{
    // Lobby waiting room: host creates a room and shares link; guest joins by code.
    // Colors are assigned automatically (host vs joiner).
    public class MultiplayerLobbyUI : MonoBehaviourPunCallbacks
    {
        private const string CopyLabel = "Copy";
        private const string CopiedLabel = "Copied!";
        private const string CreateRoomLabel = "Create room";

        // Gray palette — light neutrals only.
        private static readonly Color PanelBg = new Color(0.88f, 0.88f, 0.88f, 0.98f);
        private static readonly Color SectionBg = new Color(0.78f, 0.78f, 0.78f, 1f);
        private static readonly Color ButtonBg = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color ButtonBgMuted = new Color(0.68f, 0.68f, 0.68f, 1f);
        private static readonly Color ShareBg = new Color(0.82f, 0.82f, 0.82f, 1f);
        private static readonly Color TextDark = new Color(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color TextLight = new Color(0.96f, 0.96f, 0.96f, 1f);
        private static readonly Color InputBg = new Color(0.94f, 0.94f, 0.94f, 1f);
        private static readonly Color Placeholder = new Color(0.45f, 0.45f, 0.45f, 1f);

        private MultiplayerBootstrap bootstrap;
        private Font uiFont;

        private GameObject rootPanel;
        private RectTransform rootPanelRect;
        private GameObject backButton;
        private Text statusText;
        private GameObject hostPanel;
        private GameObject guestPanel;
        private GameObject sharePanel;
        private Text urlText;
        private Text copyButtonText;
        private InputField roomInput;
        private string currentShareUrl;
        private Button createRoomButton;
        private Text createRoomText;
        private bool creatingRoom;

        private enum LobbyLayout { Browse, RoomHost, RoomGuest, Minimal }
        private LobbyLayout currentLayout = (LobbyLayout)(-1);

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
            ResetCreateRoomButton();
            RefreshUI();
        }

        public override void OnLeftRoom()
        {
            ResetCreateRoomButton();
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
            ResetCreateRoomButton();
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

            // Click feedback for the slow room-creation round-trip: the button locks and
            // its label ticks "Creating." → ".." → "..." until Photon answers.
            if (creatingRoom && createRoomText != null)
                createRoomText.text = "Creating" + new string('.', 1 + (int)(Time.unscaledTime * 3f) % 3);

            bool connected = PhotonNetwork.IsConnected;
            bool inRoom = PhotonNetwork.InRoom;
            bool roomFull = inRoom && PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers;
            bool autoJoinPending = bootstrap != null && bootstrap.HasQueuedJoin;

            if (backButton != null)
                backButton.SetActive(!roomFull);

            if (roomFull)
            {
                EnsureLayout(LobbyLayout.Minimal);
                SetStatus("Starting game...");
                hostPanel.SetActive(false);
                guestPanel.SetActive(false);
                sharePanel.SetActive(false);
                return;
            }

            if (!connected)
            {
                EnsureLayout(LobbyLayout.Minimal);
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
                    EnsureLayout(LobbyLayout.RoomHost);
                    currentShareUrl = MultiplayerBootstrap.BuildShareUrl(PhotonNetwork.CurrentRoom.Name);
                    urlText.text = currentShareUrl;
                    copyButtonText.text = CopyLabel;
                    sharePanel.SetActive(true);
                    SetStatus($"{colorLine} Waiting for opponent ({count}/{max}) — share the link below");
                }
                else
                {
                    EnsureLayout(LobbyLayout.RoomGuest);
                    sharePanel.SetActive(false);
                    SetStatus($"{colorLine} Waiting for match to start ({count}/{max})");
                }
                return;
            }

            sharePanel.SetActive(false);
            if (autoJoinPending)
            {
                EnsureLayout(LobbyLayout.Minimal);
                string region = PhotonNetwork.IsConnected ? PhotonNetwork.CloudRegion : "…";
                SetStatus(string.IsNullOrEmpty(bootstrap.LastLobbyError)
                    ? $"Joining room… (region {region})"
                    : bootstrap.LastLobbyError);
                hostPanel.SetActive(false);
                guestPanel.SetActive(false);
                return;
            }

            EnsureLayout(LobbyLayout.Browse);

            string connectedRegion = PhotonNetwork.CloudRegion;
            SetStatus(string.IsNullOrEmpty(connectedRegion)
                ? "Create a room to host, or join with a room code"
                : $"Connected ({connectedRegion}). Create a room to host, or join with a room code");
            hostPanel.SetActive(true);
            guestPanel.SetActive(true);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private void OnCreateRoom()
        {
            if (bootstrap == null || creatingRoom) return;
            creatingRoom = true;
            if (createRoomButton != null) createRoomButton.interactable = false;
            bootstrap.CreateRoom();
        }

        private void ResetCreateRoomButton()
        {
            creatingRoom = false;
            if (createRoomButton != null) createRoomButton.interactable = true;
            if (createRoomText != null) createRoomText.text = CreateRoomLabel;
        }

        private void OnJoinClicked()
        {
            if (roomInput == null || bootstrap == null) return;
            bootstrap.JoinRoomCode(roomInput.text);
            RefreshUI();
        }

        private void OnBackClicked()
        {
            if (bootstrap == null) return;

            if (PhotonNetwork.InRoom || bootstrap.HasQueuedJoin)
            {
                bootstrap.LeaveCurrentRoom();
                if (roomInput != null) roomInput.text = string.Empty;
                RefreshUI();
                return;
            }

            bootstrap.ReturnToMainMenu();
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
                new Vector2(640, 340), PanelBg);
            rootPanelRect = rootPanel.GetComponent<RectTransform>();
            ApplyDesignedLobbyArt(rootPanel);

            backButton = CreateStretchButton(rootPanel.transform, "BackButton", "\u2039",
                new Vector2(0.04f, 0.91f), new Vector2(0.11f, 0.99f),
                ButtonBgMuted, OnBackClicked, TextLight, fontSize: 34).gameObject;

            statusText = CreateStretchText(rootPanel.transform, "Status", "Connecting...",
                new Vector2(0.13f, 0.91f), new Vector2(0.96f, 0.99f), 17, TextAnchor.MiddleCenter, TextDark);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;

            hostPanel = CreateStretchPanel(rootPanel.transform, "HostPanel",
                new Vector2(0.06f, 0.54f), new Vector2(0.94f, 0.87f), SectionBg);
            CreateStretchText(hostPanel.transform, "HostLabel", "Host a game",
                new Vector2(0.04f, 0.54f), new Vector2(0.96f, 0.96f), 20, TextAnchor.MiddleCenter, TextDark);
            createRoomButton = CreateStretchButton(hostPanel.transform, "CreateRoom", CreateRoomLabel,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.50f),
                ButtonBg, OnCreateRoom, TextLight);
            createRoomText = createRoomButton.GetComponentInChildren<Text>();

            guestPanel = CreateStretchPanel(rootPanel.transform, "GuestPanel",
                new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.50f), SectionBg);
            CreateStretchText(guestPanel.transform, "GuestLabel", "Join a game",
                new Vector2(0.04f, 0.70f), new Vector2(0.96f, 0.96f), 20, TextAnchor.MiddleCenter, TextDark);
            roomInput = CreateStretchInputField(guestPanel.transform, "RoomInput",
                new Vector2(0.04f, 0.10f), new Vector2(0.68f, 0.64f));
            CreateStretchButton(guestPanel.transform, "JoinButton", "Join",
                new Vector2(0.72f, 0.10f), new Vector2(0.96f, 0.64f),
                ButtonBg, OnJoinClicked, TextLight);

            sharePanel = CreateStretchPanel(rootPanel.transform, "SharePanel",
                new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.22f), ShareBg);
            CreateStretchText(sharePanel.transform, "ShareLabel", "Share:",
                new Vector2(0.04f, 0.1f), new Vector2(0.14f, 0.9f), 20, TextAnchor.MiddleLeft, TextDark);
            urlText = CreateStretchText(sharePanel.transform, "UrlText", "",
                new Vector2(0.16f, 0.1f), new Vector2(0.72f, 0.9f), 18, TextAnchor.MiddleLeft, TextDark);
            urlText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var copyBtn = CreateStretchButton(sharePanel.transform, "CopyButton", CopyLabel,
                new Vector2(0.74f, 0.1f), new Vector2(0.96f, 0.9f),
                ButtonBg, OnCopyClicked, TextLight);
            copyButtonText = copyBtn.GetComponentInChildren<Text>();

            hostPanel.SetActive(false);
            guestPanel.SetActive(false);
            sharePanel.SetActive(false);
            EnsureLayout(LobbyLayout.Browse);
        }

        private void EnsureLayout(LobbyLayout layout)
        {
            if (currentLayout == layout) return;
            currentLayout = layout;
            ApplyLayout(layout);
        }

        private void ApplyLayout(LobbyLayout layout)
        {
            switch (layout)
            {
                case LobbyLayout.Browse:
                    SetPanelSize(640, 340);
                    SetAnchors(backButton, 0.04f, 0.91f, 0.11f, 0.99f);
                    SetAnchors(statusText.rectTransform, 0.13f, 0.91f, 0.96f, 0.99f);
                    SetAnchors(hostPanel, 0.06f, 0.54f, 0.94f, 0.87f);
                    SetAnchors(guestPanel, 0.06f, 0.08f, 0.94f, 0.50f);
                    SetAnchors(sharePanel, 0.06f, 0.06f, 0.94f, 0.22f);
                    statusText.alignment = TextAnchor.MiddleCenter;
                    break;

                case LobbyLayout.RoomHost:
                    SetPanelSize(640, 210);
                    SetAnchors(backButton, 0.04f, 0.86f, 0.11f, 0.98f);
                    SetAnchors(statusText.rectTransform, 0.06f, 0.44f, 0.96f, 0.84f);
                    SetAnchors(sharePanel, 0.06f, 0.08f, 0.94f, 0.40f);
                    statusText.alignment = TextAnchor.MiddleCenter;
                    break;

                case LobbyLayout.RoomGuest:
                    SetPanelSize(640, 140);
                    SetAnchors(backButton, 0.04f, 0.78f, 0.11f, 0.98f);
                    SetAnchors(statusText.rectTransform, 0.06f, 0.10f, 0.96f, 0.90f);
                    statusText.alignment = TextAnchor.MiddleCenter;
                    break;

                case LobbyLayout.Minimal:
                    SetPanelSize(640, 120);
                    SetAnchors(backButton, 0.04f, 0.72f, 0.11f, 0.98f);
                    SetAnchors(statusText.rectTransform, 0.13f, 0.10f, 0.96f, 0.90f);
                    statusText.alignment = TextAnchor.MiddleCenter;
                    break;
            }
        }

        private void SetPanelSize(float width, float height)
        {
            if (rootPanelRect != null)
                rootPanelRect.sizeDelta = new Vector2(width, height);
        }

        private static void SetAnchors(GameObject go, float minX, float minY, float maxX, float maxY)
        {
            if (go == null) return;
            ApplyStretch(go.GetComponent<RectTransform>(), new Vector2(minX, minY), new Vector2(maxX, maxY));
        }

        private static void SetAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            if (rect == null) return;
            ApplyStretch(rect, new Vector2(minX, minY), new Vector2(maxX, maxY));
        }

        private static void ApplyDesignedLobbyArt(GameObject rootPanel)
        {
            var bg = UiArt.LobbyBackground;
            if (bg == null) return;

            var image = rootPanel.GetComponent<Image>();
            if (image == null) return;

            image.sprite = bg;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = false;

            var versus = UiArt.LobbyVersus;
            if (versus == null) return;

            var badgeGo = new GameObject("VersusBadge", typeof(RectTransform), typeof(Image));
            badgeGo.transform.SetParent(rootPanel.transform, false);
            var badgeRect = badgeGo.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(0f, 40f);
            badgeRect.sizeDelta = new Vector2(220f, 80f);
            var badgeImage = badgeGo.GetComponent<Image>();
            badgeImage.sprite = versus;
            badgeImage.preserveAspect = true;
            badgeImage.color = Color.white;
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
            text.color = color ?? TextDark;
            text.resizeTextForBestFit = false;
            ApplyStretch(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            return text;
        }

        private static Button CreateStretchButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bgColor,
            UnityEngine.Events.UnityAction onClick, Color? textColor = null, int fontSize = 22)
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
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = textColor ?? TextLight;
            ApplyStretch(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            return go.GetComponent<Button>();
        }

        private static InputField CreateStretchInputField(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = InputBg;
            ApplyStretch(go.GetComponent<RectTransform>(), anchorMin, anchorMax);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = GetUIFont();
            text.fontSize = 20;
            text.color = TextDark;
            text.supportRichText = false;
            ApplyStretch(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(12, 8), new Vector2(-12, -8));

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGo.transform.SetParent(go.transform, false);
            var placeholder = placeholderGo.GetComponent<Text>();
            placeholder.font = GetUIFont();
            placeholder.text = "Room code or paste link";
            placeholder.fontSize = 18;
            placeholder.color = Placeholder;
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
