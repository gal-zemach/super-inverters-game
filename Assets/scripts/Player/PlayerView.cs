using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils.Utils;

namespace Game {
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
		// Animator sync target: not_shooting_2 drives timing for all other aim layers.
		private int _referenceAimLayer;
		
		private string anim_not_shooting_prefix = "not_shooting_",  
					   anim_shoot_prefix = "shooting_";

		private int ANIM_DIR_NUMBER = 5;
		private AnimationClip[] _idleClips = new AnimationClip[5];
		private AnimationClip[] _idleShootClips = new AnimationClip[5];
		
		void Awake () {
			Init();
		}

		public void Init() {
			_spriteRenderer = _animationGameObject.GetComponent<SpriteRenderer>();
			_animator = _animationGameObject.GetComponent<Animator>();
			updateAnimLayerDictionary();
			_referenceAimLayer = anim_layers[anim_not_shooting_prefix + 2];
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

		// Called from PlayerManager.LateUpdate after aim + jump so layer weights match this frame.
		public void ApplyAnimatorState()
		{
			// Jump state uses the same side-facing clip on every aim layer; keep the
			// aim overlay pose while airborne when aiming steeply up or down.
			bool steepAim = Mathf.Abs(vertical_dir) > 0.5f;
			bool suppressJumpAnim = (isJumping || isDoubleJumping) && steepAim;
			// Sample steep aim whenever idle (in air, approaching land, or on ground).
			bool useSteepAimSample = steepAim && !isMoving;
			_animator.SetBool("isJumping", isJumping && !suppressJumpAnim);
			_animator.SetBool("isDoubleJumping", isDoubleJumping && !suppressJumpAnim);
			_animator.SetBool("isShooting", isShooting);
			_animator.SetBool("isLanding", isLanding);
			_animator.SetBool("isMoving", isMoving);
			_animator.SetInteger("movingDir", horizontal_dir);

			changeAnimationLayer();
			_animator.Update(0f);

			if (useSteepAimSample)
				applySteepAimPose();
		}

		private void cacheDirectionalIdleClips()
		{
			var controller = _animator.runtimeAnimatorController;
			if (controller == null) return;
			foreach (var clip in controller.animationClips)
			{
				if (clip.name.StartsWith("idle_shoot_")
				    && int.TryParse(clip.name.Substring(11), out int shootIdx)
				    && shootIdx >= 0 && shootIdx < ANIM_DIR_NUMBER)
					_idleShootClips[shootIdx] = clip;
				else if (clip.name.StartsWith("idle_")
				         && int.TryParse(clip.name.Substring(5), out int idleIdx)
				         && idleIdx >= 0 && idleIdx < ANIM_DIR_NUMBER)
					_idleClips[idleIdx] = clip;
			}
		}

		// Synced override layers keep the leader's horizontal idle visible; sample the
		// direction clip directly so steep aim shows the correct sprite in air and on land.
		private void applySteepAimPose()
		{
			int idx = animGetDirectionIndex(vertical_dir);
			var clip = isShooting ? _idleShootClips[idx] : _idleClips[idx];
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
			// Synced aim layers (not_shooting_0, shooting_4, etc.) mirror not_shooting_2.
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
			int val = Mathf.FloorToInt((yDir + 1)/ 2 * (ANIM_DIR_NUMBER - .01f));
			if (val < 0 || val > 4) Debug.Log(yDir + ", " + val);
			return val;
		}
	}
}

