using Game;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
	// Screen-space shoot cooldown indicator. Hierarchy is created under ShootCooldownHUD
	// in the player prefab (OnValidate in Editor); move CooldownIndicator RectTransform to reposition.
	public class ShootCooldownUI : MonoBehaviour
	{
		[SerializeField] private RectTransform indicatorRoot;
		[SerializeField] private Image fillImage;
		[SerializeField] private bool hideWhenReady = true;

		private PlayerManager _playerManager;
		private PhotonView _photonView;

		private void Awake()
		{
			_playerManager = GetComponent<PlayerManager>();
			_photonView = GetComponent<PhotonView>();
			WireReferencesFromHierarchy();
			if (fillImage == null)
				BuildHudHierarchy();
		}

		private void Update()
		{
			if (_playerManager == null) return;
			if (!IsLocalPlayer()) return;
			if (fillImage == null || indicatorRoot == null) return;

			float cooldown01 = _playerManager.ShootCooldown01;
			fillImage.fillAmount = cooldown01;

			bool show = !hideWhenReady || cooldown01 > 0.001f;
			if (indicatorRoot.gameObject.activeSelf != show)
				indicatorRoot.gameObject.SetActive(show);
		}

		private bool IsLocalPlayer()
		{
			if (_photonView != null)
				return _photonView.IsMine;
			return true;
		}

		private void WireReferencesFromHierarchy()
		{
			if (indicatorRoot == null)
			{
				var hud = transform.Find("ShootCooldownHUD");
				if (hud != null)
					indicatorRoot = hud.Find("CooldownIndicator") as RectTransform;
			}

			if (fillImage == null && indicatorRoot != null)
				fillImage = indicatorRoot.Find("Fill")?.GetComponent<Image>();
		}

		private void BuildHudHierarchy()
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
			canvas.sortingOrder = 50;

			var scaler = canvasGo.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920, 1080);
			scaler.matchWidthOrHeight = 0.5f;

			var indicatorGo = new GameObject("CooldownIndicator",
				typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			indicatorGo.transform.SetParent(canvasGo.transform, false);
			var indicatorRect = indicatorGo.GetComponent<RectTransform>();
			indicatorRect.anchorMin = new Vector2(0.85f, 0.12f);
			indicatorRect.anchorMax = new Vector2(0.85f, 0.12f);
			indicatorRect.pivot = new Vector2(0.5f, 0.5f);
			indicatorRect.sizeDelta = new Vector2(48f, 48f);
			indicatorRect.anchoredPosition = Vector2.zero;
			var bg = indicatorGo.GetComponent<Image>();
			bg.color = new Color(0.1f, 0.1f, 0.1f, 0.55f);
			bg.raycastTarget = false;

			var fillGo = new GameObject("Fill",
				typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			fillGo.transform.SetParent(indicatorGo.transform, false);
			var fillRect = fillGo.GetComponent<RectTransform>();
			fillRect.anchorMin = Vector2.zero;
			fillRect.anchorMax = Vector2.one;
			fillRect.offsetMin = Vector2.zero;
			fillRect.offsetMax = Vector2.zero;
			var fill = fillGo.GetComponent<Image>();
			fill.color = new Color(1f, 0.85f, 0.2f, 0.9f);
			fill.raycastTarget = false;
			fill.type = Image.Type.Filled;
			fill.fillMethod = Image.FillMethod.Radial360;
			fill.fillOrigin = (int)Image.Origin360.Top;
			fill.fillClockwise = false;
			fill.fillAmount = 0f;

			indicatorRoot = indicatorRect;
			fillImage = fill;

#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				UnityEditor.EditorUtility.SetDirty(this);
				UnityEditor.EditorUtility.SetDirty(gameObject);
			}
#endif
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
			if (transform.Find("ShootCooldownHUD") == null)
				BuildHudHierarchy();
			else
				WireReferencesFromHierarchy();
		}

		[ContextMenu("Rebuild Cooldown HUD")]
		private void RebuildCooldownHud()
		{
			BuildHudHierarchy();
		}
#endif
	}
}
