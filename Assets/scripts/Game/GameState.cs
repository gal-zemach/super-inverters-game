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
			foreach (var player in players)
			{	
				scoreKeeper.setScore(player.name, startLives);
			}
		}

		public bool hasNoLives(string killedPlayerName)
		{
			return scoreKeeper.getScore(killedPlayerName) == 0;
		}

		public void decreaseScore(string playerName)
		{
			scoreKeeper.decreaseScore(playerName);
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
