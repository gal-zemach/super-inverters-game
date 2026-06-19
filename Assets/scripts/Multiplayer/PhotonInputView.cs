using Controllers;
using Photon.Pun;
using UnityEngine;

namespace Multiplayer
{
    // Replicates local controller input over the network. Remote peers use
    // NetworkController; local peer uses MultiplayerKeyboardController, optional
    // WebGamepadController, and MouseAimController (when no pad aim active).
    [RequireComponent(typeof(PhotonView))]
    public class PhotonInputView : MonoBehaviourPun, IPunObservable, IPunInstantiateMagicCallback
    {
        private KeyboardController _keyboard;
        private PS4Controller _ps4;
        private MultiplayerKeyboardController _multiKb;
        private WebGamepadController _webGamepad;
        private MouseAimController _mouseAim;
        private Rigidbody2D _rigidbody;

        private Vector2 _localAim;
        private float _localMoveX;
        private bool _pendingJump, _pendingShoot, _pendingGetDown;

        private Vector2 _remoteAim;
        private float _remoteMoveX;
        private bool _remoteJumpPending, _remoteShootPending, _remoteGetDownPending;
        private Vector3 _remotePosition;
        private bool _hasRemotePosition;

        public Vector2 RemoteAim => _remoteAim;
        public float RemoteMoveX => _remoteMoveX;

        public bool ConsumeJump()    { var v = _remoteJumpPending;    _remoteJumpPending    = false; return v; }
        public bool ConsumeShoot()   { var v = _remoteShootPending;   _remoteShootPending   = false; return v; }
        public bool ConsumeGetDown() { var v = _remoteGetDownPending; _remoteGetDownPending = false; return v; }

        private void Awake()
        {
            _keyboard = GetComponent<KeyboardController>();
            _ps4 = GetComponent<PS4Controller>();
            _multiKb = GetComponent<MultiplayerKeyboardController>();
            _webGamepad = GetComponent<WebGamepadController>();
            if (_webGamepad == null)
                _webGamepad = gameObject.AddComponent<WebGamepadController>();
            _webGamepad.enabled = false;
            _mouseAim = GetComponent<MouseAimController>();
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        public void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            if (photonView.IsMine)
            {
                if (_multiKb != null) _multiKb.enabled = true;
                if (_keyboard != null) _keyboard.enabled = false;
                if (_ps4 != null) _ps4.enabled = false;
                if (_webGamepad != null) _webGamepad.enabled = true;
                if (_mouseAim != null) _mouseAim.enabled = true;
            }
            else
            {
                if (_multiKb != null) _multiKb.enabled = false;
                if (_keyboard != null) _keyboard.enabled = false;
                if (_ps4 != null) _ps4.enabled = false;
                if (_webGamepad != null) _webGamepad.enabled = false;
                // Remote-only: disable physics simulation entirely so PhotonTransformView
                // fully owns position. Without this, PlayerManager.tryToJump (triggered by
                // replicated jump events) adds upward velocity to the body; Kinematic has
                // no gravity to counter it, so the body drifts off into the sky over time
                // (observed: remote White ended up at Y=62 after a few jumps).
                // Setting simulated=false makes velocity assignments no-ops, transform
                // writes still work, animations still play (driven by NetworkController).
                if (_rigidbody != null)
                {
                    _rigidbody.bodyType = RigidbodyType2D.Kinematic;
                    _rigidbody.simulated = false;
                }
            }
        }

        private void LateUpdate()
        {
            if (!photonView.IsMine) return;

            Vector2 aim = Vector2.zero;
            float moveX = 0f;
            bool jump = false, shoot = false, down = false;

            bool padActive = _webGamepad != null && _webGamepad.enabled && _webGamepad.IsActive;

            if (_mouseAim != null && _mouseAim.enabled && !padActive)
            {
                aim = _mouseAim.aim_direction();
                shoot |= _mouseAim.shoot();
            }

            if (_keyboard != null && _keyboard.enabled)
            {
                if (aim == Vector2.zero && _keyboard.aim_direction() != Vector2.zero)
                    aim = _keyboard.aim_direction();
                jump  |= _keyboard.jump();
                shoot |= _keyboard.shoot();
                down  |= _keyboard.getDown();
                moveX = _keyboard.moving_direction().x;
            }

            if (_multiKb != null && _multiKb.enabled)
            {
                jump  |= _multiKb.jump();
                shoot |= _multiKb.shoot();
                down  |= _multiKb.getDown();
                if (_webGamepad == null || !_webGamepad.IsActive)
                    moveX = _multiKb.moving_direction().x;
            }

            if (_webGamepad != null && _webGamepad.enabled && _webGamepad.IsActive)
            {
                var padAim = _webGamepad.aim_direction();
                if (padAim != Vector2.zero)
                    aim = padAim;
                jump  |= _webGamepad.jump();
                shoot |= _webGamepad.shoot();
                down  |= _webGamepad.getDown();
                moveX = _webGamepad.moving_direction().x;
            }

            // PS4Controller is intentionally NOT sampled here (see prior comment in this file).

            _localAim = aim;
            _localMoveX = moveX;

            if (jump)  _pendingJump     = true;
            if (shoot) _pendingShoot    = true;
            if (down)  _pendingGetDown  = true;
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(_localAim);
                stream.SendNext(_localMoveX);
                stream.SendNext(_pendingJump);
                stream.SendNext(_pendingShoot);
                stream.SendNext(_pendingGetDown);
                stream.SendNext(transform.position);
                _pendingJump = _pendingShoot = _pendingGetDown = false;
            }
            else
            {
                _remoteAim = (Vector2)stream.ReceiveNext();
                _remoteMoveX = (float)stream.ReceiveNext();
                if ((bool)stream.ReceiveNext()) _remoteJumpPending    = true;
                if ((bool)stream.ReceiveNext()) _remoteShootPending   = true;
                if ((bool)stream.ReceiveNext()) _remoteGetDownPending = true;
                _remotePosition = (Vector3)stream.ReceiveNext();
                _hasRemotePosition = true;
            }
        }

        // Higher = snappier catch-up to received position, lower = smoother.
        // ~15 reaches the new target in ~4 frames at 60Hz with a 10Hz sync rate.
        private const float PositionLerpSpeed = 15f;

        private void Update()
        {
            // Smoothly move remote bodies toward the latest received position. Replaces
            // PhotonTransformView for our use case — owning position writes here keeps
            // the entire stream in one IPunObservable so there's no chance of stream
            // misalignment between two of them on the same PhotonView.
            if (!photonView.IsMine && _hasRemotePosition)
            {
                transform.position = Vector3.Lerp(transform.position, _remotePosition, Time.deltaTime * PositionLerpSpeed);
            }
        }
    }
}
