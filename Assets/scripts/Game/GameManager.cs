using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

//using System;

namespace Game {

	public enum Framework {GREY, BLACK, WHITE}

	// Inherits MonoBehaviourPun so we can call photonView.RPC for the
	// networked PlayerKilled flow. Game.prefab needs a PhotonView component
	// (added 2026-05-07).
	public class GameManager : MonoBehaviourPun {

		private GameView _gameView;
		private GameState _gameState;
		private ShotFactory _shotFactory;
		private ShellFactory _shellFactory;

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
		private GameObject _pause_menu;
		
		
		void Awake ()
		{
			_gameState = GetComponent<GameState>();
			_gameView = GetComponent<GameView>();
			_shotFactory = GetComponent<ShotFactory>();
			_shellFactory = GetComponent<ShellFactory>();

			GameObject tmp = GameObject.Find("PauseMenu");
			if (tmp != null) {
				_pause_menu = tmp;
				Debug.Log("GameManager: pause menu found");
				_pause_menu.SetActive(false);
			}



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
					Debug.Log("GameManager: Paying CountDown");
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

		public void SpawnShell(Vector2 position, Vector2 startVelocity, float rotation, Framework framework, Collider2D shooterCollider) {
			GameObject shell = _shellFactory.MakeObject(position, startVelocity, rotation, framework, shooterCollider);
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
			// Networked-instantiated objects get a "(Clone)" suffix on their
			// name; strip it so the score / win-condition logic (which keys
			// off the literal strings "BlackPlayer" / "WhitePlayer") matches
			// in both single-player and multiplayer.
			string playerName = killedPlayer.name.Replace("(Clone)", "").Trim();

			if (PhotonNetwork.InRoom)
			{
				// Broadcast so all peers update their local score and trigger
				// reload together. AllViaServer guarantees ordered delivery
				// (peers apply the kill in the same sequence). The dying
				// peer's PlayerManager already gated this call by IsMine, so
				// this RPC fires exactly once per actual death.
				photonView.RPC(nameof(RPCPlayerKilled), RpcTarget.AllViaServer, playerName);
				return;
			}

			DoPlayerKilled(playerName);
		}

		[PunRPC]
		private void RPCPlayerKilled(string killedPlayerName)
		{
			DoPlayerKilled(killedPlayerName);
		}

		private void DoPlayerKilled(string killedPlayerName)
		{
			if (roundEnded) return;  // This is to solve case where 2 players died one after the other

			roundEnded = true;
			_gameState.decreaseScore(killedPlayerName);
			_gameView.decreaseScore(killedPlayerName);
//			_gameView.updateScore();

			// added _endGameMenu null check for testing purposes, so if you don't have the end game menu you can keep playing forever.
			if (_gameState.hasNoLives(killedPlayerName) && _endGameMenu != null)
			{
				Debug.Log(killedPlayerName + " Lost");
				int winPlayerId = 0;
				if (killedPlayerName == "BlackPlayer") {
					winPlayerId = 2; // white player wins
				}
				else if (killedPlayerName == "WhitePlayer") {
					winPlayerId = 1; // black player wins
				}
				endGame(winPlayerId);
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

			if (PhotonNetwork.InRoom)
			{
				// Master triggers the network load; PUN auto-delivers it to
				// the joiner. Both peers' next scenes start with a fresh
				// GameManager (roundEnded=false) and fresh MultiplayerSpawner
				// (which will Instantiate fresh players via the existing
				// Slice 5 phase 2a flow). Non-master peers no-op here; their
				// scene transitions when the load arrives.
				if (PhotonNetwork.IsMasterClient)
				{
					PhotonNetwork.LoadLevel(gameSceneName);
				}
			}
			else
			{
				SceneManager.LoadScene(gameSceneName);
			}
		}

		// I moved all the action
		private void endGame(int winPlayerId)
		{
			IEnumerator couroutine = waitThenEndGame(winPlayerId);
			StartCoroutine(couroutine);
			Destroy(_gameState.scoreKeeper.gameObject); // so the scores don't stay for the next level
		}
		
		IEnumerator waitThenEndGame(int winPlayerId)
		{
			yield return new WaitForSeconds(secondsToNewRound);
			_endGameMenu.SetActive(true);
			EndMenuManager menuManager = _endGameMenu.GetComponent<EndMenuManager>();
			menuManager.setAnimation(winPlayerId);
			Destroy(_audioSource); // This is here so the audio will stop only after the menu appeared (because the menu has its own audio)
		}
		
		IEnumerator startCountDown()
		{
			disablePlayerControls(true);
//			AudioSource audioSource = _audioSource.GetComponent<AudioSource>(); // used to also stop bg music
//			audioSource.Stop();

			CountDownSFX _countDownSfx = _countDownAnimation.GetComponent<CountDownSFX>();
			
			yield return new WaitForSeconds(0.5f);
			_countDownAnimation.SetActive(true);
			
			yield return new WaitForSeconds(0.3f);
			_countDownSfx.PlayBeep();
			
//			yield return new WaitForSeconds(2.5f); // used to also stop bg music
//			audioSource.Play();
			
			yield return new WaitForSeconds(1f);
			_countDownSfx.PlayBeep();
			
			yield return new WaitForSeconds(1f);
			_countDownSfx.PlayGo();
			
//			yield return new WaitForSeconds(0.5f); // used to also stop bg music
			
			Debug.Log("GameManager: Player controls enabled");
			disablePlayerControls(false);
			
			yield return new WaitForSeconds(0.7f);
			_countDownAnimation.SetActive(false);
		}

		private void disablePlayerControls(bool status)
		{
			foreach (var player in _gameState.players)
			{
				player.GetComponent<PlayerManager>().DisableControls(status);
			}
		}

		public void TogglePauseMenu()
		{
			Debug.Log("GAMEMANAGER:: TogglePauseMenu");
			if (_pause_menu == null) {
				return;
			}
			
			AudioSource audioSource = _audioSource.GetComponent<AudioSource>(); // used to also stop bg music
			
			// not the optimal way but for the sake of readability
			if (_pause_menu.activeSelf)
			{
				audioSource.Play();
				_pause_menu.SetActive(false);
				Time.timeScale = 1.0f;
				
			}
			else
			{
				audioSource.Pause();
				_pause_menu.SetActive(true);
				Time.timeScale = 0f;
				
			}

			Debug.Log("GAMEMANAGER:: TimeScale: " + Time.timeScale);
		}

		public void RestartMusic()
		{
			AudioSource audioSource = _audioSource.GetComponent<AudioSource>();
			audioSource.Stop();
			audioSource.Play();
		}
	}
}
