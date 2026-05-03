using Multiplayer;
using Photon.Pun;
using UnityEngine;

namespace Controllers
{
    // Slice 4: Controller subclass that replays a remote player's inputs from
    // the network. Reads from PhotonInputView (which streams the inputs on the
    // owning peer). When attached to the local player (photonView.IsMine),
    // returns zeros/false so it doesn't interfere with KeyboardController /
    // PS4Controller via PlayerManager's last-non-zero / OR merging.
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(PhotonInputView))]
    public class NetworkController : Controller
    {
        private PhotonInputView _inputView;
        private PhotonView _photonView;

        private bool _isJump, _isShoot, _isGetDown, _isPause;

        protected override void Start()
        {
            base.Start();
            _inputView = GetComponent<PhotonInputView>();
            _photonView = GetComponent<PhotonView>();
        }

        protected override void Update()
        {
            if (_photonView != null && _photonView.IsMine)
            {
                _isJump = _isShoot = _isGetDown = _isPause = false;
                base.Update();
                return;
            }

            if (_inputView != null)
            {
                _isJump    = _inputView.ConsumeJump();
                _isShoot   = _inputView.ConsumeShoot();
                _isGetDown = _inputView.ConsumeGetDown();
                _isPause   = _inputView.ConsumePause();
            }

            base.Update();
        }

        protected override float update_moving_direction()
        {
            if (_photonView != null && _photonView.IsMine) return 0f;
            return _inputView != null ? _inputView.RemoteAim.x : 0f;
        }

        protected override Vector2 update_aim_direction()
        {
            if (_photonView != null && _photonView.IsMine) return Vector2.zero;
            return _inputView != null ? _inputView.RemoteAim : Vector2.zero;
        }

        public override bool jump()      => _isJump;
        public override bool shoot()     => _isShoot;
        public override bool getDown()   => _isGetDown;
        public override bool pauseMenu() => _isPause;
    }
}
