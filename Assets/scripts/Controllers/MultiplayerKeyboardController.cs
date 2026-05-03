using UnityEngine;

namespace Controllers
{
    // Multiplayer-only keyboard layout: WASD movement, Space jump, Shift fire.
    // Used instead of KeyboardController on the local player in a networked
    // session — both peers share the same key scheme since each peer has its
    // own keyboard. Disabled by default on the prefab so single-player co-op
    // (which uses B1_/W1_ axis names per player) continues to work; enabled
    // by PhotonInputView.OnPhotonInstantiate when photonView.IsMine is true.
    public class MultiplayerKeyboardController : Controller
    {
        private Vector2 _direction;
        private bool _isJumping, _isShooting, _isGettingDown, _isPaused;

        protected override void Update()
        {
            float x = 0f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            float y = 0f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;
            _direction = new Vector2(x, y);

            bool spacePressed = Input.GetKeyDown(KeyCode.Space);
            if (y < 0f)
            {
                _isGettingDown = spacePressed;
                _isJumping = false;
            }
            else
            {
                _isJumping = spacePressed;
                _isGettingDown = false;
            }

            _isShooting = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
            _isPaused = !inStartScene && Input.GetKeyDown(KeyCode.Escape);

            base.Update();
        }

        protected override float update_moving_direction() => _direction.x;
        protected override Vector2 update_aim_direction() => _direction;
        public override bool jump()      => _isJumping;
        public override bool shoot()     => _isShooting;
        public override bool getDown()   => _isGettingDown;
        public override bool pauseMenu() => _isPaused;
    }
}
