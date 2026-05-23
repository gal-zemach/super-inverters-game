using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game {
	[DefaultExecutionOrder(150)]
	public class PlayerView : MonoBehaviour
	{
		[SerializeField] 
		public GameObject _animationGameObject;
		
		private SpriteRenderer _spriteRenderer;
		private Animator _animator;

		private Transform crosshair;
		private SpriteRenderer _crosshair_spriteRenderer;

		[SerializeField]
		public bool showCrosshair = true;

		[SerializeField, Tooltip("The distance of the crosshair from the player")]
		public float crosshairDistance = 2.5f;

		[HideInInspector] public bool isJumping;
		[HideInInspector] public bool isDoubleJumping;
		[HideInInspector] public bool isShooting;
		[HideInInspector] public float vertical_dir;
		[HideInInspector] public int horizontal_dir;
		[HideInInspector] public bool isMoving;
		[HideInInspector] public bool facingLeft;
		[HideInInspector] public bool isLanding;
		[HideInInspector] public bool isGrounded;

		Dictionary<string, int> anim_layers = new Dictionary<string, int>();
		
		private int currentLayer;
		// Animator sync target: not_shooting_1 (side) drives timing for all other aim layers.
		private int _referenceAimLayer;
		
		private string anim_not_shooting_prefix = "not_shooting_",  
					   anim_shoot_prefix = "shooting_";

		private int ANIM_DIR_NUMBER = 3;
		private AnimationClip[] _idleClips = new AnimationClip[3];
		private AnimationClip[] _idleShootClips = new AnimationClip[3];
		private bool _sampleDirectionalAimAfterAnimator;
		private string _idleClipPrefix = "idle_";
		private string _idleShootClipPrefix = "idle_shoot_";

		// Side clip (index 1) is not listed on RuntimeAnimatorController.animationClips; prefab refs force load.
		[SerializeField] private AnimationClip[] _directionalIdleClipsOverride = new AnimationClip[3];
		[SerializeField] private AnimationClip[] _directionalIdleShootClipsOverride = new AnimationClip[3];
		
		void Awake () {
			Init();
		}

		public void Init() {
			_spriteRenderer = _animationGameObject.GetComponent<SpriteRenderer>();
			_animator = _animationGameObject.GetComponent<Animator>();
			updateAnimLayerDictionary();
			_referenceAimLayer = anim_layers[anim_not_shooting_prefix + 1];
			currentLayer = _referenceAimLayer;
			setActiveAimLayer(currentLayer);
			cacheDirectionalIdleClips();
			
			crosshair = transform.Find(Values.PLAYER_CROSSHAIR_GAMEOBJ_NAME);
			_crosshair_spriteRenderer = crosshair.GetComponent<SpriteRenderer>();
		}

		private void Start()
		{
			facingLeft = GetComponent<Rigidbody2D>().position.x > 0;
			_spriteRenderer.flipX = facingLeft;
		}

		// Called from PlayerManager.LateUpdate (order 100) after aim + jump.
		public void ApplyAnimatorState()
		{
			bool steepAim = vertical_dir != 0f;
			bool suppressJumpAnim = (isJumping || isDoubleJumping) && steepAim;
			bool useDirectionalAimSample = !isMoving;
			_animator.enabled = true;
			_animator.SetBool("isJumping", isJumping && !suppressJumpAnim);
			_animator.SetBool("isDoubleJumping", isDoubleJumping && !suppressJumpAnim);
			_animator.SetBool("isShooting", isShooting);
			_animator.SetBool("isLanding", isLanding);
			_animator.SetBool("isMoving", isMoving);
			_animator.SetInteger("movingDir", horizontal_dir);

			changeAnimationLayer();
			_animator.Update(0f);

			// Sample after this LateUpdate pass (order 150) so aim clips win over the SM.
			_sampleDirectionalAimAfterAnimator = useDirectionalAimSample;
		}

		private void LateUpdate()
		{
			if (!_sampleDirectionalAimAfterAnimator) return;
			_sampleDirectionalAimAfterAnimator = false;
			applyDirectionalAimPose();
		}

		private void cacheDirectionalIdleClips()
		{
			var playerState = GetComponent<PlayerState>();
			if (playerState != null && playerState.player_framework == Framework.WHITE)
			{
				_idleClipPrefix = "white_idle_";
				_idleShootClipPrefix = "white_idle_shoot_";
			}

			var controller = _animator.runtimeAnimatorController;
			if (controller == null) return;

			for (int i = 0; i < ANIM_DIR_NUMBER; i++)
				_idleClips[i] = _idleShootClips[i] = null;

			for (int i = 0; i < ANIM_DIR_NUMBER; i++)
				AssignDirectionalClip(_idleClips, i, shoot: false, controller);
			for (int i = 0; i < ANIM_DIR_NUMBER; i++)
				AssignDirectionalClip(_idleShootClips, i, shoot: true, controller);
		}

		private void AssignDirectionalClip(AnimationClip[] slots, int index, bool shoot, RuntimeAnimatorController controller)
		{
			string expectedName = (shoot ? _idleShootClipPrefix : _idleClipPrefix) + index;
			var overrides = shoot ? _directionalIdleShootClipsOverride : _directionalIdleClipsOverride;

			if (overrides != null && index < overrides.Length && overrides[index] != null
			    && overrides[index].name == expectedName)
				slots[index] = overrides[index];

#if UNITY_EDITOR
			if (slots[index] == null)
				slots[index] = LoadIdleClipAsset(index, shoot);
#endif

			if (slots[index] == null && controller != null)
			{
				foreach (var clip in controller.animationClips)
				{
					if (clip.name == expectedName)
					{
						slots[index] = clip;
						break;
					}
				}
			}
		}

		private bool ClipNameMatchesIndex(AnimationClip clip, int index, bool shoot) =>
			clip != null && clip.name == (shoot ? _idleShootClipPrefix : _idleClipPrefix) + index;

		private void resolveMissingIdleClips()
		{
			var controller = _animator != null ? _animator.runtimeAnimatorController : null;
			for (int i = 0; i < ANIM_DIR_NUMBER; i++)
			{
				if (!ClipNameMatchesIndex(_idleClips[i], i, shoot: false))
					AssignDirectionalClip(_idleClips, i, shoot: false, controller);
				if (!ClipNameMatchesIndex(_idleShootClips[i], i, shoot: true))
					AssignDirectionalClip(_idleShootClips, i, shoot: true, controller);
			}
		}

#if UNITY_EDITOR
		private AnimationClip LoadIdleClipAsset(int index, bool shoot)
		{
			var playerState = GetComponent<PlayerState>();
			bool white = playerState != null && playerState.player_framework == Framework.WHITE;
			string folder = white ? "white" : "black";
			string clipName = shoot ? _idleShootClipPrefix + index : _idleClipPrefix + index;
			string subFolder = shoot ? "idle_shoot_directions" : "idle_directions";
			string path = $"Assets/Animations/Player/{folder}/{subFolder}/{clipName}.anim";
			return UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
		}
#endif

		// Override layers only swap motions for up/down; sample idle_* directly for all buckets.
		private void applyDirectionalAimPose()
		{
			int idx = animGetDirectionIndex(vertical_dir);
			var clip = isShooting ? _idleShootClips[idx] : _idleClips[idx];
			if (clip == null)
			{
				resolveMissingIdleClips();
				clip = isShooting ? _idleShootClips[idx] : _idleClips[idx];
			}
			if (clip != null)
				clip.SampleAnimation(_animationGameObject, 0f);
		}

		void FixedUpdate() {
			if (facingLeft && horizontal_dir > 0) { //it was facing left and now moving right
				facingLeft = false; // it should face right
			}
			else if (!facingLeft && horizontal_dir < 0) { //it was facing right and now moving left
				facingLeft = true; // it should face left
			}

			_spriteRenderer.flipX = facingLeft;
		}


		public void changeCrosshairDirection(Vector2 direction)
		{
			// When the player isn't aiming/moving, direction is (0, 0) and the
			// crosshair would sit at the player's center, hidden inside the
			// body sprite. Fall back to the player's facing direction so the
			// crosshair stays visible at idle.
			if (direction == Vector2.zero)
			{
				direction = facingLeft ? Vector2.left : Vector2.right;
			}
			crosshair.localPosition = new Vector2(direction.x / transform.localScale[0],
				direction.y / transform.localScale[1]) * crosshairDistance;
		}

		// currently copied for the demo from PlatformMangager maybe should be in one place?
		public void SetSpriteColor(Framework framework)
		{
			if (!showCrosshair) _crosshair_spriteRenderer.enabled = false;

			// SpriteRenderer.color (per-instance tint) gets multiplied with
			// material.color, so setting only material.color leaves the prefab's
			// baked-in tint in charge — Black's prefab has m_Color (0,0,0,1) on
			// its crosshair SpriteRenderer, which would clamp any material tint
			// back to black. Setting .color directly overrides the prefab.
			switch (framework)
			{
			case Framework.BLACK:
				// Crosshair is white (not black) so it stays visible against
				// both the black player sprite and the gray background.
				_crosshair_spriteRenderer.color = Color.white;
				break;

			case Framework.GREY:
				_crosshair_spriteRenderer.color = Color.grey;
				break;

			case Framework.WHITE:
				_crosshair_spriteRenderer.color = Color.white;
				break;
			}
		}
		
		private void updateAnimLayerDictionary()
		{
			for (int i = 0; i < ANIM_DIR_NUMBER; i++)
			{
				anim_layers.Add(anim_shoot_prefix + i, _animator.GetLayerIndex(anim_shoot_prefix + i));
				anim_layers.Add(anim_not_shooting_prefix + i, _animator.GetLayerIndex(anim_not_shooting_prefix + i));
			}
		}
		
		private void changeAnimationLayer()
		{
			var newAnimLayerName = isShooting ? anim_shoot_prefix : anim_not_shooting_prefix;
			newAnimLayerName = newAnimLayerName + animGetDirectionIndex(vertical_dir);
			int newLayer = anim_layers[newAnimLayerName];
			if (newLayer != currentLayer)
				setActiveAimLayer(newLayer);
		}

		private void setActiveAimLayer(int activeLayer)
		{
			// Synced aim layers mirror not_shooting_1 (side reference).
			// The reference layer must stay at weight 1 or overrides never show.
			foreach (var entry in anim_layers)
			{
				float weight = entry.Value == _referenceAimLayer || entry.Value == activeLayer ? 1f : 0f;
				_animator.SetLayerWeight(entry.Value, weight);
			}
			currentLayer = activeLayer;
		}

		private int animGetDirectionIndex(float yDir)
		{
			if (yDir < 0f) return 0;
			if (yDir > 0f) return 2;
			return 1;
		}
	}
}

