﻿/**
 * Ugliest code ever, if it means anything - we all suffered from it
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Controllers;
using Game;

namespace Controllers
{
	public class PS4Controller : Controller
	{
		[SerializeField]
		public int JoystickNumber = 1;  // for 2 joystick support

		[SerializeField, Tooltip("Allows for jumping with up DPad + Left Analog Stick\nin Addition to other jump controls.")] 
		public bool JumpUsingVerticalMovement = false;

		public bool AimWithMovement;
		public bool defaultAimToMove;
		
		public bool AutoFire = false;
		public bool AutoJumping = false;
		
		public String[] HorizontalMovementControls = {"PS4_LeftStick_Horizontal", "PS4_DPad_Horizontal"},
						VerticalMovementControls = {"PS4_LeftStick_Vertical", "PS4_DPad_Vertical"};

		public String[] HorizontalAimControls = {"PS4_RightStick_Horizontal"},
						VerticalAimControls = {"PS4_RightStick_Vertical"};

		public String[] JumpControls = {"PS4_X", "PS4_L1", "PS4_L2", "PS4_L3"},
						ShootControls = {"PS4_Square", "PS4_R1", "PS4_R2", "PS4_R3"};

		private String PauseButton = "PS4_Options";
		
		// Xbox & PS4 Analog stick mappings are the same
		// Xbox Dpad is acting weird and once any direction is pressed it always reverts to -1 value, so I removed it.
		private String[] XboxHorizontalMovementControls = {"PS4_LeftStick_Horizontal"},
						 XboxVerticalMovementControls = {"PS4_LeftStick_Vertical"};

		private String[] XboxHorizontalAimControls = {"PS4_RightStick_Horizontal"},
						 XboxVerticalAimControls = {"PS4_RightStick_Vertical"};

		// Xbox triggers (LT, RT) are also weird and I couldn't find them
		private String[] XboxJumpControls = {"XBOX_A", "XBOX_LB", "XBOX_L3"},
						 XboxShootControls = {"XBOX_X", "XBOX_RB", "XBOX_R3"};

		private String XboxPauseButton = "XBOX_Start";
		
		private String WindowsAddon = "_Windows";
		
		private float ANALOG_MOVE_THRESHOLD = 0.3f;
		private float ANALOG_AIM_THRESHOLD = 0.3f;
		private float ANALOG_JUMP_THRESHOLD = 0.6f;

		private bool isJumping, isShooting, isPaused;
		public bool isGettingDown;
		private float _moving_direction;
		private Vector2 _aim_direction, lastNonZeroFacingDirection;

		private Rigidbody2D _rigidbody2d;

		// Will be used when implementing 2nd controller
		private void Awake()
		{			
			checkControllerType();
			
			addJoystickNumber();
			
			checkOS();

			_rigidbody2d = GetComponent<Rigidbody2D>();
			lastNonZeroFacingDirection = getDefaultPlayerDirection();
		}

		protected override void Update()
		{
			isPaused = Input.GetButtonDown(PauseButton);
			
			// Updating Aiming direction
			bool aimChanged = false;
			Vector2 tempAimDirection = _aim_direction;
			foreach (var key in HorizontalAimControls)
			{
				tempAimDirection.x = Input.GetAxis(key);
			}
			foreach (var key in VerticalAimControls)
			{
				tempAimDirection.y = Input.GetAxis(key);
			}
			if (tempAimDirection.magnitude > ANALOG_AIM_THRESHOLD)
			{
				_aim_direction = tempAimDirection.normalized;
				aimChanged = true;
				
				// This direction will be used to default to if the player doesn't touch anything
				if (!Mathf.Approximately(_aim_direction.x, 0))
				{
					lastNonZeroFacingDirection = new Vector2(Mathf.Sign(_aim_direction.x), 0);
				}
			}
			
			// Updating moving direction
			bool moveDirectionChanged = false;
			foreach (var key in HorizontalMovementControls)
			{
				float tempMoveDirection = Input.GetAxis(key);
				if (Mathf.Abs(tempMoveDirection) > ANALOG_MOVE_THRESHOLD)
				{
					_moving_direction = Mathf.Sign(tempMoveDirection);
					moveDirectionChanged = true;

					// This direction will be used to default to if the player doesn't touch anything
					if (!Mathf.Approximately(_moving_direction, 0))
					{
						lastNonZeroFacingDirection = new Vector2(Mathf.Sign(_moving_direction), 0);
					}
				}
			}
			if (!moveDirectionChanged) _moving_direction = 0f;

			Vector2 tempMoveDir = Vector2.zero;
			foreach (var key in VerticalMovementControls)
			{
				float tempYDirection = -Input.GetAxis(key);

				if (Mathf.Abs(tempYDirection) > ANALOG_MOVE_THRESHOLD)
				{
					tempMoveDir.y = Mathf.Sign(tempYDirection);
				}
			}
			tempMoveDir.x = _moving_direction;
		
			// updating aim with movement input if it was not updated yet
			if (AimWithMovement && tempAimDirection == Vector2.zero)
			{
				
				if (tempMoveDir.magnitude > ANALOG_AIM_THRESHOLD)
				{
					_aim_direction = tempMoveDir.normalized;
					aimChanged = true;
				}
			}

			if (defaultAimToMove && !aimChanged)
			{
//				if (JoystickNumber == 1) Debug.Log("No Aim");
				_aim_direction = lastNonZeroFacingDirection;
			}
			
			// updated only with input from move direction
			bool downPressed = tempMoveDir.magnitude > ANALOG_AIM_THRESHOLD && tempMoveDir.y < 0;

			// Updating jumping
			isJumping = false;
			isGettingDown = false;
			foreach (var key in JumpControls)
			{
				var keyPress = AutoJumping ? Input.GetButton(key) : Input.GetButtonDown(key);
				if (downPressed)
				{
					isGettingDown = isGettingDown || keyPress;
				}
				else
				{
					isJumping = isJumping || keyPress;
				}
//				if (keyPress) Debug.Log(key);
			}
			if (JumpUsingVerticalMovement)
			{
				foreach (var key in VerticalMovementControls)
				{
					if (downPressed)
					{
						isGettingDown = isGettingDown || Input.GetAxis(key) < -ANALOG_JUMP_THRESHOLD;
					}
					else
					{
						isJumping = isJumping || Input.GetAxis(key) < -ANALOG_JUMP_THRESHOLD;
					}
//					if (keyPress) Debug.Log(key);
				}
			}
			
			// Updating Shooting
			isShooting = false;
			foreach (var key in ShootControls)
			{
				var keyPress = AutoFire ? Input.GetButton(key) : Input.GetButtonDown(key);
				isShooting = isShooting || keyPress;
//				if (keyPress) Debug.Log(key);
			}

			base.Update();
		}

		protected override float update_moving_direction()
		{
			return _moving_direction;
		}

		protected override Vector2 update_aim_direction()
		{
			return _aim_direction;
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
		
		public override bool pauseMenu()
		{
			return !inStartScene && isPaused;
		}

		
		private void checkControllerType()
		{
			String[] joystickNames = Input.GetJoystickNames();
			if (joystickNames.Length >= JoystickNumber)
			{
				string name = joystickNames[JoystickNumber - 1];
				Debug.Log(gameObject.name + " controller name: " + name);

				if (name.Contains("PLAYSTATION(R)3"))
				{
					Debug.Log(gameObject.name + " PS3 controller found.");
					updateToPS3Controls();
				}
				else if (name.Contains("Xbox"))
				{
					Debug.Log(gameObject.name + " Xbox controller found.");
					updateToXboxControls();
				}
			}
			else
			{
				Debug.Log(gameObject.name + " ps controller not found.");
			}
		}
		
		
		// Will be used for assigning each player his own controller
		private void addJoystickNumber()
		{
			String toAdd = "J" + JoystickNumber + "_";

			for (int i = 0; i < HorizontalMovementControls.Length; i++)
			{
				HorizontalMovementControls[i] = toAdd + HorizontalMovementControls[i];
			}

			for (int i = 0; i < VerticalMovementControls.Length; i++)
			{
				VerticalMovementControls[i] = toAdd + VerticalMovementControls[i];
			}

			for (int i = 0; i < HorizontalAimControls.Length; i++)
			{
				HorizontalAimControls[i] = toAdd + HorizontalAimControls[i];
			}

			for (int i = 0; i < VerticalAimControls.Length; i++)
			{
				VerticalAimControls[i] = toAdd + VerticalAimControls[i];
			}

			for (int i = 0; i < JumpControls.Length; i++)
			{
				JumpControls[i] = toAdd + JumpControls[i];
			}

			for (int i = 0; i < ShootControls.Length; i++)
			{
				ShootControls[i] = toAdd + ShootControls[i];
			}

			PauseButton = toAdd + PauseButton;
		}

		private void checkOS()
		{
			string name = SystemInfo.operatingSystem;
			Debug.Log("System: " + name);
			
			if (name.Contains("Windows"))
			{
				for (int i = 0; i < VerticalAimControls.Length; i++)
				{
					if (VerticalAimControls[i].Contains("PS4_RightStick_Vertical"))
						VerticalAimControls[i] = VerticalAimControls[i] + WindowsAddon;
				}
				
				for (int i = 0; i < VerticalMovementControls.Length; i++)
				{
					if (VerticalMovementControls[i].Contains("PS4_DPad_Vertical"))
						VerticalMovementControls[i] = VerticalMovementControls[i] + WindowsAddon;
				}
			}
		}
		
		private void updateToPS3Controls()
		{			
			string from = "PS4";
			string to = "PS3";

			replaceInStrings(HorizontalMovementControls, from, to);
			replaceInStrings(VerticalMovementControls, from, to);
			
			replaceInStrings(HorizontalAimControls, from, to);
			replaceInStrings(VerticalAimControls, from, to);
			
			replaceInStrings(JumpControls, from, to);
			replaceInStrings(ShootControls, from, to);
		}
		
		private void updateToXboxControls()
		{
			HorizontalMovementControls = XboxHorizontalMovementControls;
			VerticalMovementControls = XboxVerticalMovementControls;

			HorizontalAimControls = XboxHorizontalAimControls;
			VerticalAimControls = XboxVerticalAimControls;

			JumpControls = XboxJumpControls;
			ShootControls = XboxShootControls;

			PauseButton = XboxPauseButton;
		}

		private string[] replaceInStrings(string[] strings, string from, string to)
		{
			for (int i = 0; i < strings.Length; i++)
			{
				strings[i] = strings[i].Replace(from, to);
			}
			return strings;
		}
		
		
		private Vector2 getDefaultPlayerDirection()
		{
			return _rigidbody2d.position.x < 0 ? Vector2.right : Vector2.left;
		}
		
	}
}