using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game{
	public class GameState : MonoBehaviour
	{
		[HideInInspector] public List<GameObject> shots;
		[HideInInspector] public GameObject[] platforms;
		[HideInInspector] public GameObject[] players;
		public ScoreKeeper scoreKeeper;

		[SerializeField]
		public int startLives = 5; 

		private GameView _gameView;

		private void Awake()
		{
			
			platforms = GameObject.FindGameObjectsWithTag(Values.PLATFORM_TAG); //TODO: change tag of platform bodies to platform body and assign platform to platform game objects
			shots = new List<GameObject>();
			players = GameObject.FindGameObjectsWithTag(Values.PLAYER_TAG);
			
			_gameView = GetComponent<GameView>();
		}

		private void Start()
		{
			scoreKeeper = GameObject.Find(Values.SCORES_GAMEOBJ_NAME).GetComponent<ScoreKeeper>();
			if (!scoreKeeper.scoresExist()) initializeScores();
			
			_gameView.updateScore();
		}

		public void initializeScores()
		{
			// Seed both Black and White unconditionally. In single-player the
			// players array (from scene-baked instances tagged "player") would
			// give us the same two entries; in multiplayer the players are
			// PhotonNetwork.Instantiate'd at runtime so the scene-scan finds
			// nothing at this point. Without these seeds, ScoreKeeper.decreaseScore
			// is a no-op (ContainsKey returns false) and lives never tick down.
			scoreKeeper.setScore("BlackPlayer", startLives);
			scoreKeeper.setScore("WhitePlayer", startLives);
		}

		public bool hasNoLives(string killedPlayerName)
		{
			// <= 0 (not == 0) so a score that ever skips past zero still ends the
			// game; unknown names (getScore returns DOESNT_EXIST) never do.
			int score = scoreKeeper.getScore(killedPlayerName);
			return score != scoreKeeper.DOESNT_EXIST && score <= 0;
		}

		public void decreaseScore(string playerName)
		{
			scoreKeeper.decreaseScore(playerName);
		}

		// Force the lives count to an authoritative value (used by the
		// master-authoritative multiplayer kill path to keep every peer in sync).
		public void setScore(string playerName, int lives)
		{
			scoreKeeper.setScore(playerName, lives);
		}

		public int getScore(string playerName)
		{
			return scoreKeeper.getScore(playerName);
		}

		public bool isGameStart()
		{
			foreach (var player in players)
			{	
				if (scoreKeeper.getScore(player.name) != startLives) return false;
			}
			return true;
		}
	}

}
