using Controllers;
using Photon.Pun;
using UnityEngine;

namespace Multiplayer
{
    // Slice 4: replicates a player's controller inputs across the network and
    // disables local-input controllers on the remote player so only one peer
    // drives each player.
    //
    // On the local player (photonView.IsMine): samples KeyboardController/
    // PS4Controller each Update, accumulates pressed-since-last-sync button
    // events, and writes axis + button state on each OnPhotonSerializeView.
    //
    // On the remote player (!photonView.IsMine): KeyboardController and
    // PS4Controller are disabled in OnPhotonInstantiate; OnPhotonSerializeView
    // reads the stream and stores values for NetworkController to consume via
    // the public API below.
    [RequireComponent(typeof(PhotonView))]
    public class PhotonInputView : MonoBehaviourPun, IPunObservable, IPunInstantiateMagicCallback
    {
        private KeyboardController _keyboard;
        private PS4Controller _ps4;
        private MultiplayerKeyboardController _multiKb;
        private Rigidbody2D _rigidbody;

        private Vector2 _localAim;
        private bool _pendingJump, _pendingShoot, _pendingGetDown, _pendingPause;

        private Vector2 _remoteAim;
        private bool _remoteJumpPending, _remoteShootPending, _remoteGetDownPending, _remotePausePending;
        private Vector3 _remotePosition;
        private bool _hasRemotePosition;

        public Vector2 RemoteAim => _remoteAim;

        public bool ConsumeJump()    { var v = _remoteJumpPending;    _remoteJumpPending    = false; return v; }
        public bool ConsumeShoot()   { var v = _remoteShootPending;   _remoteShootPending   = false; return v; }
        public bool ConsumeGetDown() { var v = _remoteGetDownPending; _remoteGetDownPending = false; return v; }
        public bool ConsumePause()   { var v = _remotePausePending;   _remotePausePending   = false; return v; }

        private void Awake()
        {
            _keyboard = GetComponent<KeyboardController>();
            _ps4 = GetComponent<PS4Controller>();
            _multiKb = GetComponent<MultiplayerKeyboardController>();
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        public void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            if (photonView.IsMine)
            {
                // Local player in a networked session: swap to the multiplayer key
                // layout (WASD / Space / Shift). The single-player KB and PS4
                // controllers are disabled so they don't double-drive movement
                // alongside MultiplayerKeyboardController.
                if (_multiKb != null) _multiKb.enabled = true;
                if (_keyboard != null) _keyboard.enabled = false;
                if (_ps4 != null) _ps4.enabled = false;
            }
            else
            {
                if (_multiKb != null) _multiKb.enabled = false;
                if (_keyboard != null) _keyboard.enabled = false;
                if (_ps4 != null) _ps4.enabled = false;
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
            bool jump = false, shoot = false, down = false, pause = false;

            if (_keyboard != null && _keyboard.enabled)
            {
                if (_keyboard.aim_direction() != Vector2.zero) aim = _keyboard.aim_direction();
                jump  |= _keyboard.jump();
                shoot |= _keyboard.shoot();
                down  |= _keyboard.getDown();
                pause |= _keyboard.pauseMenu();
            }

            if (_multiKb != null && _multiKb.enabled)
            {
                if (_multiKb.aim_direction() != Vector2.zero) aim = _multiKb.aim_direction();
                jump  |= _multiKb.jump();
                shoot |= _multiKb.shoot();
                down  |= _multiKb.getDown();
                pause |= _multiKb.pauseMenu();
            }

            // PS4Controller is intentionally NOT sampled here. Its `defaultAimToMove`
            // setting forces aim_direction() to a spawn-direction default (e.g. (-1,0)
            // for WhitePlayer because White spawns at x=3) every frame, which would
            // permanently override KB's aim and pollute the network stream. PS4 still
            // drives the LOCAL player via PlayerManager's controller iteration; we just
            // don't replicate it over the network until the no-default-aim case is
            // properly handled.

            // Snap residual Input.GetAxis values to discrete -1 / 0 / +1.
            // Without this, axis decay (e.g. 0.05 mid-release) gets serialized and the
            // remote's Controller.Update normalizes (0.05, 0) to (1, 0) — full magnitude
            // — causing stuck-running animations and ghost movement on the remote.
            _localAim = new Vector2(SnapAxis(aim.x), SnapAxis(aim.y));

            if (jump)  _pendingJump     = true;
            if (shoot) _pendingShoot    = true;
            if (down)  _pendingGetDown  = true;
            if (pause) _pendingPause    = true;
        }

        private const float AxisDeadzone = 0.2f;

        private static float SnapAxis(float v)
        {
            if (v >  AxisDeadzone) return  1f;
            if (v < -AxisDeadzone) return -1f;
            return 0f;
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(_localAim);
                stream.SendNext(_pendingJump);
                stream.SendNext(_pendingShoot);
                stream.SendNext(_pendingGetDown);
                stream.SendNext(_pendingPause);
                stream.SendNext(transform.position);
                _pendingJump = _pendingShoot = _pendingGetDown = _pendingPause = false;
            }
            else
            {
                _remoteAim = (Vector2)stream.ReceiveNext();
                if ((bool)stream.ReceiveNext()) _remoteJumpPending    = true;
                if ((bool)stream.ReceiveNext()) _remoteShootPending   = true;
                if ((bool)stream.ReceiveNext()) _remoteGetDownPending = true;
                if ((bool)stream.ReceiveNext()) _remotePausePending   = true;
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
