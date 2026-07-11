using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Controllers;
using Photon.Pun;
using Utils.Utils;


namespace Game{
	[DefaultExecutionOrder(100)]
	public class PlayerManager : MonoBehaviour
	{
		private PlayerView _playerView;

		private PlayerState _playerState;

		private Controller[] controllers;
		private MouseAimController _mouseAim;
		
		[SerializeField]
		public bool EnableSFX = false;

		private PlayerSFX _sfx;
		

		[Header("Physics")]
		[SerializeField, Tooltip("The player's speed")]
		float maxXVelocity = 17;
		public float VelocityFactor = 40f; // need to get rid of this but maybe some other time :)

		//	[SerializeField, Tooltip("The maximum speed in y axis during falling")]
		private float MaxFallingVelocity = 40;

		[SerializeField, Tooltip("Lower value makes the player switch direction slower")]
		public float turnRate = 1.5f;

		[SerializeField, Tooltip("Lower value makes the player slide more on floors\n Value of 1 adds nothing")]
		public float bonusFriction = 1.05f;

		[SerializeField]
		public int jumpHeight = 700;

		[HideInInspector] bool doubleJumpEnabled = true; // used to be relevant in inspector
		[SerializeField, Tooltip("The higher the value, the sooner the player can doubleJump")]
		public float minimalDoubleJumpVelocity = 17f;
		
		public float RegularGravityScale = 1;
		public float FallingGravityScale = 1;

		[SerializeField, Tooltip("Physics frames between shots. Lower = faster (2 ≈ very high rate at 50 Hz).")]
		private int turnsBetweenShots = 2;

		[SerializeField, Tooltip("Max seconds of continuous fire per hold before you must release.")]
		private float burstFireDurationSeconds = 0.5f;

		private float InterShotCooldownSeconds => turnsBetweenShots * Time.fixedDeltaTime;

		[SerializeField, Tooltip("This *2 is the shells' random angle range.")]
		public float shellAngleRange = 10f;
		
		[SerializeField, Tooltip("How far the player is pushed back when shooting")]
		public float recoil = 0.12f;

		private Rigidbody2D _rigidbody2D;
		private GameManager _gameManager;

		[Header("for Testing")]
		public Vector2 movingDirection; // public only for testing
		public Vector2 shootingDirection;
		private Vector2 lastNonZeroDirection;
		private int _lastHorizontalAimDir = 1;

		public bool isGrounded;
		private bool canDoubleJump;
		private float jumpRatio = 0.2f;
		private float _nextShootTime;
		private bool _burstActive;
		private float _burstStartTime;
		private float Jump_Y_Threshold = 5f;

		/// <summary>1 = just fired (inter-shot cooldown active), 0 = ready for next shot in burst.</summary>
		public float ShootCooldown01
		{
			get
			{
				float cd = InterShotCooldownSeconds;
				if (cd <= 0f) return 0f;
				return Mathf.Clamp01((_nextShootTime - Time.time) / cd);
			}
		}

		/// <summary>1 = full burst available, 0 = burst exhausted until fire is released.</summary>
		public float BurstAmmo01
		{
			get
			{
				if (burstFireDurationSeconds <= 0f) return 1f;
				if (!_burstActive) return 1f;
				float elapsed = Time.time - _burstStartTime;
				return Mathf.Clamp01(1f - elapsed / burstFireDurationSeconds);
			}
		}

		// Used for checking if player is grounded
		private Transform overlap_topLeft, overlap_bottomRight;
		private int overlap_layersMask;
		private Collider2D[] _overlap_colliders = new Collider2D[10];

		[SerializeField, Tooltip("Player can't die\n(but can fall to infinity)")]
		public bool invincible = false;

		// for testing
		public Vector2 currentVelocity;
		
		public bool debugMode;
		private PlayerLog eventLog;

		private bool controlsDisabled = false;
		
