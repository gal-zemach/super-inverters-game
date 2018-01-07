﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game{
	public class PlatformState : MonoBehaviour {

		[SerializeField] private GameColor game_color = GameColor.GREY;

		[SerializeField] private static float HEIGHT = 1.0f;

		private static float DEFAULT_WIDTH = 5.0f;

		[SerializeField] private float width = DEFAULT_WIDTH;

		public Vector2 Position {
			get {
				return transform.position;
			}
			set {
				transform.position = value;
			}
		}

		public GameColor PlatformColor {
			get {
				return this.game_color;
			}
			set {
				this.game_color = value;
			}
		} 

		// Use this for initialization
		void Start () {

		}

		// Update is called once per frame
		void Update () {

		}
	}
}
