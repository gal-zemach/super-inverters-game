using UnityEngine;
using UnityEngine.UI;

namespace Game
{
	// Main menu: multiplayer is active; single-player is shown disabled (not yet available).
	public class MainMenuUI : MonoBehaviour
	{
		private const string MultiplayerScene = "Multiplayer";
		private static readonly Color DisabledBg = new Color(0.68f, 0.68f, 0.68f, 0.55f);
		private static readonly Color DisabledText = new Color(0.55f, 0.55f, 0.55f, 0.65f);

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

			DisableSinglePlayerButton();
			WireButton(multiplayerButton, MultiplayerScene);
		}

		private void DisableSinglePlayerButton()
		{
			if (singlePlayerButton == null) return;

			singlePlayerButton.onClick.RemoveAllListeners();
			singlePlayerButton.interactable = false;
			singlePlayerButton.transition = Selectable.Transition.None;

			var nav = singlePlayerButton.navigation;
			nav.mode = Navigation.Mode.None;
			singlePlayerButton.navigation = nav;

			var colors = singlePlayerButton.colors;
			colors.normalColor = DisabledBg;
			colors.highlightedColor = DisabledBg;
			colors.pressedColor = DisabledBg;
			colors.selectedColor = DisabledBg;
			colors.disabledColor = DisabledBg;
			singlePlayerButton.colors = colors;

			var image = singlePlayerButton.GetComponent<Image>();
			if (image != null)
				image.color = DisabledBg;

			var label = singlePlayerButton.GetComponentInChildren<Text>();
			if (label != null)
				label.color = DisabledText;
		}

		private void WireButton(Button button, string sceneName)
		{
			if (button == null) return;

			button.interactable = true;
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(() => sceneLoader.LoadSceneByName(sceneName));
		}
	}
}