		void Awake()
		{
			_playerState = GetComponent<PlayerState>();
			_playerView = GetComponent<PlayerView>();

			_gameManager = GetComponentInParent<GameManager>();
			if (_gameManager == null) _gameManager = FindObjectOfType<GameManager>();
			_rigidbody2D = GetComponent<Rigidbody2D>();

			controllers = GetComponents<Controller>();
			_mouseAim = GetComponent<MouseAimController>();

			overlap_topLeft = transform.Find(Values.PLAYER_TOP_LEFT_GAMEOBJ_NAME);
			overlap_bottomRight = transform.Find(Values.PLAYER_BOT_RIGHT_GAMEOBJ_NAME);

			_sfx = GetComponentInChildren<PlayerSFX>();
		}


		void Start ()
		{
			_lastHorizontalAimDir = transform.position.x >= 0f ? 1 : -1;
			ConfigureMouseAim();
			_playerView.SetSpriteColor(_playerState.player_framework);

			// layer of platforms for checking if grounded
			if (_playerState.player_framework == Framework.BLACK) overlap_layersMask = LayerMask.GetMask("platforms_black", "floor");
			else if (_playerState.player_framework == Framework.WHITE) overlap_layersMask = LayerMask.GetMask("platforms_white", "floor");

			if (debugMode)
			{
				eventLog = GetComponent<PlayerLog>();
				foreach (var controller in controllers)
				{
					controller.debugModeStatus(true);
				}
			}
		}

		private void Update()
		{
			foreach (var controller in controllers)
			{
				if (!controller.pauseMenu()) continue;

				var pv = GetComponentInChildren<PhotonView>();
				bool isLocal = pv == null || pv.IsMine;
				if (!isLocal) return;

				if (_gameManager != null)
					_gameManager.ToggleInGamePauseMenu();
				return;
			}

			// Per-player disable (single-player path via _gameState.players)
			// OR global countdown flag (works for multiplayer where players
			// are PhotonNetwork.Instantiate'd and aren't in _gameState.players
			// at GameState.Awake time).
			if (controlsDisabled || GameManager.CountdownActive) return;
			
			updateGrounded();
		}

		// After all Controller.Update calls so aim_direction() reflects this frame's input.
		private void LateUpdate()
		{
			if (controlsDisabled || GameManager.CountdownActive) return;

			updateGrounded();

			updateDirection();

			// Jump after aim merge so up-aim + Space stay in sync for animation layers.
			foreach (var controller in controllers)
			{
				var behaviour = controller as MonoBehaviour;
				if (behaviour != null && !behaviour.enabled) continue;
				if (controller.jump())
					tryToJump();
			}

			// Only end jump pose when grounded and not still rising (overlap can stay
			// true for a few frames after takeoff).
			if (isGrounded && _rigidbody2D.linearVelocity.y <= 0f)
			{
				_playerView.isJumping = false;
				_playerView.isDoubleJumping = false;
			}

			_playerView.isGrounded = isGrounded;
			_playerView.ApplyAnimatorState();
		}

		void FixedUpdate()
		{
			if (controlsDisabled || GameManager.CountdownActive) return;

			bool isShootingThisFrame = false;
			bool fireHeldThisFrame = false;

			foreach (var controller in controllers)
			{
				if (controller == null) continue;

				updateGrounded();

				if (controller.getDown())
					getOffPlatform();

				move(movingDirection);

				if (Mathf.Approximately(movingDirection.x, 0) && isGrounded)
					slowHorizontalVelocity(bonusFriction);

				_playerView.changeCrosshairDirection(shootingDirection);

				if (controller.shoot())
				{
					fireHeldThisFrame = true;
					if (TryShoot(shootingDirection))
						isShootingThisFrame = true;
				}

				if (_rigidbody2D.linearVelocity.y < 0) _rigidbody2D.gravityScale = FallingGravityScale;
				else _rigidbody2D.gravityScale = RegularGravityScale;

				if (!isGrounded && _rigidbody2D.linearVelocity.y < -MaxFallingVelocity)
					_rigidbody2D.linearVelocity = new Vector2(_rigidbody2D.linearVelocity.x, -MaxFallingVelocity);

				currentVelocity = _rigidbody2D.linearVelocity;
			}

			if (!fireHeldThisFrame)
				ResetBurstState();

			_playerView.isShooting = isShootingThisFrame;
		}

		private void ResetBurstState()
		{
			_burstActive = false;
			_burstStartTime = 0f;
		}

