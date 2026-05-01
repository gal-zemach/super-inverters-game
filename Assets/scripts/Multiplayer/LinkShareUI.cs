using System;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Multiplayer
{
    // Slice 3: minimal share-link UI for the host.
    //
    // On OnJoinedRoom (master client only), shows a screen-space-overlay panel
    // with the share URL and a Copy button. Hides itself once the room is full
    // (no need to keep advertising after the friend joined). Joiner peers
    // never see this UI — they got the URL out-of-band already.
    //
    // UI is built programmatically in Awake so the host scene only needs to
    // have this component on a GameObject. A proper menu/UX pass comes in
    // Slice 5 when the multiplayer-menu scene gets built.
    public class LinkShareUI : MonoBehaviourPunCallbacks
    {
        private const string CopyLabel = "Copy";
        private const string CopiedLabel = "Copied!";
        private const float FeedbackDuration = 1.5f;

        private GameObject panel;
        private Text urlText;
        private Text buttonText;
        private string currentUrl;

        private void Awake()
        {
            BuildUI();
            panel.SetActive(false);
        }

        public override void OnJoinedRoom()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            currentUrl = BuildShareUrl(PhotonNetwork.CurrentRoom.Name);
            urlText.text = currentUrl;
            buttonText.text = CopyLabel;
            panel.SetActive(true);
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room != null && room.PlayerCount >= room.MaxPlayers)
            {
                panel.SetActive(false);
            }
        }

        private void OnCopyClicked()
        {
            GUIUtility.systemCopyBuffer = currentUrl;
            CancelInvoke(nameof(ResetButtonLabel));
            buttonText.text = CopiedLabel;
            Invoke(nameof(ResetButtonLabel), FeedbackDuration);
            Debug.Log($"[Multiplayer] Copied to clipboard: {currentUrl}");
        }

        private void ResetButtonLabel()
        {
            if (buttonText != null) buttonText.text = CopyLabel;
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("LinkShareCanvas",
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

            panel = new GameObject("SharePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            panel.GetComponent<Image>().color = new Color(0.96f, 0.96f, 0.96f, 1f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0, -20);
            panelRect.sizeDelta = new Vector2(900, 80);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(panel.transform, false);
            var label = labelGo.GetComponent<Text>();
            label.font = GetUIFont();
            label.text = "Share:";
            label.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleLeft;
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(0, 1);
            labelRect.pivot = new Vector2(0, 0.5f);
            labelRect.anchoredPosition = new Vector2(20, 0);
            labelRect.sizeDelta = new Vector2(90, 0);

            var urlGo = new GameObject("UrlText", typeof(RectTransform), typeof(Text));
            urlGo.transform.SetParent(panel.transform, false);
            urlText = urlGo.GetComponent<Text>();
            urlText.font = GetUIFont();
            urlText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            urlText.fontSize = 22;
            urlText.alignment = TextAnchor.MiddleLeft;
            urlText.horizontalOverflow = HorizontalWrapMode.Overflow;
            urlText.verticalOverflow = VerticalWrapMode.Truncate;
            var urlRect = urlGo.GetComponent<RectTransform>();
            urlRect.anchorMin = new Vector2(0, 0);
            urlRect.anchorMax = new Vector2(1, 1);
            urlRect.offsetMin = new Vector2(115, 0);
            urlRect.offsetMax = new Vector2(-160, 0);

            var btnGo = new GameObject("CopyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(panel.transform, false);
            btnGo.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.95f, 1f);
            btnGo.GetComponent<Button>().onClick.AddListener(OnCopyClicked);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1, 0.5f);
            btnRect.anchorMax = new Vector2(1, 0.5f);
            btnRect.pivot = new Vector2(1, 0.5f);
            btnRect.anchoredPosition = new Vector2(-15, 0);
            btnRect.sizeDelta = new Vector2(130, 50);

            var btnTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            btnTextGo.transform.SetParent(btnGo.transform, false);
            buttonText = btnTextGo.GetComponent<Text>();
            buttonText.font = GetUIFont();
            buttonText.text = CopyLabel;
            buttonText.color = Color.white;
            buttonText.fontSize = 22;
            buttonText.alignment = TextAnchor.MiddleCenter;
            var btnTextRect = btnTextGo.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
        }

        private static Font cachedFont;

        private static Font GetUIFont()
        {
            if (cachedFont != null) return cachedFont;
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (cachedFont == null) cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            if (cachedFont == null) Debug.LogWarning("[Multiplayer] LinkShareUI: no UI font available; text will be invisible.");
            return cachedFont;
        }

        private static string BuildShareUrl(string roomName)
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url))
            {
                return $"(editor) ?room={roomName}";
            }
            int q = url.IndexOf('?');
            string baseUrl = q >= 0 ? url.Substring(0, q) : url;
            return $"{baseUrl}?room={Uri.EscapeDataString(roomName)}";
        }
    }
}
