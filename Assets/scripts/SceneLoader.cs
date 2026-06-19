using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

namespace Game {
	public class SceneLoader : MonoBehaviour {

		private const string MainMenuSceneName = "main_menu";
		private static int MIN_LEVEL_IDX = 2;
		private static int MAX_LEVEL_IDX = 5;

		public void LoadScene(int index) {
			// End-game Leave Level uses build index 1 — both peers exit together.
			if (index == 1)
			{
				var gameManager = GameObject.Find("Game")?.GetComponent<GameManager>();
				if (gameManager != null)
				{
					gameManager.RequestNetworkedExitToMainMenu();
					return;
				}

				SceneManager.LoadScene(MainMenuSceneName);
				return;
			}
			SceneManager.LoadScene(index);
		}

		public void ExitToMainMenu()
		{
			ExitToMainMenuLocal();
		}

		public void ExitToMainMenuLocal()
		{
			var gameManager = GameObject.Find("Game")?.GetComponent<GameManager>();
			if (gameManager != null)
			{
				gameManager.RequestLocalExitToMainMenu();
				return;
			}

			SceneManager.LoadScene(MainMenuSceneName);
		}

		public void LoadSceneByName(string sceneName) {
			SceneManager.LoadScene(sceneName);
		}

		public void ReloadCurrentScene()
		{
			var gameManager = GameObject.Find("Game")?.GetComponent<GameManager>();
			if (gameManager != null)
			{
				gameManager.RequestNetworkedReplay();
				return;
			}

			int currentScene = SceneManager.GetActiveScene().buildIndex;
			LoadScene(currentScene);
		}

		public void LoadRandomScene() {
			int scene_idx = Random.Range(MIN_LEVEL_IDX,MAX_LEVEL_IDX);
			SceneManager.LoadScene(scene_idx);
		}
	}
}
