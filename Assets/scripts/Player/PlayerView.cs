using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

		Dictionary<string, int> anim_layers = new Dictionary<string, int>();
		
		private int currentLayer;
		
		private string anim_not_shooting_prefix = "not_shooting_",  
					   anim_shoot_prefix = "shooting_";

		private int ANIM_DIR_NUMBER = 5;
		
		void Awake () {
			Init();
		}

		public void Init() {
			_spriteRenderer = _animationGameObject.GetComponent<SpriteRenderer>();
			_animator = _animationGameObject.GetComponent<Animator>();
			updateAnimLayerDictionary();
			
			crosshair = transform.Find(Values.PLAYER_CROSSHAIR_GAMEOBJ_NAME);
			_crosshair_spriteRenderer = crosshair.GetComponent<SpriteRenderer>();
		}

		private void Start()
		{
			facingLeft = GetComponent<Rigidbody2D>().position.x > 0;
			_spriteRenderer.flipX = facingLeft;
		}

		void Update() {
			if (isLanding)
			{
				isJumping = false;
				isDoubleJumping = false;
			}
			
			_animator.SetBool("isJumping", isJumping);
			_animator.SetBool("isDoubleJumping", isDoubleJumping);
			_animator.SetBool("isShooting", isShooting);
			_animator.SetBool("isLanding", isLanding);
			_animator.SetBool("isMoving", isMoving);
			_animator.SetInteger("movingDir", horizontal_dir);
			
			changeAnimationLayer();
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
			crosshair.localPosition = new Vector2(direction.x / transform.localScale[0], 
				direction.y / transform.localScale[1]) * crosshairDistance;	
		}

		// currently copied for the demo from PlatformMangager maybe should be in one place?
		public void SetSpriteColor(Framework framework)
		{
			if (!showCrosshair) _crosshair_spriteRenderer.enabled = false;

			switch (framework)
			{
			case Framework.BLACK:
				//				_spriteRenderer.material.color = Color.black;
				_crosshair_spriteRenderer.material.color = Color.black;
				break;

			case Framework.GREY:
				//				_spriteRenderer.material.color = Color.grey;
				_crosshair_spriteRenderer.material.color = Color.grey;
				break;

			case Framework.WHITE:
				//				_spriteRenderer.material.color = Color.white;
				_crosshair_spriteRenderer.material.color = Color.white;
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
			// assembling layer name
			var newAnimLayerName = isShooting ? anim_shoot_prefix : anim_not_shooting_prefix;
			newAnimLayerName = newAnimLayerName + animGetDirectionIndex(vertical_dir);
			int newLayer = anim_layers[newAnimLayerName];
			
			// updating layer visibility
			if (newLayer != currentLayer)
			{
//				Debug.Log(gameObject.name + ": newLayer= " + newAnimLayerName + ", " + newLayer);
				_animator.SetLayerWeight(newLayer, 1);
				_animator.SetLayerWeight(currentLayer, 0);
			
				currentLayer = newLayer;
			}
		}

		private int animGetDirectionIndex(float yDir)
		{
			int val = Mathf.FloorToInt((yDir + 1)/ 2 * (ANIM_DIR_NUMBER - .01f));
			if (val < 0 || val > 4) Debug.Log(yDir + ", " + val);
			return val;
		}
	}
}

