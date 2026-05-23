using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils.Utils;

namespace Game {
	public class ShotManager : MonoBehaviour {

		[SerializeField] private float speed = 10f;

		private ShotState shot_state;

		private ShotView shot_view;

		void Awake() {
			shot_state = GetComponent<ShotState>();
			shot_view = GetComponent<ShotView>();
		}

		// Use this for initialization
		void Start () {
			
		}
		
		public void Activate(Vector2 position, Vector2 startVelocity, float rotation, Framework shot_framework)
		{
			shot_state.Position = position;
			shot_state.Rotation = rotation;
			
			Vector2 dir = Quaternion.Euler(0,0,rotation) * Vector2.right;
			Vector2 velocity = startVelocity + dir * speed;
			shot_state.Velocity = velocity;
			shot_view.SetVelocity(velocity);
			SetFramework(shot_framework);
			gameObject.layer = shot_framework == Framework.BLACK ? 9 : 10; // set layer accordingly, this allows for shots to ignore platforms of same framework
		}

		private void SetFramework(Framework shot_framework) {
			shot_state.shot_framework = shot_framework;
			shot_view.SetColor(shot_framework);

		}

		// Update is called once per frame
		void Update () {

		}


	}
}

