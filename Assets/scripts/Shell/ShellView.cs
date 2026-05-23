using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game{
	public class ShellView : MonoBehaviour {

		private Color color = Color.gray;

		private Rigidbody2D body;

		private SpriteRenderer sprite_renderer;

		public Collider2D shooter_collider;

		public Color ShellColor{
			get {
				return this.color;
			}
			set {
				this.color = value;
				sprite_renderer.material.color = this.color;
			}
		}

		[SerializeField]  public Vector2 Position {
			get {
				return transform.position;
			}
			set {
				transform.position = value;
			}
		}


		protected void Awake() {
			body = GetComponent<Rigidbody2D>();
			sprite_renderer = GetComponent<SpriteRenderer>();
		}



		// Use this for initialization
		void Start () {

		}

		// Update is called once per frame
		void Update () {

		}

		public void SetVelocity(Vector2 velocity) {
//			body.velocity = velocity;
			body.AddForce(velocity, ForceMode2D.Impulse);
		}

		void OnCollisionEnter2D(Collision2D other) {
			if (other.gameObject.CompareTag(Values.PLATFORM_BODY_TAG)) {
//				Debug.Log("shell: collision with platform");
			}
			if (other.gameObject.CompareTag(Values.WALL_TAG))
			{
				Destroy(gameObject);
			}
		}

		public void SetShooterCollider(Collider2D shooter_collider) {
			this.shooter_collider = shooter_collider;
//			Physics2D.IgnoreCollision(GetComponent<Collider2D>(), this.shooter_collider,true);
//			Debug.Log("shell: ignoring player");
		} 

		void OnCollisionExit2D(Collision2D other) {
			if (other.gameObject.CompareTag(Values.PLAYER_TAG)) {
				Debug.Log("shell: exiting player");
			}
			if (other.gameObject.CompareTag(Values.WALL_TAG))
			{
				Destroy(gameObject);
			}
		}

		private void OnTriggerExit2D(Collider2D other)
		{
			if (other.CompareTag(Values.BOUNDRIES_TAG))
			{
				Destroy(gameObject);
			}
		}

		public void SetColor(Framework shot_framework) {
			switch (shot_framework) {
			case Framework.BLACK :
				this.ShellColor = Color.black;
				break;
			case Framework.WHITE :
				this.ShellColor = Color.white;
				break;
			default :
				this.ShellColor = Color.gray;
				break;
			}
		}
	}
}

