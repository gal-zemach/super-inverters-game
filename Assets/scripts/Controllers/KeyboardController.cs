using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Controllers;
using Game;

namespace Controllers
{
    public class KeyboardController : Controller
    {
        public String HorizontalAxis = "B1_Horizontal",
            VerticalAxis = "B1_Vertical",
            JumpAxis = "B1_Jump",
            ShootAxis = "B1_Fire";
            
        private KeyCode PauseButton = KeyCode.Escape;

        public Vector2 direction;
        private bool isJumping, isShooting, isGettingDown, isPaused;
        private bool _wasShootingLastFrame;
        public bool autoFire, autoJump;

        protected override void Update()
        {
            isPaused = !inStartScene && Input.GetKeyDown(PauseButton);

            direction.x = Input.GetAxis(HorizontalAxis);
            direction.y = Input.GetAxis(VerticalAxis);
            isShooting = autoFire ? Input.GetButton(ShootAxis) : Input.GetButtonDown(ShootAxis);

            // Log only on the press/release transitions so a held-fire burst
            // produces two log lines (start + end) instead of one per frame.
            // Makes the console readable when capturing logs around a death
            // event without losing signal that shooting happened.
            if (isShooting && !_wasShootingLastFrame)
            {
                Debug.Log(gameObject.name + ": shoot start");
            }
            else if (!isShooting && _wasShootingLastFrame)
            {
                Debug.Log(gameObject.name + ": shoot end");
            }
            _wasShootingLastFrame = isShooting;

            if (direction.y < 0)
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
            return direction.x;
        }

        protected override Vector2 update_aim_direction()
        {
            var mouseAim = GetComponent<MouseAimController>();
            if (mouseAim != null && mouseAim.enabled)
                return Vector2.zero;
            return direction;
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
            return isPaused;
        }
    }
}
