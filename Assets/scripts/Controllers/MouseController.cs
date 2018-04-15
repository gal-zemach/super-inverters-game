﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Controllers;

namespace Controllers
{
	public class MouseController : Controller
	{
		public String HorizontalAxis = "W1_Horizontal",
		VerticalAxis = "W1_Vertical",
		JumpAxis = "Mouse_Jump",
		ShootAxis = "Mouse_Fire";

		public Vector2 _self_moving_direction;
		public Vector2 _self_aim_direction, lastNonZeroFacingDirection;
		private bool isJumping, isShooting, isGettingDown;
		public bool autoFire, autoJump;

		private Rigidbody2D _rigidbody2d;

		private float MOUSE_DELTA_THRESH = 0.001f;

		private void Awake()
		{
			_rigidbody2d = GetComponent<Rigidbody2D>();
			lastNonZeroFacingDirection = getDefaultPlayerDirection();
		}

		protected override void Update()
		{
			_self_moving_direction.x = Input.GetAxis(HorizontalAxis);
			_self_moving_direction.y = Input.GetAxis(VerticalAxis);
//			Debug.Log(_self_moving_direction);
			isShooting = autoFire ? Input.GetButton(ShootAxis) : Input.GetButtonDown(ShootAxis);

			if (_self_moving_direction.y < 0)
			{
				isGettingDown = autoJump ? Input.GetButton(JumpAxis) : Input.GetButtonDown(JumpAxis);
			}
			else
			{
				
				isJumping = autoJump ? Input.GetButton(JumpAxis) : Input.GetButtonDown(JumpAxis);
			}

			base.Update();
		}

		protected override float update_moving_direction()
		{
			// This direction will be used to default to if the player doesn't touch anything
			if (_self_moving_direction.x != 0)
			{
				lastNonZeroFacingDirection = new Vector2(Mathf.Sign(_self_moving_direction.x), 0);
			}
			return _self_moving_direction.x;
		}

		protected override Vector2 update_aim_direction()
		{
			Vector3 mouseViewportPos = Camera.main.ScreenToViewportPoint(Input.mousePosition);
			Vector2 relativeMousePos = (Vector2)mouseViewportPos - new Vector2(0.5f, 0.5f);
			float mouse_delta = Mathf.Max(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
			if (_self_moving_direction.x != 0 || _self_moving_direction.y != 0) {
				Debug.Log(_self_moving_direction);
				return _self_moving_direction.normalized;
			}
			else if (mouse_delta > MOUSE_DELTA_THRESH) {
				lastNonZeroFacingDirection = relativeMousePos;
				return relativeMousePos.normalized;
			}
			return lastNonZeroFacingDirection.normalized;
		}

		public override bool jump()
		{
			return isJumping;
		}

		public override bool shoot()
		{
			return isShooting;
		}

		public override bool getDown()
		{
			return isGettingDown;
		}

		private Vector2 getDefaultPlayerDirection()
		{
			return _rigidbody2d.position.x < 0 ? Vector2.right : Vector2.left;
		}
	}
}
