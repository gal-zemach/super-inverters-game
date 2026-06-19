using UnityEngine;

namespace Controllers
{
	// Browser / WebGL gamepad input using Unity's legacy Input Manager.
	// Maps the first connected pad: left stick move, right stick aim, A/Cross jump,
	// X/Square shoot, Start pause. Keyboard+mouse remain available alongside.
	public class WebGamepadController : Controller
	{
		private const float MoveDeadzone = 0.25f;
		private const float AimDeadzone = 0.25f;

		private float _moveX;
		private Vector2 _aim;
		private bool _jump;
		private bool _getDown;
		private bool _shoot;
		private bool _pause;

		public bool IsActive =>
			Application.isPlaying && Input.GetJoystickNames().Length > 0;

		protected override void Update()
		{
			_moveX = 0f;
			_aim = Vector2.zero;
			_jump = _getDown = _shoot = _pause = false;

			if (!IsActive)
			{
				base.Update();
				return;
			}

			float moveX = Input.GetAxisRaw("Horizontal");
			float moveY = -Input.GetAxisRaw("Vertical");
			if (Mathf.Abs(moveX) > MoveDeadzone)
				_moveX = Mathf.Sign(moveX);

			float aimX = Input.GetAxisRaw("WebGL_RightStickX");
			float aimY = Input.GetAxisRaw("WebGL_RightStickY");
			var aimVec = new Vector2(aimX, aimY);
			if (aimVec.sqrMagnitude > AimDeadzone * AimDeadzone)
				_aim = aimVec.normalized;

			// Fallback aim: left stick horizontal when not moving (common on pads without mapped right stick).
			if (_aim == Vector2.zero && _moveX != 0f)
				_aim = new Vector2(_moveX, 0f);

			bool jumpPress = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Joystick1Button0);
			if (moveY < -0.5f)
				_getDown = jumpPress;
			else
				_jump = jumpPress;

			_shoot = Input.GetButton("Fire1") || Input.GetKey(KeyCode.Joystick1Button2);
			_pause = Input.GetButtonDown("Submit") || Input.GetKeyDown(KeyCode.Joystick1Button7);

			base.Update();
		}

		protected override float update_moving_direction() => _moveX;

		protected override Vector2 update_aim_direction() => _aim;

		public override bool jump() => _jump;

		public override bool shoot() => _shoot;

		public override bool getDown() => _getDown;

		public override bool pauseMenu() => !inStartScene && _pause;
	}
}
