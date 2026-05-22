using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

		[SerializeField, Tooltip("Disable the scene's Audio Source GameObject in Awake. Useful during multiplayer development so the music doesn't restart on every PhotonNetwork.LoadLevel reload. Leave unchecked for shipping / single-player.")]
		public bool muteMusicForTesting = false;


		private bool roundEnded;
		private bool _countdownRunning;
		private GameObject _endGameMenu;
		private GameObject _audioSource;
		private GameObject _countDownAnimation;
		private GameObject _pause_menu;

		private const int MultiplayerPlayerCount = 2;
		private const float WaitForPlayersTimeoutSeconds = 10f;

		// Survives PhotonNetwork.LoadLevel round reloads while still in the room.
		// Mid-round reloads re-run Start() but must not replay Ready/Set/Fight.
		// Cleared on rematch (Replay) and when not in a room (lobby / disconnect).
		private static bool s_matchStartCountdownPlayed;
		
		
		void Awake ()
		{
			// Reset the static countdown flag on every scene load so it can't
			// leak across reloads (would otherwise stay true forever if a
			// scene reloads mid-coroutine).
			CountdownActive = false;
			_countdownRunning = false;
			if (!PhotonNetwork.InRoom)
			{
				s_matchStartCountdownPlayed = false;
				PlatformMotionEpoch = -1;
			}
			else
			{
				PlatformMotionEpoch = -1;
			}

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
			if (_audioSource != null && muteMusicForTesting)
			{
				_audioSource.SetActive(false);
			}

			_countDownAnimation = GameObject.Find(Values.COUNTDOWN_ANIM_GAMEOBJ_NAME);
			if (_countDownAnimation != null)
			{
				_countDownAnimation.SetActive(false);
			}
		}

		private void Start()
		{
			AssignPlatformNetworkIds();

			// Never use the single-player countdown path on MP levels — that scene
			// has countDownEveryRound=1 and would start the countdown before
			// PhotonNetwork.Instantiate spawns anyone when InRoom is briefly false.
			if (IsMultiplayerLevel())
			{
				// Round reload (death → LoadLevel) must not restart countdown;
				// level_1-multiplayer has countDownEveryRound=1 for SP semantics only.
				if (!s_matchStartCountdownPlayed)
				{
					StartCoroutine(MultiplayerCountdownLoop());
				}
				return;
			}

			TryStartCountdownSinglePlayer();
		}

		private static bool IsMultiplayerLevel()
		{
			return Object.FindObjectOfType<Multiplayer.MultiplayerSpawner>() != null;
		}

		private void TryStartCountdownSinglePlayer()
		{
			if (countDown && _countDownAnimation != null)
			{
				if (countDownEveryRound || _gameState.isGameStart())
				{
					if (_countdownRunning) return;
					Debug.Log("GameManager: Paying CountDown");
					StartCoroutine(startCountDown());
				}
			}
		}

		// Each peer starts the countdown locally once both networked avatars exist in
		// the scene (no RPC — avoids joiner missing a 2s readiness window).
		private IEnumerator MultiplayerCountdownLoop()
		{
			if (!countDown || _countDownAnimation == null)
			{
				Debug.LogWarning("GameManager: Multiplayer countdown disabled (countDown off or CountDownAnimation missing).");
				yield break;
			}

			yield return null;

			float elapsed = 0f;
			while (CountNetworkedPlayersInScene() < MultiplayerPlayerCount && elapsed < WaitForPlayersTimeoutSeconds)
			{
				// Input-only lock while waiting for the second peer. Do NOT disable
				// Rigidbody2D simulation here — spawn Y is above the platform and
				// simulated=false leaves the avatar hovering in mid-air (log 5efe84).
				if (CountNetworkedPlayersInScene() > 0)
				{
					CountdownActive = true;
				}

				elapsed += Time.deltaTime;
				yield return null;
			}

			if (CountNetworkedPlayersInScene() < MultiplayerPlayerCount)
			{
				CountdownActive = false;
				Debug.LogWarning(
					$"GameManager: Timed out before countdown ({CountNetworkedPlayersInScene()}/{MultiplayerPlayerCount} networked players).");
				yield break;
			}

			if (_countdownRunning || s_matchStartCountdownPlayed) yield break;
			s_matchStartCountdownPlayed = true;
			Debug.Log($"GameManager: {CountNetworkedPlayersInScene()} players in scene — starting countdown.");
			StartCoroutine(startCountDown());
		}

		private static int CountNetworkedPlayersInScene()
		{
			int count = 0;
			foreach (var go in GameObject.FindGameObjectsWithTag(Values.PLAYER_TAG))
			{
				if (go.GetComponentInChildren<PhotonView>() != null) count++;
			}
			return count;
		}

		// Freeze every visible player during countdown (local + remote).
		private void SetCountdownPhysicsFrozen(bool frozen)
		{
			foreach (var go in GameObject.FindGameObjectsWithTag(Values.PLAYER_TAG))
			{
				var rb = go.GetComponent<Rigidbody2D>();
				if (rb == null) continue;

				var pv = go.GetComponentInChildren<PhotonView>();
				if (frozen)
				{
					rb.velocity = Vector2.zero;
					rb.simulated = false;
				}
				else if (pv == null || pv.IsMine)
				{
					rb.simulated = true;
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

			// Slice 5 phase 2d: this peer just spawned its own real shot (which
			// flies and paints locally + broadcasts paint via phase 2b). Tell
			// the other peers to spawn a visual-only ghost so they see the shot
			// fly too. Only the owning peer reaches SpawnShot (PlayerManager.shoot
			// gates remote players out in a room), so this never double-fires.
			if (PhotonNetwork.InRoom)
			{
				photonView.RPC(nameof(RPCSpawnGhostShot), RpcTarget.Others,
					position, startVelocity, rotation, (int)framework);
			}
		}

		[PunRPC]
		private void RPCSpawnGhostShot(Vector2 position, Vector2 startVelocity, float rotation, int frameworkInt)
		{
			// Ghost = visual-only; the `true` flag makes the platform collision
			// handlers skip UpdateHit so it doesn't re-paint (paint already
			// arrives via RPCPaintPlatform).
			_shotFactory.MakeObject(position, startVelocity, rotation, (Framework)frameworkInt, true);
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
			if (CountdownActive) return;
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
			else if (PhotonNetwork.InRoom && IsMultiplayerLevel())
			{
				HandleMultiplayerRoundDeath(killedPlayerName);
			}
			else
			{
				Debug.Log("reloading level");
				StartCoroutine(waitThenReloadGame());
			}
		}

		private void HandleMultiplayerRoundDeath(string killedPlayerName)
		{
			var spawner = Object.FindObjectOfType<Multiplayer.MultiplayerSpawner>();
			if (spawner != null && spawner.IsLocalPlayer(killedPlayerName))
			{
				spawner.ForceRespawn();
			}

			StartCoroutine(ReleaseRoundEndedAfterDelay());
		}

		private IEnumerator ReleaseRoundEndedAfterDelay()
		{
			yield return new WaitForSeconds(secondsToNewRound);
			roundEnded = false;
		}

		// --- Slice 5 phase 2b: networked platform paint ---------------------
		// Each peer's local shots only exist on the firing peer's machine, so
		// only the shooter's PlatformShotSensor detects the collision and
		// triggers UpdateHit -> UpdateFramework. The other peer's view of the
		// same platform stays the original color, which causes physics
		// divergence (one peer falls through, the other doesn't).
		//
		// Fix: when a platform actually flips color (UpdateHit's threshold
		// path), the shooter's PlatformManager calls BroadcastPaintPlatform
		// with the platform's deterministic networkId. RPCPaintPlatform fires
		// on remote peers and re-applies the color + collision-layer change
		// without re-broadcasting.
		//
		// Platform networkIds are assigned at scene start by sorting all
		// PlatformManager instances by initial position (x then y). Both
		// peers run the same sort on the same scene, so each platform has
		// the same id on every peer.

		private Dictionary<int, PlatformManager> _platformsById = new Dictionary<int, PlatformManager>();

		private void AssignPlatformNetworkIds()
		{
			// Sort by hierarchy path (full GameObject path from scene root)
			// rather than by position + InstanceID. The path is identical on
			// both peers because they load the same scene file; InstanceID
			// is process-local and DIFFERS between Unity instances, which
			// caused id drift for platforms at identical positions —
			// peer A's "id 7" pointed at a different physical platform than
			// peer B's "id 7", so paint RPCs landed on the wrong platform.
			var sorted = FindObjectsOfType<PlatformManager>()
				.OrderBy(p => GetHierarchyPath(p.transform), System.StringComparer.Ordinal)
				.ToArray();

			_platformsById.Clear();
			for (int i = 0; i < sorted.Length; i++)
			{
				sorted[i].networkId = i;
				_platformsById[i] = sorted[i];
				Debug.Log($"[Platform IDs] {i}: {GetHierarchyPath(sorted[i].transform)} at {sorted[i].transform.position}");
			}
		}

		private static string GetHierarchyPath(Transform t)
		{
			var sb = new System.Text.StringBuilder();
			while (t != null)
			{
				if (sb.Length > 0) sb.Insert(0, "/");
				sb.Insert(0, t.name);
				t = t.parent;
			}
			return sb.ToString();
		}

		public void BroadcastPaintPlatform(int platformNetworkId, Framework framework)
		{
			if (!PhotonNetwork.InRoom) return;
			if (platformNetworkId < 0) return;
			// Plain Others (not OthersBuffered) — buffered RPCs would replay
			// onto the fresh scene after a PhotonNetwork.LoadLevel reload,
			// pre-painting platforms before the new round starts.
			photonView.RPC(nameof(RPCPaintPlatform), RpcTarget.Others, platformNetworkId, (int)framework);
		}

		[PunRPC]
		private void RPCPaintPlatform(int platformNetworkId, int frameworkInt)
		{
			if (!_platformsById.TryGetValue(platformNetworkId, out var platform))
			{
				Debug.LogWarning($"GameManager: RPCPaintPlatform got unknown id {platformNetworkId}.");
				return;
			}
			platform.ApplyPaintFromNetwork((Framework)frameworkInt);
		}

		IEnumerator waitThenReloadGame()
		{
			yield return new WaitForSeconds(secondsToNewRound);
			ReloadMatchScene();
		}

		// End-game menu Replay button → SceneLoader.ReloadCurrentScene → here.
		// In multiplayer, SceneManager.LoadScene only reloads the peer that
		// pressed the button; the other stays on the end-game menu. Mirror
		// waitThenReloadGame: RPC so every peer calls PhotonNetwork.LoadLevel.
		public void RequestNetworkedReplay()
		{
			DismissEndGamePresentation();
			if (PhotonNetwork.InRoom && photonView != null)
			{
				photonView.RPC(nameof(RPCReplayMatch), RpcTarget.All);
				return;
			}
			PrepareScoresForRematch();
			roundEnded = false;
			ReloadMatchScene();
		}

		[PunRPC]
		private void RPCReplayMatch()
		{
			DismissEndGamePresentation();
			PrepareScoresForRematch();
			roundEnded = false;
			ReloadMatchScene();
		}

		private void DismissEndGamePresentation()
		{
			if (_endGameMenu == null) return;
			var endAudioGo = _endGameMenu.transform.Find("EndGameAudio");
			if (endAudioGo != null)
			{
				var src = endAudioGo.GetComponent<AudioSource>();
				if (src != null) src.Stop();
			}
			_endGameMenu.SetActive(false);
		}

		private void PrepareScoresForRematch()
		{
			s_matchStartCountdownPlayed = false;
			var keeper = ScoreKeeper.getInstance;
			if (keeper != null)
			{
				keeper.clearScores();
			}
		}

		private void ReloadMatchScene()
		{
			string sceneToLoad = string.IsNullOrEmpty(gameSceneName)
				? SceneManager.GetActiveScene().name
				: gameSceneName;

			if (PhotonNetwork.InRoom)
			{
				PhotonNetwork.LoadLevel(sceneToLoad);
			}
			else
			{
				SceneManager.LoadScene(sceneToLoad);
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
		
		// Global flag every PlayerManager checks each frame. Used in addition
		// to the per-player disablePlayerControls/_gameState.players path
		// because in multiplayer the players are PhotonNetwork.Instantiate'd
		// at runtime and aren't in _gameState.players at GameState.Awake time,
		// so the per-player disable was a no-op there. Reset to false at
		// scene load (Awake — see below) so it doesn't leak across reloads.
		public static bool CountdownActive { get; private set; }

		// Photon room time when moving platforms may advance. -1 = frozen (MP wait / countdown).
		// Both peers derive platform pose from (PhotonNetwork.Time - epoch) so join latency
		// does not leave the host's platforms ahead of the guest's.
		public static double PlatformMotionEpoch { get; private set; } = -1;

		[PunRPC]
		private void RPCSetPlatformMotionEpoch(double epoch)
		{
			PlatformMotionEpoch = epoch;
		}

		private void BeginPlatformMotionAtGo()
		{
			if (!PhotonNetwork.InRoom || photonView == null) return;
			if (PhotonNetwork.IsMasterClient)
			{
				photonView.RPC(nameof(RPCSetPlatformMotionEpoch), RpcTarget.All, PhotonNetwork.Time);
			}
		}

		IEnumerator startCountDown()
		{
			if (_countdownRunning) yield break;
			_countdownRunning = true;
			CountdownActive = true;
			SetCountdownPhysicsFrozen(true);
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
			CountdownActive = false;
			SetCountdownPhysicsFrozen(false);
			BeginPlatformMotionAtGo();
			_countdownRunning = false;

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
