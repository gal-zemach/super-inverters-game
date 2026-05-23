using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Game{
	public class BlinkingPlatformManager : PlatformManager {

		[SerializeField] private float visible_period = 2.0f;


		// Use this for initialization
		void Start () {
//			platform_view.Hide();
//			StartCoroutine(Reposition());
			Init();
		}

		// Update is called once per frame
		void Update () {

		}

		protected void FixedUpdate() {
			if ((Time.time - initial_lerp_time) >= segment_period) {
				UpdateSourceTargetPoints(); // updates initial_lerp_time to current time
//				Debug.Log("Blinking FixedUpdate: " + points[current_point_idx].position);
				platform_state.Position = points[current_point_idx].position;
				Show(); // importatnt to do this before setting view's position
				platform_view.Position = platform_state.Position;
				Invoke("Hide", Mathf.Min(segment_period, visible_period));
			}
		}

		IEnumerator Reposition() {

			while (true) {
				// reposition

				UpdateSourceTargetPoints();
				platform_state.Position = points[current_point_idx].position;


				yield return new WaitForSecondsRealtime(segment_period);

			}
		}


		private void Show() {
			platform_view.Show();
			platform_view.gameObject.SetActive(true);
//			Debug.Log("Showing object at time: " + Time.time);
		}

		private void Hide() {
			platform_view.Hide();
			platform_view.gameObject.SetActive(false);
//			Debug.Log("Hiding object at time: " + Time.time);
		}
	}
}

