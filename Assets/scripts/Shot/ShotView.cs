using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Game{
	public class ShotView : MonoBehaviour {

		private Color color = Color.gray;

		private Rigidbody2D body;

		private SpriteRenderer sprite_renderer;

		private ShotState shot_state;

		public Color ShotColor{
			get {
				return this.color;
			}
			set {
				this.color = value;
				sprite_renderer.material.color = this.color;
			}
		}

		// Use this for initialization
		void Start () {

		}

		// Update is called once per frame
		void Update () {

		}

		protected void Awake() {
			body = GetComponent<Rigidbody2D>();
			sprite_renderer = GetComponent<SpriteRenderer>();
			shot_state = GetComponent<ShotState>();
		}

		public void SetVelocity(Vector2 velocity) {
			body.velocity = velocity;
		}

		void OnTriggerEnter2D(Collider2D ob) {
			if (ob.CompareTag(Values.PLATFORM_TAG)) {
//				Debug.Log("ShotManager: detected platform");
				PlatformShotSensor shot_sensor = ob.gameObject.GetComponent<PlatformShotSensor>();
				if (shot_sensor) {
					Framework framework = shot_sensor.platform_framework;
				} else {
//					Debug.Log("ShotManager: shot sensor is null!!! :(");
				}

				Destroy(gameObject);
			}
		}

		
		void OnCollisionEnter2D(Collision2D other) {
			
			if (other.gameObject.CompareTag(Values.PLAYER_TAG) || other.gameObject.CompareTag(Values.WALL_TAG))
			{
				Destroy(gameObject);
			}
		}
		
		private void OnTriggerExit2D(Collider2D other)
		{
			if (other.CompareTag(Values.BOUNDRIES_TAG))
			{
//				Debug.Log("out of boundry");
				Destroy(gameObject);
			}
		}

		public void SetColor(Framework shot_framework) {
			switch (shot_framework) {
			case Framework.BLACK :
				this.ShotColor = Color.black;
				break;
			case Framework.WHITE :
				this.ShotColor = Color.white;
				break;
			default :
				this.ShotColor = Color.gray;
				break;
			}
		}

	}
}