		private bool CanShoot()
		{
			if (Time.time < _nextShootTime) return false;
			if (_burstActive && Time.time - _burstStartTime >= burstFireDurationSeconds)
				return false;
			return true;
		}


		public bool ControlsDisabled => controlsDisabled;

		public void DisableControls(bool status)
		{
			controlsDisabled = status;
		}

		public void ResetShootState()
		{
			ResetBurstState();
			_nextShootTime = 0f;
		}


		private void ConfigureMouseAim()
		{
			if (_mouseAim == null) return;

			var photonView = GetComponent<PhotonView>();
			bool isLocal = photonView == null || photonView.IsMine;
			bool useMouse = isLocal && ShouldUseMouseAimForPlayer();
			_mouseAim.enabled = useMouse;
		}

		private bool ShouldUseMouseAimForPlayer()
		{
			if (PhotonNetwork.InRoom) return true;
			// Single-player: one mouse — Black player only (White keeps keyboard aim).
			return _playerState.player_framework == Framework.BLACK;
		}

		private void updateDirection()
		{
			bool movingDirChanged = false;
			bool aimDirChanged = false;
			bool anyAimInput = false;
			Vector2 tempAimDir = Vector2.zero;

			if (_mouseAim != null && _mouseAim.enabled)
			{
				tempAimDir = _mouseAim.aim_direction();
				if (tempAimDir != Vector2.zero)
				{
					shootingDirection = tempAimDir;
					aimDirChanged = true;
					anyAimInput = true;
				}
			}
			
			foreach (var controller in controllers)
			{
				if (controller == _mouseAim) continue;
				var behaviour = controller as MonoBehaviour;
				if (behaviour != null && !behaviour.enabled) continue;

				Vector2 tempMovingDir = controller.moving_direction();
				if (tempMovingDir != Vector2.zero)
				{
					movingDirection = tempMovingDir;
					movingDirChanged = true;
				}
				
				if (!aimDirChanged)
				{
					tempAimDir = controller.aim_direction();
					if (tempAimDir != Vector2.zero)
					{
						shootingDirection = tempAimDir;
						aimDirChanged = true;
						anyAimInput = true;
					}
				}
			}
			if (!movingDirChanged) movingDirection = Vector2.zero;
			
			if (!aimDirChanged)
			{
				// movingDirection is horizontal-only; don't replace steep up/down aim on landing.
				if (movingDirection != Vector2.zero && IsSideAimBucket(shootingDirection))
					shootingDirection = movingDirection;
				else if (isGrounded && !anyAimInput)
					shootingDirection = Vector2.zero;
			}
			else if (shootingDirection != Vector2.zero)
			{
				lastNonZeroDirection = shootingDirection;
			}

			Vector2 animAim = shootingDirection;
			if (animAim == Vector2.zero && !isGrounded)
				animAim = lastNonZeroDirection;
			ClassifyAimDirection(animAim, _lastHorizontalAimDir,
				out _playerView.vertical_dir, out _playerView.horizontal_dir);
			if (animAim != Vector2.zero)
				_lastHorizontalAimDir = _playerView.horizontal_dir;

			_playerView.isMoving = isGrounded && !Mathf.Approximately(movingDirection.x, 0);
		}

		// Four aim regions with boundaries at |x| = |y| (a 45°-rotated diamond).
		// Mostly horizontal → side; mostly vertical → up or down.
		private static void ClassifyAimDirection(Vector2 aim, int lastHorizontal,
			out float verticalDir, out int horizontalDir)
		{
			if (aim == Vector2.zero)
			{
				verticalDir = 0f;
				horizontalDir = lastHorizontal;
				return;
			}

			if (Mathf.Abs(aim.x) >= Mathf.Abs(aim.y))
			{
				verticalDir = 0f;
				horizontalDir = aim.x >= 0f ? 1 : -1;
			}
			else if (aim.y > 0f)
			{
				verticalDir = 1f;
				horizontalDir = Mathf.Approximately(aim.x, 0f) ? lastHorizontal : (int)Mathf.Sign(aim.x);
			}
			else
			{
				verticalDir = -1f;
				horizontalDir = Mathf.Approximately(aim.x, 0f) ? lastHorizontal : (int)Mathf.Sign(aim.x);
			}
		}

