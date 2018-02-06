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
		
		[SerializeField]
		public int BPM = 140;
	
		
		[SerializeField]
		public bool countDown = true;
		
		[SerializeField]
		public bool countDownEveryRound = false;

		
		private bool roundEnded;
		private GameObject _endGameMenu;
		private GameObject _audioSource;
		private GameObject _countDownAnimation;
		
		
		void Awake ()
		{
			_gameState = GetComponent<GameState>();
			_gameView = GetComponent<GameView>();
			_shotFactory = GetComponent<ShotFactory>();
			
			UpdateLayerNames();	// must happen in Awake otherwise platforms are set to Default layer
			
			roundEnded = false;
			_endGameMenu = GameObject.Find(Values.END_GAME_MENU_GAMEOBJ_NAME);
			if (_endGameMenu != null) _endGameMenu.SetActive(false);
			
			_audioSource = GameObject.Find(Values.AUDIO_SOURCE_GAMEOBJ_NAME);
			
			_countDownAnimation = GameObject.Find(Values.COUNTDOWN_ANIM_GAMEOBJ_NAME);
			if (_countDownAnimation != null)
			{
				_countDownAnimation.SetActive(false);
			}
		}

		private void Start()
		{
			if (countDown && _countDownAnimation != null)
			{
				if (countDownEveryRound || _gameState.isGameStart())
				{
					StartCoroutine(startCountDown());					
				}
			}
		}

		public void MockPlatformsAtBeat(int beat_num) {
			GameObject[] platforms = GameObject.FindGameObjectsWithTag(Values.PLATFORM_TAG); //TODO: change tags of platformBody from "platform" to "platformBody" and add tag "platform" to platform
			foreach (GameObject platform in platforms) {
				PlatformManager platform_manager = platform.GetComponentInParent<PlatformManager>();
				platform_manager.SetPosition(((float)beat_num/(float)platform_manager.beats_per_cycle)%1);
			}
		}

		public void SpawnShot(Vector2 position, Vector2 startVelocity, float rotation, Framework framework) {
			GameObject shot = _shotFactory.MakeObject(position, startVelocity,rotation,framework);
		}
		
		
		private void UpdateLayerNames()
		{
			white_platforms_layer = LayerMask.NameToLayer(Values.WHITE_PLATFORM_LAYER);
			black_platforms_layer = LayerMask.NameToLayer(Values.BLACK_PLATFORM_LAYER);
			grey_platforms_layer = LayerMask.NameToLayer(Values.GREY_PLATFORM_LAYER);
			white_player_layer = LayerMask.NameToLayer(Values.WHITE_PLAYER_LAYER);
			black_player_layer = LayerMask.NameToLayer(Values.BLACK_PLAYER_LAYER);
		}

		public void ChangeLayer(GameObject obj, Framework framework)
		{
//			Debug.Log("ChangeLayer: " + obj.tag);
			if (obj.CompareTag(Values.PLATFORM_BODY_TAG)) {
				SetLayerRecursively(obj, framework);
			}
			
			else if (obj.CompareTag(Values.PLAYER_TAG)) obj.layer = framework == Framework.BLACK ? black_player_layer : 
														   white_player_layer;
		}

		// Recursion is probably not a good idea
		public void SetLayerRecursively(GameObject obj, Framework framework )
		{
			obj.layer = framework == Framework.BLACK ? black_platforms_layer : 
				framework == Framework.GREY ? grey_platforms_layer : 
				white_platforms_layer;
			foreach ( Transform child in obj.transform )
			{
				SetLayerRecursively( child.gameObject, framework );
			}
		}

		public void PlayerKilled(GameObject killedPlayer)
		{
			if (roundEnded) return;  // This is to solve case where 2 players died one after the other
			
			roundEnded = true;
			_gameState.incrementScore(killedPlayer.name);
			_gameView.updateScore();

			// added _endGameMenu null check for testing purposes, so if you don't have the end game menu you can keep playing forever.
			if (_gameState.reachedMaxScore(killedPlayer.name) && _endGameMenu != null)
			{
				Debug.Log(killedPlayer.name + " Lost");
				endGame();
			}
			else
			{
				Debug.Log("reloading level");
				StartCoroutine(waitThenReloadGame());
			}
		}

		IEnumerator waitThenReloadGame()
		{
			yield return new WaitForSeconds(secondsToNewRound);
			SceneManager.LoadScene(gameSceneName);
		}

		// I moved all the action
		private void endGame()
		{
			StartCoroutine(waitThenEndGame());
			Destroy(_gameState.scoreKeeper.gameObject); // so the scores don't stay for the next level
		}
		
		IEnumerator waitThenEndGame()
		{
			yield return new WaitForSeconds(secondsToNewRound);
			_endGameMenu.SetActive(true);
			Destroy(_audioSource); // This is here so the audio will stop only after the menu appeared (because the menu has its own audio)
		}
		
		IEnumerator startCountDown()
		{
			disablePlayerControls(true);
			AudioSource audioSource = _audioSource.GetComponent<AudioSource>();
			audioSource.Stop();
			
			yield return new WaitForSeconds(1f);
			_countDownAnimation.SetActive(true);
			
			yield return new WaitForSeconds(2.5f);
			audioSource.Play();
			
			yield return new WaitForSeconds(0.5f);
			Debug.Log("GameManager: Player controls enabled");
			disablePlayerControls(false);
			
			yield return new WaitForSeconds(0.2f);
			_countDownAnimation.SetActive(false);
		}

		private void disablePlayerControls(bool status)
		{
			foreach (var player in _gameState.players)
			{
				player.GetComponent<PlayerManager>().DisableControls(status);
			}
		}
	}
}
