using UnityEngine;

namespace Controllers
{
    // Multiplayer-only keyboard layout: WASD movement, Space jump. Fire is on MouseAimController (LMB).
    // Used instead of KeyboardController on the local player in a networked
    // session — both peers share the same key scheme since each peer has its
    // own keyboard. Disabled by default on the prefab so single-player co-op
    // (which uses B1_/W1_ axis names per player) continues to work; enabled
    // by PhotonInputView.OnPhotonInstantiate when photonView.IsMine is true.
    public class MultiplayerKeyboardController : Controller
    {
        private Vector2 _direction;
        private bool _isJumping, _isGettingDown, _isPaused;

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

            _isPaused = !inStartScene && Input.GetKeyDown(KeyCode.Escape);

            base.Update();
        }

        protected override float update_moving_direction() => _direction.x;

        // Aim is driven by MouseAimController; W/S only affect jump / platform drop.
        protected override Vector2 update_aim_direction() => Vector2.zero;
        public override bool jump()      => _isJumping;
        public override bool shoot()     => false;
        public override bool getDown()   => _isGettingDown;
        public override bool pauseMenu() => _isPaused;
    }
}