		private static bool IsSideAimBucket(Vector2 aim)
		{
			if (aim == Vector2.zero) return true;
			ClassifyAimDirection(aim, 1, out float verticalDir, out _);
			return verticalDir == 0f;
		}
		

		private void move(Vector2 direction)
		{
			Vector2 newVelocity = _rigidbody2D.linearVelocity;
			//		newVelocity.x = direction.x * VelocityFactor;
			newVelocity.x = Mathf.Lerp(_rigidbody2D.linearVelocity.x, direction.x * VelocityFactor, -Mathf.Pow(Time.deltaTime, 2) + 1);
			newVelocity.x = Mathf.Clamp(newVelocity.x, -maxXVelocity, maxXVelocity);
			_rigidbody2D.linearVelocity = newVelocity;
		}

		private void updateGrounded()
		{
			isGrounded = Physics2D.OverlapAreaNonAlloc(overlap_topLeft.position, overlap_bottomRight.position,
				             _overlap_colliders, overlap_layersMask) > 0;
		}
		
		private void tryToJump()
		{
			if (isGrounded)
			{
				// This is to avoid chain jumping
				if (_rigidbody2D.linearVelocity.y > Jump_Y_Threshold)
				{
					if (debugModeOn()) eventLog.AddEvent("PlayerManager: Didn't jump. y speed: " + _rigidbody2D.linearVelocity.y);
					return;
				}
				
				DisconnectFromPlatfrom();
				
				_rigidbody2D.linearVelocity += new Vector2(0, jumpHeight);
				if (EnableSFX) _sfx.PlayJump();
				if (debugModeOn()) eventLog.AddEvent("PlayerManager: Jumped.");
				_playerView.isJumping = true;
				canDoubleJump = true;
			}

			// Double jumping
			else if (canDoubleJump)
			{
				// This is to avoid chain jumping
				if (_rigidbody2D.linearVelocity.y > minimalDoubleJumpVelocity)
				{
					if (debugModeOn()) eventLog.AddEvent("PlayerManager: Didn't doubleJump. y speed: " + _rigidbody2D.linearVelocity.y);
					return;
				}
									
				_rigidbody2D.linearVelocity = new Vector2(_rigidbody2D.linearVelocity.x, jumpHeight);
				if (EnableSFX) _sfx.PlayDoubleJump();
				if (debugModeOn()) eventLog.AddEvent("PlayerManager: DoubleJumped");
				_playerView.isDoubleJumping = true;

				canDoubleJump = false;
			}

			else
			{
				if (debugModeOn()) eventLog.AddEvent("PlayerManager: Didn't jump. isGrounded=" + isGrounded + ", canDoubleJump=" + canDoubleJump);
			}
		}

