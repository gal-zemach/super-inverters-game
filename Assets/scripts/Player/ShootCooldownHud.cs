using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
	// Thin burst-ammo bar under the local player's lives row (black or white by player color).
	public class ShootCooldownHud : MonoBehaviour
	{
		[SerializeField] private float barHeight = 8f;
		[SerializeField] private float fallbackBarWidth = 110f;

		[Header("Position under lives")]
		[SerializeField] private Vector3 belowLivesWorldOffset = new Vector3(0f, -4.5f, 0f);
		[SerializeField] private Vector2 screenFineTuneOffset = Vector2.zero;

		[Header("Grenade count")]
		[Tooltip("Height of each grenade icon shown under the lives row (canvas px).")]
		[SerializeField] private float grenadeIconHeight = 26f;
		[Tooltip("Horizontal step between icons — the width of each icon's invisible box, " +
		         "so they read as separate slots.")]
		[SerializeField] private float grenadeIconSpacing = 44f;
		[SerializeField] private Vector2 grenadeRowOffset = new Vector2(0f, -6f);
		[Tooltip("Show the burst-ammo bar line. Off = only the grenade icons render (the bar " +
		         "still drives their anchoring).")]
		[SerializeField] private bool showBurstBar = false;

		private static readonly Color BlackFill = Color.black;
		private static readonly Color BlackTrack = new Color(0f, 0f, 0f, 0.22f);
		private static readonly Color WhiteFill = Color.white;
		private static readonly Color WhiteTrack = new Color(1f, 1f, 1f, 0.22f);

		private GameView _gameView;
		private RectTransform _canvasRect;
		private RectTransform _barRoot;
		private RectTransform _trackRect;
		private RectTransform _fillRect;
		private Image _trackImage;
		private Image _fillImage;
		private PlayerManager _localPlayer;
		private Framework? _barFramework;
		private float _barWidth;
		private RectTransform _grenadeRow;
		private Image _grenadeRowBg;
		private readonly System.Collections.Generic.List<Image> _grenadeIcons =
			new System.Collections.Generic.List<Image>();
		private Sprite _grenadeSprite;

		private void Awake()
		{
			_gameView = GetComponent<GameView>();
			BuildHud();
		}

		private void Update()
		{
			if (_fillRect == null || _barRoot == null)
				return;

			RefreshLocalPlayer();
			if (_localPlayer == null)
			{
				if (_barRoot.gameObject.activeSelf)
					_barRoot.gameObject.SetActive(false);
				return;
			}

			var playerState = _localPlayer.GetComponent<PlayerState>();
			if (playerState == null) return;

			if (!_barRoot.gameObject.activeSelf)
				_barRoot.gameObject.SetActive(true);

			UpdateBarLayout(playerState.player_framework);
			ApplyBarColors(playerState.player_framework);

			_trackImage.enabled = showBurstBar;
			_fillImage.enabled = showBurstBar;

			float ammo01 = _localPlayer.BurstAmmo01;
			_fillRect.sizeDelta = new Vector2(_barWidth * ammo01, barHeight);

			UpdateGrenadeIcons(playerState.player_framework);
		}

		// One grenade icon per grenade in the local player's inventory, in a row under the
		// lives. Black's row hugs the lives row's LEFT edge, White's hugs the RIGHT edge
		// (mirroring the lives layout). An opaque white plate sits behind the icons so
		// they never visually blend into level platforms.
		private void UpdateGrenadeIcons(Framework framework)
		{
			if (_grenadeRow == null || _localPlayer == null) return;

			var inv = _localPlayer.GetComponent<Powerups.GrenadeInventory>();
			int count = inv != null ? inv.Count : 0;

			if (_grenadeSprite == null)
			{
				var thrower = _localPlayer.GetComponent<Powerups.GrenadeThrower>();
				if (thrower != null) _grenadeSprite = thrower.GrenadeSprite;
			}

			const float pad = 6f;
			float iconW = grenadeIconHeight * 0.82f;

			while (_grenadeIcons.Count < count)
			{
				var iconGo = new GameObject("GrenadeIcon",
					typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
				iconGo.transform.SetParent(_grenadeRow, false);
				var rt = iconGo.GetComponent<RectTransform>();
				rt.anchorMin = new Vector2(0f, 1f);
				rt.anchorMax = new Vector2(0f, 1f);
				rt.pivot = new Vector2(0f, 1f);
				rt.sizeDelta = new Vector2(iconW, grenadeIconHeight);
				var img = iconGo.GetComponent<Image>();
				img.raycastTarget = false;
				img.preserveAspect = true;
				_grenadeIcons.Add(img);
			}

			bool anyShown = count > 0 && _grenadeSprite != null;
			float plateW = pad * 2f + (count > 0 ? (count - 1) * grenadeIconSpacing + iconW : 0f);
			float plateH = pad * 2f + grenadeIconHeight;

			_grenadeRow.sizeDelta = new Vector2(plateW, plateH);
			_grenadeRow.anchoredPosition = new Vector2(
				(framework == Framework.BLACK ? 0f : _barWidth - plateW) + grenadeRowOffset.x,
				grenadeRowOffset.y);
			if (_grenadeRowBg != null) _grenadeRowBg.enabled = anyShown;

			for (int i = 0; i < _grenadeIcons.Count; i++)
			{
				var rt = _grenadeIcons[i].rectTransform;
				rt.anchoredPosition = new Vector2(pad + i * grenadeIconSpacing, -pad);
				if (_grenadeIcons[i].sprite == null) _grenadeIcons[i].sprite = _grenadeSprite;
				_grenadeIcons[i].enabled = anyShown && i < count;
			}
		}

		private void ApplyBarColors(Framework framework)
		{
			if (_barFramework == framework || _fillImage == null || _trackImage == null)
				return;

			_barFramework = framework;
			if (framework == Framework.BLACK)
			{
				_fillImage.color = BlackFill;
				_trackImage.color = BlackTrack;
			}
			else
			{
				_fillImage.color = WhiteFill;
				_trackImage.color = WhiteTrack;
			}
		}

		private void RefreshLocalPlayer()
		{
			if (_localPlayer == null)
			{
				_localPlayer = FindLocalPlayerManager();
				return;
			}

			if (!_localPlayer.isActiveAndEnabled)
			{
				_localPlayer = null;
				return;
			}

			var pv = _localPlayer.GetComponent<PhotonView>();
			if (pv != null && !pv.IsMine)
				_localPlayer = null;
		}

		private void UpdateBarLayout(Framework framework)
		{
			var cam = Camera.main;
			if (cam == null || _gameView == null || _gameView.lives_objects == null)
				return;

			int livesIndex = framework == Framework.BLACK ? 0 : 1;
			if (livesIndex < 0 || livesIndex >= _gameView.lives_objects.Length)
				return;

			var livesObject = _gameView.lives_objects[livesIndex];
			if (livesObject == null)
				return;

			var livesVisualizer = livesObject.GetComponent<LivesVisualizer>();
			float rowWorldWidth = livesVisualizer != null ? livesVisualizer.RowWorldWidth : 20f;

			Transform livesTransform = livesObject.transform;
			Vector3 rowStartWorld = livesTransform.position;
			Vector3 rowEndWorld = livesTransform.TransformPoint(new Vector3(rowWorldWidth, 0f, 0f));
			Vector3 anchorWorld = rowStartWorld + belowLivesWorldOffset;

			Vector3 screenStart = cam.WorldToScreenPoint(rowStartWorld);
			Vector3 screenEnd = cam.WorldToScreenPoint(rowEndWorld);
			Vector3 screenAnchor = cam.WorldToScreenPoint(anchorWorld);
			if (screenAnchor.z < 0f)
				return;

			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
				    _canvasRect, screenStart, null, out Vector2 localStart) &&
			    RectTransformUtility.ScreenPointToLocalPointInRectangle(
				    _canvasRect, screenEnd, null, out Vector2 localEnd) &&
			    RectTransformUtility.ScreenPointToLocalPointInRectangle(
				    _canvasRect, screenAnchor, null, out Vector2 localAnchor))
			{
				float leftX = Mathf.Min(localStart.x, localEnd.x);
				_barWidth = Mathf.Max(24f, Mathf.Abs(localEnd.x - localStart.x));
				_barRoot.pivot = new Vector2(0f, 1f);
				_barRoot.anchorMin = new Vector2(0.5f, 0.5f);
				_barRoot.anchorMax = new Vector2(0.5f, 0.5f);
				_barRoot.anchoredPosition = new Vector2(leftX, localAnchor.y) + screenFineTuneOffset;
				_barRoot.sizeDelta = new Vector2(_barWidth, barHeight);

				_trackRect.sizeDelta = new Vector2(_barWidth, barHeight);
				_fillRect.sizeDelta = new Vector2(_barWidth, barHeight);
			}
			else if (_barWidth <= 0f)
			{
				_barWidth = fallbackBarWidth;
				_barRoot.sizeDelta = new Vector2(_barWidth, barHeight);
				_trackRect.sizeDelta = new Vector2(_barWidth, barHeight);
			}
		}

		private static PlayerManager FindLocalPlayerManager()
		{
			PlayerManager fallback = null;

			foreach (var go in GameObject.FindGameObjectsWithTag(Values.PLAYER_TAG))
			{
				var manager = go.GetComponent<PlayerManager>();
				if (manager == null) continue;

				var pv = go.GetComponent<PhotonView>();
				if (pv == null)
				{
					fallback = manager;
					continue;
				}

				if (pv.IsMine)
					return manager;
			}

			return fallback;
		}

		private void BuildHud()
		{
			var existing = transform.Find("ShootCooldownHUD");
			if (existing != null)
			{
#if UNITY_EDITOR
				if (!Application.isPlaying)
					DestroyImmediate(existing.gameObject);
				else
#endif
					Destroy(existing.gameObject);
			}

			var canvasGo = new GameObject("ShootCooldownHUD",
				typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			canvasGo.transform.SetParent(transform, false);

			var canvas = canvasGo.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 500;

			_canvasRect = canvasGo.GetComponent<RectTransform>();
			_canvasRect.anchorMin = Vector2.zero;
			_canvasRect.anchorMax = Vector2.one;
			_canvasRect.offsetMin = Vector2.zero;
			_canvasRect.offsetMax = Vector2.zero;

			var scaler = canvasGo.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920, 1080);
			scaler.matchWidthOrHeight = 0.5f;

			var barGo = new GameObject("BurstBar",
				typeof(RectTransform));
			barGo.transform.SetParent(canvasGo.transform, false);
			_barRoot = barGo.GetComponent<RectTransform>();
			_barWidth = fallbackBarWidth;
			_barRoot.sizeDelta = new Vector2(_barWidth, barHeight);

			var trackGo = new GameObject("Track",
				typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			trackGo.transform.SetParent(barGo.transform, false);
			_trackRect = trackGo.GetComponent<RectTransform>();
			_trackRect.anchorMin = new Vector2(0f, 0.5f);
			_trackRect.anchorMax = new Vector2(0f, 0.5f);
			_trackRect.pivot = new Vector2(0f, 0.5f);
			_trackRect.anchoredPosition = Vector2.zero;
			_trackRect.sizeDelta = new Vector2(_barWidth, barHeight);
			_trackImage = trackGo.GetComponent<Image>();
			_trackImage.color = WhiteTrack;
			_trackImage.raycastTarget = false;

			var fillGo = new GameObject("Fill",
				typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			fillGo.transform.SetParent(barGo.transform, false);
			_fillRect = fillGo.GetComponent<RectTransform>();
			_fillRect.anchorMin = new Vector2(0f, 0.5f);
			_fillRect.anchorMax = new Vector2(0f, 0.5f);
			_fillRect.pivot = new Vector2(0f, 0.5f);
			_fillRect.anchoredPosition = Vector2.zero;
			_fillRect.sizeDelta = new Vector2(_barWidth, barHeight);

			_fillImage = fillGo.GetComponent<Image>();
			_fillImage.color = WhiteFill;
			_fillImage.raycastTarget = false;

			var rowGo = new GameObject("GrenadeRow",
				typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			rowGo.transform.SetParent(barGo.transform, false);
			_grenadeRow = rowGo.GetComponent<RectTransform>();
			_grenadeRow.anchorMin = new Vector2(0f, 0f);
			_grenadeRow.anchorMax = new Vector2(0f, 0f);
			_grenadeRow.pivot = new Vector2(0f, 1f);
			_grenadeRow.anchoredPosition = grenadeRowOffset;
			_grenadeRow.sizeDelta = Vector2.zero;
			_grenadeRowBg = rowGo.GetComponent<Image>();
			_grenadeRowBg.color = Color.white;   // opaque plate so icons never blend into platforms
			_grenadeRowBg.raycastTarget = false;
			_grenadeRowBg.enabled = false;

			_barRoot.gameObject.SetActive(false);
		}
	}
}
