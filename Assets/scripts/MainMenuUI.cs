using UnityEngine;
using UnityEngine.UI;

namespace Game
{
	// Main menu: multiplayer and single-player both active.
	public class MainMenuUI : MonoBehaviour
	{
		private const string MultiplayerScene = "Multiplayer";
		private const string SinglePlayerScene = "level_menu";

		[SerializeField] private Button singlePlayerButton;
		[SerializeField] private Button multiplayerButton;

		private SceneLoader sceneLoader;

		private void Awake()
		{
			sceneLoader = GetComponent<SceneLoader>();
			if (sceneLoader == null)
				sceneLoader = gameObject.AddComponent<SceneLoader>();

			if (singlePlayerButton == null)
				singlePlayerButton = transform.Find("singleplayer button")?.GetComponent<Button>();
			if (multiplayerButton == null)
				multiplayerButton = transform.Find("multiplayer button")?.GetComponent<Button>();

			WireButton(singlePlayerButton, SinglePlayerScene);
			WireButton(multiplayerButton, MultiplayerScene);
		}

		private void WireButton(Button button, string sceneName)
		{
			if (button == null) return;

			button.interactable = true;
			button.transition = Selectable.Transition.ColorTint;

			var colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
			colors.pressedColor = new Color(0.45f, 0.45f, 0.45f, 1f);
			colors.selectedColor = colors.highlightedColor;
			colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f);
			colors.colorMultiplier = 1f;
			button.colors = colors;

			var image = button.GetComponent<Image>();
			if (image != null)
				image.color = Color.white;

			// Dark label: the button image is white in its normal state (the tint
			// only greys it when selected/pressed), so a near-white label is
			// invisible until the button is selected.
			var label = button.GetComponentInChildren<Text>();
			if (label != null)
				label.color = new Color(0.15f, 0.15f, 0.15f, 1f);

			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(() => sceneLoader.LoadSceneByName(sceneName));
		}
	}
}