		private bool TryShoot(Vector2 direction)
		{
			if (!CanShoot()) return false;

			// When the player is standing still with no aim input, `direction`
			// is (0, 0) and GetAngle returns 0 → shot fires Vector2.right.
			// Fall back to the player's current facing so the shot goes the
			// way the sprite is pointing.
			if (direction == Vector2.zero)
			{
				direction = _playerView.facingLeft ? Vector2.left : Vector2.right;
			}

			// Slice 5 phase 2d: in a Photon room only the owning peer simulates
			// the real shot (spawn + shell + recoil + the shot's own paint
			// collision). Remote players are driven by replicated input, but
			// their shot is shown via a visual-only ghost spawned through
			// GameManager.RPCSpawnGhostShot — so they must not spawn a second,
			// painting shot here. The shooting animation is still set by the
			// FixedUpdate caller, so the remote player still animates.
			if (PhotonNetwork.InRoom)
			{
				var pv = GetComponent<PhotonView>();
				if (pv != null && !pv.IsMine) return false;
			}

			if (!_burstActive)
			{
				_burstActive = true;
				_burstStartTime = Time.time;
			}

			_nextShootTime = Time.time + InterShotCooldownSeconds;

			Vector2 pos = _rigidbody2D.position;
			if (EnableSFX) _sfx.PlayShoot();
			float shooting_angle = direction.GetAngle();
			_gameManager.SpawnShot(pos, _rigidbody2D.linearVelocity, shooting_angle, _playerState.player_framework);

//			float shell_rotation = (-direction + Vector2.up*1.5f).GetAngle();
			float shell_rotation_angle = (shooting_angle >= 90 || shooting_angle < -90) ? -90.0f : 90.0f;
			Vector2 y_shell_dir = Quaternion.AngleAxis(shell_rotation_angle, Vector3.forward)*direction;
//			Debug.Log("y dir: " + y_shell_dir.ToString() + ", shooting angle: " + shooting_angle.ToString() + ", shooting dir: " +  direction.ToString() );
			float shell_rotation = (-direction + y_shell_dir*1.5f).GetAngle();
			
			// adding some randomness to the angle
			shell_rotation = Random.Range(shell_rotation - shellAngleRange, shell_rotation + shellAngleRange);

			_gameManager.SpawnShell(pos, _rigidbody2D.linearVelocity, shell_rotation, _playerState.player_framework, GetComponent<Collider2D>());
			
			// recoil
			if (!isGrounded)
			{
				_rigidbody2D.MovePosition(new Vector3(pos.x - direction.x * recoil, pos.y - direction.y * recoil, transform.position.z));				
			}
			else
			{
				_rigidbody2D.MovePosition(new Vector3(pos.x - direction.x * recoil, pos.y, transform.position.z));
			}

			return true;
		}

		private void slowHorizontalVelocity(float factor)
		{
			Vector2 vel = _rigidbody2D.linearVelocity;
			vel.x /= factor;
			_rigidbody2D.linearVelocity = vel;
		}

		private void OnCollisionEnter2D(Collision2D other)
		{
			if (other.transform.position.y < _rigidbody2D.position.y)
			{
				if (other.gameObject.CompareTag(Values.PLATFORM_BODY_TAG))
				{
					ConnectToPlatform(other.gameObject);
				}
			}

			if (other.gameObject.CompareTag(Values.SHOT_TAG))
			{
				if (EnableSFX) _sfx.PlayImpact();
			}
		}

		private void OnCollisionExit2D(Collision2D other)
		{
			if (other.gameObject.CompareTag(Values.PLATFORM_BODY_TAG))
			{
				DisconnectFromPlatfrom();
			}
		}

		private void OnTriggerExit2D(Collider2D other)
		{
			if (other.CompareTag(Values.PLATFORM_BODY_TAG))
			{
				var edgeCollider = other.gameObject.GetComponent<EdgeCollider2D>();
				if (edgeCollider != null) edgeCollider.enabled = true;
			}
			if (other.CompareTag(Values.BOUNDRIES_TAG))
			{
				if (invincible) return;
				// MP match start: player may briefly fall through a mis-layered
				// spawn platform before countdown reposition/freeze. Don't treat
				// that as a real death — GO enables normal bounds kills again.
				if (GameManager.IsMatchStartProtectionActive) return;
				if (EnableSFX) _sfx.PlayDeath();

				// In a Photon room, only the local-owner peer reports the
				// death. The remote view also independently detects the
				// off-screen condition, so without this gate the kill would
				// fire twice (host's and joiner's view of the same player
				// each calling PlayerKilled), double-decrementing the score.
				if (PhotonNetwork.InRoom)
				{
					var pv = GetComponent<PhotonView>();
					if (pv != null && !pv.IsMine) return;
				}

				_gameManager.PlayerKilled(gameObject);
				Debug.Log(gameObject.name + ": Killed");
			}
		}

		private void ConnectToPlatform(GameObject platform)
		{
			_playerState.currentPlatform = platform;
		}

		private void DisconnectFromPlatfrom()
		{
			_playerState.currentPlatform = null;
		}

		private void getOffPlatform()
		{
			if (_playerState.currentPlatform != null)
			{
				var edgeCollider = _playerState.currentPlatform.gameObject.GetComponent<EdgeCollider2D>();
				if (edgeCollider != null) edgeCollider.enabled = false;			
			}
		}

		private bool debugModeOn()
		{
			return debugMode && eventLog != null;
		}
	}
}

