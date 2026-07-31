using UnityEngine;
using UnityEngine.UI;

namespace Game
{
	// Level-select screen: "BOT DIFFICULTY ◀ 4 ▶" stepper, bottom-center.
	// The row is built at runtime onto the scene canvas (same approach as the
	// pause menu's runtime buttons) so the scene only needs this component.
	// Up/Down arrows, W/S or a gamepad stick step the tier; the ◀ ▶ buttons
	// are mouse-clickable. Level navigation on this screen is horizontal, so
	// the vertical axis is free. The choice persists via BotDifficulty.
	public class BotDifficultyUI : MonoBehaviour
	{
		private const float AxisThreshold = 0.3f;

		private Text _valueText;
		private bool _axisReady = true;

		private void Start()
		{
			var canvas = FindObjectOfType<Canvas>();
			if (canvas == null) return;
			BuildRow(canvas.transform);
			RefreshValue();
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
				Step(+1);
			else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
				Step(-1);

			CheckAxis("J1_PS4_LeftStick_Vertical");
			CheckAxis("J2_PS4_LeftStick_Vertical");
		}

		// Gamepad: one step per stick flick (re-arms when the stick returns to
		// neutral). Stick up is negative on this project's PS4 vertical mapping
		// (same convention EndMenuManager navigates by).
		private void CheckAxis(string axis)
		{
			float v = Input.GetAxis(axis);
			if (Mathf.Abs(v) < AxisThreshold)
			{
				_axisReady = true;
				return;
			}
			if (!_axisReady) return;
			_axisReady = false;
			Step(v < 0 ? +1 : -1);
		}

		private void Step(int delta)
		{
			BotDifficulty.Tier += delta;
			RefreshValue();
		}

		private void RefreshValue()
		{
			if (_valueText != null)
				_valueText.text = BotDifficulty.Tier + " / " + BotDifficulty.MaxTier;
		}

		// ---- runtime UI construction --------------------------------------

		private void BuildRow(Transform canvas)
		{
			var row = CreateRect("BotDifficultyRow", canvas);
			row.anchorMin = new Vector2(0.5f, 0f);
			row.anchorMax = new Vector2(0.5f, 0f);
			row.pivot = new Vector2(0.5f, 0f);
			row.anchoredPosition = new Vector2(0f, 28f);
			row.sizeDelta = new Vector2(560f, 44f);

			MakeText(row, "Label", "BOT DIFFICULTY", 24, FontStyle.Normal,
				new Vector2(-90f, 0f), new Vector2(280f, 44f));

			var left = MakeText(row, "StepDown", "◀", 26, FontStyle.Bold,
				new Vector2(90f, 0f), new Vector2(40f, 44f));
			AddTextButton(left, () => Step(-1));

			_valueText = MakeText(row, "Value", "", 26, FontStyle.Bold,
				new Vector2(160f, 0f), new Vector2(100f, 44f));

			var right = MakeText(row, "StepUp", "▶", 26, FontStyle.Bold,
				new Vector2(230f, 0f), new Vector2(40f, 44f));
			AddTextButton(right, () => Step(+1));
		}

		private static RectTransform CreateRect(string name, Transform parent)
		{
			var go = new GameObject(name, typeof(RectTransform));
			go.transform.SetParent(parent, false);
			return (RectTransform)go.transform;
		}

		private static Text MakeText(RectTransform parent, string name, string content,
			int size, FontStyle style, Vector2 pos, Vector2 sizeDelta)
		{
			var rect = CreateRect(name, parent);
			rect.anchoredPosition = pos;
			rect.sizeDelta = sizeDelta;
			var text = rect.gameObject.AddComponent<Text>();
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			text.fontSize = size;
			text.fontStyle = style;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = new Color(0.9f, 0.9f, 0.9f, 1f);
			text.text = content;
			return text;
		}

		// A Button whose target graphic is the text itself: no sprites needed
		// (this project's UI avoids the builtin UISprite, which errors here),
		// and automatic navigation is off so arrow keys can't steal selection
		// from the level buttons.
		private static void AddTextButton(Text text, UnityEngine.Events.UnityAction onClick)
		{
			var button = text.gameObject.AddComponent<Button>();
			button.targetGraphic = text;
			var nav = button.navigation;
			nav.mode = Navigation.Mode.None;
			button.navigation = nav;
			var colors = button.colors;
			colors.highlightedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
			colors.pressedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
			button.colors = colors;
			button.onClick.AddListener(onClick);
		}
	}
}
