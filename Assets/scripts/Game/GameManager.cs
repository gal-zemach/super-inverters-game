using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Multiplayer;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

//using System;

namespace Game {

	public enum Framework {GREY, BLACK, WHITE}

	// Inherits MonoBehaviourPunCallbacks so we can call photonView.RPC and receive
	// OnLeftRoom for networked menu exit. Game.prefab needs a PhotonView component
	// (added 2026-05-07).
	public class GameManager : MonoBehaviourPunCallbacks {

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
		// Master-authoritative latch: true once the master has declared a winner,
		// so a near-simultaneous second final kill can't declare a second winner.
		// Reset on scene load (Awake) so rematch / next round starts fresh.
		private bool _matchOver;
		private bool _countdownRunning;
		private Coroutine _rejoinCountdownCoroutine;
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
		private static bool s_pendingExitToMainMenu;
		private const string MainMenuSceneName = "main_menu";
		
		
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
			_matchOver = false;
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

			// PUN default (-1) does not dispatch in LateUpdate when timeScale==0,
			// so pause/resume RPCs never reach the still-paused peer.
			PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = 1f;
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
			return SceneManager.GetActiveScene().name == Multiplayer.MultiplayerSceneNames.GameSceneName;
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

		// Master waits until both networked avatars exist, then RPCs all peers to run
		// the same countdown (fixes host timing out before joiner's scene loads).
		private IEnumerator MultiplayerCountdownLoop()
		{
			if (!countDown || _countDownAnimation == null)
			{
				Debug.LogWarning("GameManager: Multiplayer countdown disabled (countDown off or CountDownAnimation missing).");
				yield break;
			}

			yield return null;

			// Scene Start() can run before PUN finishes joining the room; never treat
			// !IsMasterClient as "joiner" until we are actually in a room.
			float joinRoomElapsed = 0f;
			while (!PhotonNetwork.InRoom && joinRoomElapsed < WaitForPlayersTimeoutSeconds)
			{
				joinRoomElapsed += Time.deltaTime;
				yield return null;
			}

			if (!PhotonNetwork.InRoom)
			{
				Debug.LogWarning("GameManager: Multiplayer countdown skipped (not in a Photon room).");
				yield break;
			}

			if (PhotonNetwork.IsMasterClient)
				yield return MasterWaitThenBroadcastCountdown();
			else
				yield return WaitForSyncedCountdownRpc();
		}

		private IEnumerator WaitForSyncedCountdownRpc()
		{
			float waitElapsed = 0f;
			CountdownActive = false;
			while (!s_matchStartCountdownPlayed)
			{
				waitElapsed += Time.deltaTime;

				if (IsRoomFull() && waitElapsed > WaitForPlayersTimeoutSeconds)
				{
					Debug.LogWarning("GameManager: countdown RPC not received — match start aborted.");
					yield break;
				}

				yield return null;
			}
		}

		private IEnumerator MasterWaitThenBroadcastCountdown()
		{
			CountdownActive = false;
			while (!IsReadyForMatchCountdown())
				yield return null;

			if (_countdownRunning || s_matchStartCountdownPlayed) yield break;

			if (photonView != null)
			{
				Debug.Log($"GameManager: room full, {CountNetworkedPlayersInScene()} avatars — syncing countdown via RPC.");
				photonView.RPC(nameof(RPCStartMatchCountdown), RpcTarget.All);
			}
			else
			{
				Debug.LogWarning("GameManager: No PhotonView — starting countdown locally only.");
				RPCStartMatchCountdown();
			}
		}

		private static bool IsRoomFull()
		{
			return PhotonNetwork.InRoom
			       && PhotonNetwork.CurrentRoom.PlayerCount >= MultiplayerPlayerCount;
		}

		private static bool IsReadyForMatchCountdown()
		{
			return IsRoomFull() && CountNetworkedPlayersInScene() >= MultiplayerPlayerCount;
		}

		private static void ResetLocalPlayersToSpawn(bool forceNewPick = false)
		{
			var spawner = UnityEngine.Object.FindObjectOfType<Multiplayer.MultiplayerSpawner>();
			spawner?.ResetLocalPlayerToSpawnPosition(forceNewPick);
		}

