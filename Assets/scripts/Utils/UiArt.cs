using UnityEngine;

namespace Utils
{
	// Loads designed UI sprites from Assets/Resources/UI/ at runtime.
	public static class UiArt
	{
		public static Sprite PauseBackground => Load("UI/Pause/BG");
		public static Sprite PauseResume => Load("UI/Pause/resume");
		public static Sprite PauseExit => Load("UI/Pause/back_to_main_menu");
		public static Sprite LobbyBackground => Load("UI/Lobby/Background");
		public static Sprite LobbyVersus => Load("UI/Lobby/Versus static");
		public static Sprite GameOverExit => Load("UI/GameOver/Back to Main menu");

		private static Sprite Load(string path)
		{
			return Resources.Load<Sprite>(path);
		}
	}
}
