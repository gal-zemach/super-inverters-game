﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

//using System;

namespace Game {

	public enum Framework {GREY, BLACK, WHITE}

	public class GameManager : MonoBehaviour {

		private GameView _gameView;
		private GameState _gameState;
		private ShotFactory _shotFactory;

		private int white_platforms_layer, black_platforms_layer, grey_platforms_layer,
					white_player_layer, black_player_layer;

		[SerializeField]
		public string gameSceneName;
		
		[SerializeField]
		public float secondsToNewRound = 3f;
		
		void Awake ()
		{
			_gameState = GetComponent<GameState>();
			_gameView = GetComponent<GameView>();
			_shotFactory = GetComponent<ShotFactory>();
			
			UpdateLayerNames();	// must happen in Awake otherwise platforms are set to Default layer
		}

		private void Start()
		{
				
		}

		public void SpawnShot(Vector2 position, Vector2 startVelocity, float rotation, Framework framework) {
			GameObject shot = _shotFactory.MakeObject(position, startVelocity,rotation,framework);
		}
		
		
		private void UpdateLayerNames()
		{
			white_platforms_layer = LayerMask.NameToLayer("platforms_white");
			black_platforms_layer = LayerMask.NameToLayer("platforms_black");
			grey_platforms_layer = LayerMask.NameToLayer("platforms_grey");
			white_player_layer = LayerMask.NameToLayer("players_white");
			black_player_layer = LayerMask.NameToLayer("players_black");
		}

		public void ChangeLayer(GameObject obj, Framework framework)
		{
			if (obj.CompareTag("platform")) obj.layer = framework == Framework.BLACK ? black_platforms_layer : 
														framework == Framework.GREY ? grey_platforms_layer : 
														white_platforms_layer;
			
			else if (obj.CompareTag("player")) obj.layer = framework == Framework.BLACK ? black_player_layer : 
														   white_player_layer;
		}

		public void PlayerKilled(GameObject killedPlayer)
		{
			foreach (var player in _gameState.players)
			{
				if (player.name != killedPlayer.name)
				{
					_gameState.incrementScore(player.name);
				}
			}
			_gameView.updateScore();
			reloadGame();
		}

		private void reloadGame()
		{
			StartCoroutine(waitThenReloadGame());
		}

		IEnumerator waitThenReloadGame()
		{
			yield return new WaitForSeconds(secondsToNewRound);
			SceneManager.LoadScene(gameSceneName);
		}
	}
}