		public override void OnPlayerEnteredRoom(Player newPlayer)
		{
			if (!IsMultiplayerLevel() || !PhotonNetwork.IsMasterClient) return;
			if (!s_matchStartCountdownPlayed) return;

			// Host may be paused while sharing the room link — resume immediately so
			// RPCs dispatch and the rejoin countdown can sync with the joiner.
			ForceDismissInGamePauseMenu();

			if (_rejoinCountdownCoroutine != null)
				StopCoroutine(_rejoinCountdownCoroutine);
			_rejoinCountdownCoroutine = StartCoroutine(MasterWaitThenBroadcastRejoinCountdown());
		}

		private IEnumerator MasterWaitThenBroadcastRejoinCountdown()
		{
			if (!countDown || _countDownAnimation == null)
			{
				_rejoinCountdownCoroutine = null;
				yield break;
			}

			PlatformMotionEpoch = -1;
			float waitElapsed = 0f;
			while (!IsReadyForMatchCountdown())
			{
				waitElapsed += Time.unscaledDeltaTime;
				if (waitElapsed > WaitForPlayersTimeoutSeconds)
				{
					Debug.LogWarning("GameManager: rejoin countdown aborted — second player not ready in time.");
					_rejoinCountdownCoroutine = null;
					yield break;
				}
				yield return null;
			}

			if (_countdownRunning)
			{
				_rejoinCountdownCoroutine = null;
				yield break;
			}

			Debug.Log("GameManager: player rejoined — syncing respawn + countdown.");
			ForceDismissInGamePauseMenu();
			if (photonView != null)
				photonView.RPC(nameof(RPCStartRejoinCountdown), RpcTarget.AllViaServer);
			else
				RPCStartRejoinCountdown();

			_rejoinCountdownCoroutine = null;
		}

		[PunRPC]
		private void RPCStartRejoinCountdown()
		{
			if (_countdownRunning) return;

			s_matchStartCountdownPlayed = true;
			ForceDismissInGamePauseMenu();
			PlatformMotionEpoch = -1;
			CountdownActive = false;
			ResetLocalPlayersToSpawn(forceNewPick: true);
			StartCoroutine(startCountDown());
		}

