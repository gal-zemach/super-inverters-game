using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils.Utils;

namespace Game{
	public class ShotState : MonoBehaviour {


		[SerializeField] public Framework shot_framework;

		[SerializeField] public float Rotation {
			get {
				return transform.eulerAngles.z;
			}
			set {
				var rot = transform.eulerAngles;
				rot.z = value;
				transform.eulerAngles = rot;
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

		[SerializeField]  public Vector2 Forward {
			get {
				return Vector2.right.Rotate(Rotation * Mathf.Deg2Rad);
			}
			set {
				transform.position = value;
			}
		}

		public Vector2 Velocity;

		// Use this for initialization
		void Start () {

		}

		// Update is called once per frame
		void Update () {

		}


	}
}

