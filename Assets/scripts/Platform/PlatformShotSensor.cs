using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game{
	public class PlatformShotSensor : MonoBehaviour {

		public PlatformView platform_view;

		public Framework platform_framework {
			get {
				return platform_view.platform_state.platform_framework;
			}
		}


		public void Init() {
			platform_view = GetComponentInParent<PlatformView>();
			gameObject.tag = Values.PLATFORM_TAG; // TODO: change tag to PLATFORM_BODY_TAG
		}

		void Awake() {
			Init();
		}

		// Use this for initialization
		void Start () {

		}

		// Update is called once per frame
		void Update () {

		}

		void OnTriggerEnter2D(Collider2D ob) {
			if (ob.CompareTag(Values.SHOT_TAG)) {
//				Debug.Log("PlatformShotSensor: detected shot");
				Framework framework = ob.gameObject.GetComponent<ShotState>().shot_framework;
				platform_view.UpdateHit(framework);
			}
		}

		void OnTriggerExit2D(Collider2D ob) {
			if (ob.CompareTag(Values.PLAYER_TAG)) {
				var edgeCollider = GetComponentInParent<EdgeCollider2D>();
				if (edgeCollider != null) edgeCollider.enabled = true;
			}

		}


	}
}

