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

		[SerializeField, Tooltip("Lower value makes shooting faster")]
		public int turnsBetweenShots = 5;

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
		private int _timesSinceFired = 0;
		private float Jump_Y_Threshold = 5f;

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

			overlap_topLeft = transform.Find(Values.PLAYER_TOP_LEFT_GAMEOBJ_NAME);
			overlap_bottomRight = transform.Find(Values.PLAYER_BOT_RIGHT_GAMEOBJ_NAME);

			_sfx = GetComponentInChildren<PlayerSFX>();
		}


		void Start ()
		{
			_lastHorizontalAimDir = transform.position.x >= 0f ? 1 : -1;
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
			// Per-player disable (single-player path via _gameState.players)
			// OR global countdown flag (works for multiplayer where players
			// are PhotonNetwork.Instantiate'd and aren't in _gameState.players
			// at GameState.Awake time).
			if (controlsDisabled || GameManager.CountdownActive) return;

			foreach (var controller in controllers)
			{
				if (controller.pauseMenu())
				{
					_gameManager.TogglePauseMenu();
				}
			}
			
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
			if (isGrounded && _rigidbody2D.velocity.y <= 0f)
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
			
			foreach (var controller in controllers)
			{
				if (controller != null)
				{
					updateGrounded();

					if (controller.getDown())
						getOffPlatform();

					move(movingDirection);

					// used so the player doesn't slide as much
					if (Mathf.Approximately(movingDirection.x, 0) && isGrounded)
					{
						slowHorizontalVelocity(bonusFriction);
					}

					if (_timesSinceFired > 0) _timesSinceFired--;

					_playerView.changeCrosshairDirection(shootingDirection);
					if (controller.shoot())
					{
						shoot(shootingDirection);
						_playerView.isShooting = true;
					} else {
						_playerView.isShooting = false;
					}

					// Different gravity scale during fall
					if (_rigidbody2D.velocity.y < 0) _rigidbody2D.gravityScale = FallingGravityScale;
					else _rigidbody2D.gravityScale = RegularGravityScale;

					// limiting y speed while falling
					if (!isGrounded && _rigidbody2D.velocity.y < -MaxFallingVelocity)
					{
						_rigidbody2D.velocity = new Vector2(_rigidbody2D.velocity.x, -MaxFallingVelocity);
					}
					currentVelocity = _rigidbody2D.velocity;
				}
			}
		}


		public void DisableControls(bool status)
		{
			controlsDisabled = status;
		}


		private void updateDirection()
		{
			bool movingDirChanged = false;
			bool aimDirChanged = false;
			bool anyAimInput = false;
			Vector2 tempAimDir = Vector2.zero;
			
			foreach (var controller in controllers)
			{
				var behaviour = controller as MonoBehaviour;
				if (behaviour != null && !behaviour.enabled) continue;

				Vector2 tempMovingDir = controller.moving_direction();
				if (tempMovingDir != Vector2.zero)
				{
					movingDirection = tempMovingDir;
					movingDirChanged = true;
				}
				
				tempAimDir = controller.aim_direction();
				if (tempAimDir != Vector2.zero)
				{
					shootingDirection = tempAimDir;
					aimDirChanged = true;
					anyAimInput = true;
				}
			}
			if (!movingDirChanged) movingDirection = Vector2.zero;
			
			if (!aimDirChanged)
			{
				// movingDirection is horizontal-only; don't replace steep up/down aim on landing.
				if (movingDirection != Vector2.zero && Mathf.Abs(shootingDirection.y) < 0.5f)
					shootingDirection = movingDirection;
				else if (isGrounded && !anyAimInput)
					shootingDirection = Vector2.zero;
			}
			else if (shootingDirection != Vector2.zero)
			{
				lastNonZeroDirection = shootingDirection;
			}

			// updating animation parameters
			// vertical_dir is the aim's "verticalness" in [-1, 1] — the angle
			// from horizontal, normalized to ±π/2. Using the angle (rather than
			// raw shootingDirection.y) lets keyboard W+A produce 0.5 (up-diag)
			// instead of 1.0 (up), so the up_diag/down_diag animation buckets
			// actually trigger for keyboard input.
			Vector2 animAim = shootingDirection;
			if (animAim == Vector2.zero && !isGrounded)
				animAim = lastNonZeroDirection;
			if (animAim == Vector2.zero)
				_playerView.vertical_dir = 0f;
			else
				_playerView.vertical_dir = Mathf.Atan2(animAim.y, Mathf.Abs(animAim.x)) / (Mathf.PI / 2f);

			_playerView.isMoving = isGrounded && !Mathf.Approximately(movingDirection.x, 0);
			if (Mathf.Approximately(shootingDirection.x, 0))
				_playerView.horizontal_dir = _lastHorizontalAimDir;
			else
			{
				_playerView.horizontal_dir = (int) Mathf.Sign(shootingDirection.x);
				_lastHorizontalAimDir = _playerView.horizontal_dir;
			}
		}
		

		private void move(Vector2 direction)
		{
			Vector2 newVelocity = _rigidbody2D.velocity;
			//		newVelocity.x = direction.x * VelocityFactor;
			newVelocity.x = Mathf.Lerp(_rigidbody2D.velocity.x, direction.x * VelocityFactor, -Mathf.Pow(Time.deltaTime, 2) + 1);
			newVelocity.x = Mathf.Clamp(newVelocity.x, -maxXVelocity, maxXVelocity);
			_rigidbody2D.velocity = newVelocity;
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
				if (_rigidbody2D.velocity.y > Jump_Y_Threshold)
				{
					if (debugModeOn()) eventLog.AddEvent("PlayerManager: Didn't jump. y speed: " + _rigidbody2D.velocity.y);
					return;
				}
				
				DisconnectFromPlatfrom();
				
				_rigidbody2D.velocity += new Vector2(0, jumpHeight);
				if (EnableSFX) _sfx.PlayJump();
				if (debugModeOn()) eventLog.AddEvent("PlayerManager: Jumped.");
				_playerView.isJumping = true;
				canDoubleJump = true;
			}

			// Double jumping
			else if (canDoubleJump)
			{
				// This is to avoid chain jumping
				if (_rigidbody2D.velocity.y > minimalDoubleJumpVelocity)
				{
					if (debugModeOn()) eventLog.AddEvent("PlayerManager: Didn't doubleJump. y speed: " + _rigidbody2D.velocity.y);
					return;
				}
									
				_rigidbody2D.velocity = new Vector2(_rigidbody2D.velocity.x, jumpHeight);
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

		private void shoot(Vector2 direction)
		{
			if (_timesSinceFired > 0) return;
			_timesSinceFired = turnsBetweenShots;

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
				if (pv != null && !pv.IsMine) return;
			}

			Vector2 pos = _rigidbody2D.position;
			if (EnableSFX) _sfx.PlayShoot();
			float shooting_angle = direction.GetAngle();
			_gameManager.SpawnShot(pos, _rigidbody2D.velocity, shooting_angle, _playerState.player_framework);

//			float shell_rotation = (-direction + Vector2.up*1.5f).GetAngle();
			float shell_rotation_angle = (shooting_angle >= 90 || shooting_angle < -90) ? -90.0f : 90.0f;
			Vector2 y_shell_dir = Quaternion.AngleAxis(shell_rotation_angle, Vector3.forward)*direction;
//			Debug.Log("y dir: " + y_shell_dir.ToString() + ", shooting angle: " + shooting_angle.ToString() + ", shooting dir: " +  direction.ToString() );
			float shell_rotation = (-direction + y_shell_dir*1.5f).GetAngle();
			
			// adding some randomness to the angle
			shell_rotation = Random.Range(shell_rotation - shellAngleRange, shell_rotation + shellAngleRange);

			_gameManager.SpawnShell(pos, _rigidbody2D.velocity, shell_rotation, _playerState.player_framework, GetComponent<Collider2D>());
			
			// recoil
			if (!isGrounded)
			{
				_rigidbody2D.MovePosition(new Vector3(pos.x - direction.x * recoil, pos.y - direction.y * recoil, transform.position.z));				
			}
			else
			{
				_rigidbody2D.MovePosition(new Vector3(pos.x - direction.x * recoil, pos.y, transform.position.z));
			}

		}

		private void slowHorizontalVelocity(float factor)
		{
			Vector2 vel = _rigidbody2D.velocity;
			vel.x /= factor;
			_rigidbody2D.velocity = vel;
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

