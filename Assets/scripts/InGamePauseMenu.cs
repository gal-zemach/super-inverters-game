using Game;
using Multiplayer;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class InGamePauseMenu : MonoBehaviour
{
	private const float ButtonWidth = 285f;
	private const float ButtonHeight = 40f;
	private const float ButtonScale = 2.267338f;
	private static readonly Color ButtonColor = new Color(0.654902f, 0.654902f, 0.654902f, 1f);

	private Button _resumeButton;
	private Button _copyAddressButton;
	private Button _exitButton;

	private GameManager _gameManager;
	private bool _uiBuilt;
	private string _roomShareUrl;

	void Awake()
	{
		_gameManager = GameObject.Find("Game")?.GetComponent<GameManager>();
		BuildMenu();
	}

	public void Refresh()
	{
		if (!_uiBuilt)
			BuildMenu();

		bool inRoom = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;
		_roomShareUrl = inRoom
			? MultiplayerBootstrap.BuildShareUrl(PhotonNetwork.CurrentRoom.Name)
			: string.Empty;

		if (_copyAddressButton != null)
			_copyAddressButton.interactable = inRoom;
	}

	public void OnCopyRoomAddressClicked()
	{
		if (string.IsNullOrEmpty(_roomShareUrl)) return;
		GUIUtility.systemCopyBuffer = _roomShareUrl;
	}

	public void OnExitToMainMenuClicked()
	{
		if (_gameManager != null)
			_gameManager.RequestLocalExitToMainMenu();
		else
			UnityEngine.SceneManagement.SceneManager.LoadScene("main_menu");
	}

	private void BuildMenu()
	{
		if (_uiBuilt) return;
		_uiBuilt = true;

		var panel = transform.Find("Panel");
		if (panel == null) return;

		HideLegacyMenu(panel);
		CreateMenuButtons(panel);
		WireEndMenuManager();
	}

	private static void HideLegacyMenu(Transform panel)
	{
		string[] legacyNames =
		{
			"Resume button",
			"Replay button",
			"BackToMainMenu button",
			"LeaveLevel button",
			"RoomLinkInput",
			"RoomLinkCopy button",
			"RoomAddressText",
			"MenuButtons"
		};

		foreach (var name in legacyNames)
		{
			var child = panel.Find(name);
			if (child != null)
				child.gameObject.SetActive(false);
		}
	}

	private void CreateMenuButtons(Transform panel)
	{
		var container = new GameObject("MenuButtons", typeof(RectTransform));
		container.transform.SetParent(panel, false);

		var containerRect = container.GetComponent<RectTransform>();
		containerRect.anchorMin = new Vector2(0.5f, 0.5f);
		containerRect.anchorMax = new Vector2(0.5f, 0.5f);
		containerRect.pivot = new Vector2(0.5f, 0.5f);
		containerRect.anchoredPosition = new Vector2(0f, -180f);
		containerRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight * 3f + 40f);
		containerRect.localScale = Vector3.one;

		_resumeButton = CreateMenuButton(container.transform, "Resume", 0f,
			() => _gameManager?.ToggleInGamePauseMenu());
		_copyAddressButton = CreateMenuButton(container.transform, "Copy Room Address", -90f,
			OnCopyRoomAddressClicked);
		_exitButton = CreateMenuButton(container.transform, "Exit to Main Menu", -180f,
			OnExitToMainMenuClicked);
	}

	private static Button CreateMenuButton(Transform parent, string label, float yOffset, UnityEngine.Events.UnityAction onClick)
	{
		var buttonGo = new GameObject(label + " button", typeof(RectTransform), typeof(Image), typeof(Button));
		buttonGo.transform.SetParent(parent, false);

		var rect = buttonGo.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 1f);
		rect.anchorMax = new Vector2(0.5f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
		rect.anchoredPosition = new Vector2(0f, yOffset);
		rect.localScale = new Vector3(ButtonScale, ButtonScale, ButtonScale);

		var image = buttonGo.GetComponent<Image>();
		image.color = ButtonColor;
		var sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
		if (sprite != null)
		{
			image.sprite = sprite;
			image.type = Image.Type.Sliced;
		}

		var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
		labelGo.transform.SetParent(buttonGo.transform, false);
		var labelRect = labelGo.GetComponent<RectTransform>();
		labelRect.anchorMin = Vector2.zero;
		labelRect.anchorMax = Vector2.one;
		labelRect.offsetMin = Vector2.zero;
		labelRect.offsetMax = Vector2.zero;

		var text = labelGo.GetComponent<Text>();
		EnsureTextFont(text);
		text.text = label;
		text.fontSize = 22;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = Color.black;

		var button = buttonGo.GetComponent<Button>();
		button.targetGraphic = image;
		button.onClick.AddListener(onClick);
		return button;
	}

	private void WireEndMenuManager()
	{
		var menuManager = GetComponent<EndMenuManager>();
		if (menuManager == null || _resumeButton == null) return;

		menuManager.buttons = new[] { _resumeButton, _copyAddressButton, _exitButton };
	}

	private static Font _cachedUiFont;

	private static void EnsureTextFont(Text text)
	{
		if (text == null || text.font != null) return;
		if (_cachedUiFont == null)
		{
			_cachedUiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			if (_cachedUiFont == null)
				_cachedUiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
		}
		text.font = _cachedUiFont;
	}
}
