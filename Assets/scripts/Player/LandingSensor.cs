using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game {
	public class LandingSensor : MonoBehaviour {

		private PlayerView player_view;
		private PlayerManager player_manager;

		public GameObject player;
		private Rigidbody2D body;

		public bool colliding;

		void Awake() {
			player_view = GetComponentInParent<PlayerView>();
			player_manager = GetComponentInParent<PlayerManager>();
			body = player.GetComponentInParent<Rigidbody2D>();
		}

		// Use this for initialization
		void Start () {
			
		}

		// Update is called once per frame
		void Update () {
			if (body.velocity.y <= -1 && colliding) {
				player_view.isLanding = true;
			} else if (player_manager.isGrounded) {
				player_view.isLanding = false;
			}
		}

		private void OnTriggerEnter2D(Collider2D other)
		{
			if (other.CompareTag(Values.PLATFORM_BODY_TAG))
			{
//				player_view.isLanding = true;
				colliding = true;
			}
		}

//		private void OnTriggerStay2D(Collider2D other)
//		{
//			if (other.CompareTag(Values.PLATFORM_BODY_TAG))
//			{
//				colliding = true;
//			}
//		}

		private void OnTriggerExit2D(Collider2D other)
		{
			if (other.CompareTag(Values.PLATFORM_BODY_TAG))
			{
				colliding = false;
			}
		}
			

		void FixedUpdate()
		{
//			if (player_manager.isGrounded) {
//				player_view.isLanding = false;
//			}
//			colliding = false;
		}


	}

}