		[PunRPC]
		private void RPCStartMatchCountdown()
		{
			if (s_matchStartCountdownPlayed || _countdownRunning) return;
			s_matchStartCountdownPlayed = true;

			ResetLocalPlayersToSpawn();
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
					rb.linearVelocity = Vector2.zero;
					rb.simulated = false;
				}
				else if (pv == null || pv.IsMine)
				{
					rb.simulated = true;
				}
			}
		}

		private static void ForEachLocalPlayer(System.Action<GameObject> action)
		{
			foreach (var go in GameObject.FindGameObjectsWithTag(Values.PLAYER_TAG))
			{
				var pv = go.GetComponentInChildren<PhotonView>();
				if (PhotonNetwork.InRoom && pv != null && !pv.IsMine) continue;
				action(go);
			}
		}

		private static void SetLocalPlayerGameplayEnabled(bool enabled)
		{
			ForEachLocalPlayer(go =>
			{
				var pm = go.GetComponent<PlayerManager>();
				if (pm != null)
				{
					pm.DisableControls(!enabled);
					if (!enabled) pm.ResetShootState();
				}

				var rb = go.GetComponent<Rigidbody2D>();
				if (rb == null) return;
				if (!enabled)
				{
					rb.linearVelocity = Vector2.zero;
					rb.simulated = false;
				}
				else
				{
					rb.simulated = true;
				}
			});
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
			// A local exit-to-menu tears the avatar down via PhotonNetwork.Destroy;
			// deactivating its collider fires the bounds OnTriggerExit2D and lands
			// here as a phantom death MID-CLEANUP — kill RPCs, score decrement and
			// a respawn coroutine all racing LeaveRoom (froze the editor). The exit
			// flag is set before the teardown, so it gates exactly that window.
			if (s_pendingExitToMainMenu) return;

			// Networked-instantiated objects get a "(Clone)" suffix on their
			// name; strip it so the score / win-condition logic (which keys
			// off the literal strings "BlackPlayer" / "WhitePlayer") matches
			// in both single-player and multiplayer.
			string playerName = killedPlayer.name.Replace("(Clone)", "").Trim();

			if (PhotonNetwork.InRoom)
			{
				// Master-authoritative kill handling. The dying peer only REPORTS
				// the death to the master; the master owns the score, decides
				// round-respawn vs. game-over, and broadcasts the authoritative
				// result to everyone. Previously each peer decremented its own
				// ScoreKeeper and decided game-over independently, so the two
				// peers could drift and disagree on who lost (one logged
				// "BlackPlayer Lost", the other "WhitePlayer Lost").
				photonView.RPC(nameof(RPCReportKillToMaster), RpcTarget.MasterClient, playerName);
				return;
			}

			DoPlayerKilled(playerName);
		}

		// Master only: apply the kill to the authoritative score, then broadcast
		// the resulting lives count + winner (if any) to every peer so all clients
		// end the match together for the same winner.
		[PunRPC]
		private void RPCReportKillToMaster(string killedPlayerName)
		{
			if (!PhotonNetwork.IsMasterClient) return;
			if (_matchOver) return;  // ignore a near-simultaneous second final kill
			if (CountdownActive)
			{
				// Countdown end is per-peer coroutine timing, so the victim's
				// protection can lift a few frames before the master's. A death
				// reported in that window must still respawn the victim — they
				// are out of bounds and only respawn when a result comes back.
				// Discount the score, but don't drop the respawn.
				photonView.RPC(nameof(RPCRespawnWithoutScore), RpcTarget.All, killedPlayerName);
				return;
			}

			_gameState.decreaseScore(killedPlayerName);
			int remainingLives = _gameState.getScore(killedPlayerName);

			int winPlayerId = -1;
			if (remainingLives <= 0 && _endGameMenu != null)
			{
				_matchOver = true;
				winPlayerId = WinnerIdFor(killedPlayerName);
			}

			photonView.RPC(nameof(RPCApplyKillResult), RpcTarget.All,
				killedPlayerName, remainingLives, winPlayerId);
		}

		// Every peer (incl. master): force the local score to the master's
		// authoritative value, refresh the HUD, then either end the match for the
		// master-chosen winner or respawn the local player for a round death.
		[PunRPC]
		private void RPCApplyKillResult(string killedPlayerName, int remainingLives, int winPlayerId)
		{
			_gameState.setScore(killedPlayerName, remainingLives);
			_gameView.decreaseScore(killedPlayerName); // animated -1 life
			_gameView.updateScore();                   // hard-correct any drift to the authoritative count

			if (winPlayerId > 0 && _endGameMenu != null)
			{
				_matchOver = true;
				Debug.Log(killedPlayerName + " Lost");
				endGame(winPlayerId);
			}
			else
			{
				HandleMultiplayerRoundDeath(killedPlayerName);
			}
		}

		// A kill the master discounted (arrived during its countdown): no score
		// change, no HUD animation — just get the out-of-bounds victim back on
		// a platform so the match can't softlock.
		[PunRPC]
		private void RPCRespawnWithoutScore(string killedPlayerName)
		{
			HandleMultiplayerRoundDeath(killedPlayerName);
		}

		// White (id 2) wins when Black runs out of lives; Black (id 1) wins when White does.
		private static int WinnerIdFor(string killedPlayerName)
		{
			if (killedPlayerName == "BlackPlayer") return 2;
			if (killedPlayerName == "WhitePlayer") return 1;
			return 0;
		}

		// Single-player kill path. (Multiplayer goes through the master-authoritative
		// RPCReportKillToMaster / RPCApplyKillResult pair above.)
		private void DoPlayerKilled(string killedPlayerName)
		{
			if (CountdownActive) return;

			if (roundEnded) return;  // avoid double reload when two players die at once
			roundEnded = true;

			_gameState.decreaseScore(killedPlayerName);
			_gameView.decreaseScore(killedPlayerName);

			// added _endGameMenu null check for testing purposes, so if you don't have the end game menu you can keep playing forever.
			if (_gameState.hasNoLives(killedPlayerName) && _endGameMenu != null)
			{
				int winPlayerId = WinnerIdFor(killedPlayerName);
				Debug.Log(killedPlayerName + " Lost");
				endGame(winPlayerId);
			}
			else
			{
				Debug.Log("reloading level");
				StartCoroutine(waitThenReloadGame());
			}
		}

		private void HandleMultiplayerRoundDeath(string killedPlayerName)
		{
			var spawner = FindObjectOfType<Multiplayer.MultiplayerSpawner>();
			if (spawner != null && spawner.IsLocalPlayer(killedPlayerName))
			{
				spawner.ForceRespawn();
			}
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

		private const float SpawnPlatformRayDistance = 12f;

		// Paint the platform under a player spawn so respawns don't fall through
		// a platform that was shot to the opposite color during the round.
		public void EnsureSpawnPlatformMatchesPlayer(Framework playerFramework, Vector2 spawnWorldPos, bool forceRepaint = false)
		{
			PlatformManager platform = FindPlatformBelow(spawnWorldPos);
			if (platform == null)
			{
				Debug.LogWarning(
					$"GameManager: no platform under spawn {spawnWorldPos} for {playerFramework}.");
				return;
			}

			var state = platform.GetComponent<PlatformState>();
			if (!forceRepaint && state != null && state.platform_framework == playerFramework)
				return;

			platform.ApplyPaintFromNetwork(playerFramework);
			if (PhotonNetwork.InRoom && platform.networkId >= 0)
				BroadcastPaintPlatform(platform.networkId, playerFramework);
		}

		/// <summary>True from MP scene load until GO — blocks out-of-bounds kills during spawn/countdown.</summary>
		public static bool IsMatchStartProtectionActive =>
			PhotonNetwork.InRoom && (CountdownActive || PlatformMotionEpoch < 0);

		private static PlatformManager FindPlatformBelow(Vector2 worldPos)
		{
			PlatformManager best = null;
			float bestDist = float.MaxValue;

			foreach (var hit in Physics2D.RaycastAll(worldPos, Vector2.down, SpawnPlatformRayDistance))
			{
				var platform = hit.collider.GetComponentInParent<PlatformManager>();
				if (platform == null) continue;
				if (hit.distance < bestDist)
				{
					bestDist = hit.distance;
					best = platform;
				}
			}

			if (best != null) return best;

			// Spawn point can sit slightly inside the collider; try upward as well.
			foreach (var hit in Physics2D.RaycastAll(worldPos, Vector2.up, 2f))
			{
				var platform = hit.collider.GetComponentInParent<PlatformManager>();
				if (platform == null) continue;
				if (hit.distance < bestDist)
				{
					bestDist = hit.distance;
					best = platform;
				}
			}

			return best;
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

		public void RequestNetworkedExitToMainMenu()
		{
			DismissEndGamePresentation();
			RestoreGameplayTimeScale();

			if (PhotonNetwork.InRoom && photonView != null)
			{
				photonView.RPC(nameof(RPCExitToMainMenu), RpcTarget.All);
				return;
			}

			s_pendingExitToMainMenu = true;
			LoadMainMenuScene();
		}

		public void RequestLocalExitToMainMenu()
		{
			PerformExitToMainMenuCleanup();
		}

		[PunRPC]
		private void RPCExitToMainMenu()
		{
			PerformExitToMainMenuCleanup();
		}

		private void PerformExitToMainMenuCleanup()
		{
			DismissEndGamePresentation();
			RestoreGameplayTimeScale();
			s_pendingExitToMainMenu = true;
			s_matchStartCountdownPlayed = false;
			roundEnded = false;

			var spawner = FindObjectOfType<MultiplayerSpawner>();
			spawner?.ResetSessionSpawnState();
			MultiplayerColorAssignment.ClearLocalColorClaim();

			if (PhotonNetwork.InRoom)
				PhotonNetwork.LeaveRoom();
			else
				LoadMainMenuScene();
		}

		public override void OnLeftRoom()
		{
			if (!s_pendingExitToMainMenu) return;
			s_pendingExitToMainMenu = false;
			LoadMainMenuScene();
		}

		// The synced pause menu sets timeScale=0 on BOTH peers, but a local exit
		// (pause menu → Exit to Main Menu) only cleans up the leaving peer. Without
		// this, the remaining peer stays frozen on the pause menu with no way to
		// know the opponent is gone.
		public override void OnPlayerLeftRoom(Player otherPlayer)
		{
			if (!IsMultiplayerLevel()) return;
			ForceDismissInGamePauseMenu();
		}

		private void RestoreGameplayTimeScale()
		{
			ForceDismissInGamePauseMenu();
		}

		private void ForceDismissInGamePauseMenu()
		{
			LocalPauseActive = false;
			Time.timeScale = 1f;
			SetLocalPlayerGameplayEnabled(true);
			SetBackgroundMusicPaused(false);
			if (_pause_menu != null)
				_pause_menu.SetActive(false);
		}

		private static void LoadMainMenuScene()
		{
			s_matchStartCountdownPlayed = false;
			s_pendingExitToMainMenu = false;
			CountdownActive = false;
			SceneManager.LoadScene(MainMenuSceneName);
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
			CountdownActive = false;
			SetLocalPlayerGameplayEnabled(true);
			IEnumerator couroutine = waitThenEndGame(winPlayerId);
			StartCoroutine(couroutine);
			Destroy(_gameState.scoreKeeper.gameObject); // so the scores don't stay for the next level
		}
		
		IEnumerator waitThenEndGame(int winPlayerId)
		{
			yield return new WaitForSeconds(secondsToNewRound);
			Time.timeScale = 1f;
			SetLocalPlayerGameplayEnabled(false);
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

		public static bool LocalPauseActive { get; private set; }

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
			SetLocalPlayerGameplayEnabled(!status);
		}

		private bool IsInGamePauseMenuOpen =>
			_pause_menu != null && _pause_menu.activeSelf;

		public void ToggleInGamePauseMenu()
		{
			SetInGamePauseMenuOpen(!IsInGamePauseMenuOpen);
		}

		private void SetInGamePauseMenuOpen(bool open)
		{
			if (PhotonNetwork.InRoom && photonView != null)
			{
				if (open)
				{
					photonView.RPC(nameof(RPCSetInGamePauseMenuOpen), RpcTarget.AllViaServer, true);
				}
				else
				{
					// Restore timeScale locally so the outgoing RPC can dispatch,
					// then sync close to every peer via the server.
					ApplyInGamePauseMenuOpen(false);
					photonView.RPC(nameof(RPCSetInGamePauseMenuOpen), RpcTarget.AllViaServer, false);
				}
				return;
			}

			ApplyInGamePauseMenuOpen(open);
		}

		[PunRPC]
		private void RPCSetInGamePauseMenuOpen(bool open)
		{
			ApplyInGamePauseMenuOpen(open);
		}

		private void ApplyInGamePauseMenuOpen(bool open)
		{
			if (open)
				OpenInGamePauseMenu();
			else
				CloseInGamePauseMenu();
		}

		public void OpenInGamePauseMenu()
		{
			if (_pause_menu == null) return;
			if (IsInGamePauseMenuOpen) return;

			LocalPauseActive = true;
			SetBackgroundMusicPaused(true);
			_pause_menu.SetActive(true);
			Time.timeScale = 0f;

			var pauseUi = _pause_menu.GetComponent<InGamePauseMenu>();
			if (pauseUi == null)
				pauseUi = _pause_menu.AddComponent<InGamePauseMenu>();
			pauseUi.Refresh();
		}

		public void CloseInGamePauseMenu()
		{
			if (_pause_menu == null || !IsInGamePauseMenuOpen) return;

			ForceDismissInGamePauseMenu();
		}

		public void TogglePauseMenu()
		{
			ToggleInGamePauseMenu();
		}

		private void SetBackgroundMusicPaused(bool paused)
		{
			if (_audioSource == null) return;
			var audioSource = _audioSource.GetComponent<AudioSource>();
			if (audioSource == null) return;

			if (paused)
				audioSource.Pause();
			else
				audioSource.Play();
		}

		public void RestartMusic()
		{
			// _audioSource is destroyed by waitThenEndGame at game over (and may be
			// disabled by muteMusicForTesting) — this can be reached from the end
			// menu's gamepad path afterwards.
			if (_audioSource == null) return;
			AudioSource audioSource = _audioSource.GetComponent<AudioSource>();
			if (audioSource == null) return;
			audioSource.Stop();
			audioSource.Play();
		}
	}
}
