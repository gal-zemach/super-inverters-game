using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game{
	public class PlatformView : MonoBehaviour {

		public bool useSensorAsTrigger;

		public bool isLetter;

		private PlatformManager platform_manager;

		public PlatformState platform_state;

		public Rigidbody2D body;

		public PlatformSFX sfx;

		private SpriteRenderer sprite_renderer;

		private Animator animator;

		private List<Rigidbody2D> carried_bodies = new List<Rigidbody2D>();

		public Vector2 Position{
			get {
				return body.position;
			}
			set {
				body.position = value;
			}
		}

		public Vector2 Velocity{
			get {
				return body.velocity;
			}
			set {
				body.velocity = value;
			}
		}

		private Vector2 last_position;

		[SerializeField] private Color color = Color.gray;

		public Color PlatformColor{
			get {
				return this.color;
			}
			set {
				this.color = value;
				if (isLetter) {
					if (sprite_renderer == null) {
						sprite_renderer = GetComponent<SpriteRenderer>();
					}
					sprite_renderer.material.color = this.color;
				}

			}
		}

		// Use this for initialization
		void Start () {
			if (isLetter) {
				sprite_renderer.material.color = this.color;
			}

			last_position = Position;
			body.isKinematic = true;
//			if (useSensorAsTrigger) {
//				foreach(PlatformSensor sensor in GetComponentsInChildren<PlatformSensor>() ) {
//					
//					sensor.platform_view = this;
//				}
//			}

		}

		// Update is called once per frame
		void Update () {
			animator.SetInteger("color", (int)platform_state.platform_framework);
			if (Random.Range(0.0f, 1.0f) > 0.99f) {
//				animator.SetBool("glitch", true);
			}

		}

		void LateUpdate() {
			Velocity = Position - last_position;
			foreach(Rigidbody2D body in carried_bodies) {
//				Debug.Log("PlatformView: moving carried body " + body.transform + ", " + Velocity);
				body.transform.Translate(Velocity);
//				body.transform.parent = transform;
			}
			last_position = Position;
		}

		protected void Awake() {
			
			Init();
		}

		void OnEnable() {
			if (!isLetter) {
				if (platform_state.platform_framework == Framework.BLACK) {
					animator.Play("black");
				} else if (platform_state.platform_framework == Framework.WHITE) {
					animator.Play("white");
				}
			}

		}

		public void Init() {
			body = GetComponent<Rigidbody2D>();
			sprite_renderer = GetComponent<SpriteRenderer>();
			animator = GetComponent<Animator>();
			platform_manager = GetComponentInParent<PlatformManager>();
			platform_state = GetComponentInParent<PlatformState>();

			if (transform.Find("PlatformSensor") == null) {
				AddCarrierSensor();
			}

			if (transform.Find("PlatformShotSensor") == null) {
				AddShotSensor();
			}
			
			if (sfx == null) AddSFX();

			PlatformSensor carrier_sensor = GetComponentInChildren<PlatformSensor>();
			carrier_sensor.Init();
			PlatformShotSensor shot_sensor = GetComponentInChildren<PlatformShotSensor>();
			shot_sensor.Init();
			if (!isLetter) {
				animator.SetInteger("color", (int)platform_state.platform_framework);
			}
				
		}

		private void AddShotSensor() {
//			Debug.Log("platform body: create shot sensor");
			GameObject shot_sensor_instance = Instantiate(Resources.Load("PlatformShotSensor"), transform.position, transform.rotation) as GameObject;
			shot_sensor_instance.transform.parent = transform;
			shot_sensor_instance.name = "PlatformShotSensor";
			shot_sensor_instance.GetComponent<PlatformShotSensor>().Init();
			BoxCollider2D box = shot_sensor_instance.GetComponent<BoxCollider2D>();
			box.size = transform.localScale; // TODO: fix this according to sprite image to support different sized images
		}

		private void AddCarrierSensor() {
//			Debug.Log("platform body: create carrier sensor");
			GameObject carrier_sensor_instance = Instantiate(Resources.Load("PlatformSensor"), transform.position, transform.rotation) as GameObject;
			carrier_sensor_instance.transform.parent = transform;
			carrier_sensor_instance.name = "PlatformSensor";
			carrier_sensor_instance.GetComponent<PlatformSensor>().Init();
			EdgeCollider2D collider = carrier_sensor_instance.GetComponent<EdgeCollider2D>();
			// TODO: fix size according to sprite image to support different sized images
		}
		
				
		private void AddSFX() {
//			Debug.Log("platform body: create sfx object");
			GameObject sfx_instance = Instantiate(Resources.Load("PlatformSFX"), transform.position, transform.rotation) as GameObject;
			sfx_instance.transform.parent = transform;
			sfx_instance.name = "PlatformSFX";
			sfx = sfx_instance.GetComponent<PlatformSFX>();
		}

		
		public void UpdateHit(Framework framework) {
			if (framework != platform_state.platform_framework) {
				platform_manager.UpdateHit(framework);
			}
		}

		void OnCollisionEnter2D(Collision2D other) {
			if (other.gameObject.tag == Values.SHOT_TAG)
			{
//				Debug.Log("PlatformManager: detected shot");
				Framework framework = other.gameObject.GetComponent<ShotState>().shot_framework;
				UpdateHit(framework);
			}
			if (other.gameObject.tag == Values.PLAYER_TAG)
			{
				if (!useSensorAsTrigger) {
					Rigidbody2D rb = other.collider.GetComponent<Rigidbody2D>();

					if (rb != null) {
						Add(rb);
					}
				}

//				Debug.Log("PlatformManger: detected player");
			}
		}

		void OnCollisionExit2D(Collision2D other) {
			if (other.gameObject.tag == Values.PLAYER_TAG)
			{
				if (!useSensorAsTrigger) {
					Rigidbody2D rb = other.collider.GetComponent<Rigidbody2D>();
					if (rb != null) {
						Remove(rb);
					}
				}
				//				Debug.Log("PlatformManger: detected player");
			}
		}

		public void Add(Rigidbody2D rb) {
			if (!carried_bodies.Contains(rb)) {
				carried_bodies.Add(rb);
			}
		}

		public void Remove(Rigidbody2D rb) {
			if (carried_bodies.Contains(rb)) {
				carried_bodies.Remove(rb);
			}
		}

		// this is used right below
		private bool sfxFirstChange = true;
		
		public void SetColor(Framework platform_framework) {

			// I know it's ugly code but it solves playing the sound when platforms change color at start
			if (!sfxFirstChange) sfx.PlayChangeColor();
			else sfxFirstChange = false;
			
			switch (platform_framework) {
			case Framework.BLACK :
				this.PlatformColor = Color.black;
				break;
			case Framework.WHITE :
				this.PlatformColor = Color.white;
				break;
			default :
				this.PlatformColor = Color.grey;
				break;
			}
			if (!isLetter) {
				animator.SetInteger("color", (int)platform_framework);
			}

		}

		public void Show() {
			sprite_renderer.enabled = true;
		}

		public void Hide() {
			sprite_renderer.enabled = false;

		}
	}
}

