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
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(() => sceneLoader.LoadSceneByName(sceneName));
		}
	}
}
