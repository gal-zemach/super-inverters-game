using Photon.Pun;
using UnityEngine;

namespace Game.Powerups
{
    // Local-only hold-to-charge aim + throw for the grenade (Slice G2).
    //   * HOLD throwKey -> charge ramps 0..1 over time, force lerps min..max; a white
    //     beam from the player along the aim direction grows LONGER with charge and is
    //     WIDER at its far end.
    //   * RELEASE       -> GrenadeThrower.Throw(dir, force); the inventory is consumed.
    //
    // Active only when the local avatar actually holds a grenade and controls are live.
    // The throw is replicated by RPC in G5, NOT through the input/serialize stream, so
    // this deliberately never touches the Controller hierarchy or PhotonInputView.
    [RequireComponent(typeof(GrenadeInventory))]
    [RequireComponent(typeof(GrenadeThrower))]
    public class GrenadeAimController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode throwKey = KeyCode.G;
        [Tooltip("Optional gamepad button name for throwing. Empty = keyboard only.")]
        [SerializeField] private string throwButton = "";

        [Header("Charge")]
        [SerializeField] private float throwForceMin = 8f;
        [SerializeField] private float throwForceMax = 36f;
        [Tooltip("Charge fill rate. charge01 = heldSeconds * this, clamped to 1.")]
        [SerializeField] private float chargeRampPerSecond = 1.2f;

        [Header("Aim beam")]
        [SerializeField] private float beamOriginOffset = 1.2f;
        [Tooltip("Beam length at zero charge / full charge (world units).")]
        [SerializeField] private float beamLenMin = 2f;
        [SerializeField] private float beamLenMax = 12f;
        [Tooltip("Beam width at the player end / far end (world units).")]
        [SerializeField] private float beamWidthNear = 0.2f;
        [SerializeField] private float beamWidthFar = 1.0f;

        private PhotonView _pv;
        private PlayerManager _player;
        private GrenadeInventory _inventory;
        private GrenadeThrower _thrower;
        private LineRenderer _beam;
        private float _heldTime;
        private Vector2 _lastAimDir = Vector2.right;

        private void Awake()
        {
            _pv = GetComponent<PhotonView>();
            _player = GetComponent<PlayerManager>();
            _inventory = GetComponent<GrenadeInventory>();
            _thrower = GetComponent<GrenadeThrower>();
            SetupBeam();
        }

        private bool IsLocal => _pv == null || _pv.IsMine;

        private bool CanAim =>
            IsLocal
            && _inventory.HasGrenade
            && !GameManager.CountdownActive
            && !GameManager.LocalPauseActive
            && (_player == null || !_player.ControlsDisabled);

        private void Update()
        {
            if (!CanAim)
            {
                _heldTime = 0f;
                if (_beam.enabled) _beam.enabled = false;
                return;
            }

            Vector2 dir = AimDir();

            if (Held())
            {
                _heldTime += Time.deltaTime;
                UpdateBeam(dir);
            }
            else if (Released())
            {
                _thrower.Throw(dir, Mathf.Lerp(throwForceMin, throwForceMax, Charge01));
                _inventory.Consume();
                _heldTime = 0f;
                _beam.enabled = false;
            }
            else if (_beam.enabled)
            {
                _beam.enabled = false;
            }
        }

        // Tuning accessors used by the temporary GrenadeDebugSpawner slider UI.
        public float ThrowForceMin { get => throwForceMin; set => throwForceMin = value; }
        public float ThrowForceMax { get => throwForceMax; set => throwForceMax = value; }
        public float ChargeRampPerSecond { get => chargeRampPerSecond; set => chargeRampPerSecond = value; }

        private float Charge01 => Mathf.Clamp01(_heldTime * chargeRampPerSecond);

        private bool Held() =>
            Input.GetKey(throwKey) ||
            (!string.IsNullOrEmpty(throwButton) && Input.GetButton(throwButton));

        private bool Released() =>
            Input.GetKeyUp(throwKey) ||
            (!string.IsNullOrEmpty(throwButton) && Input.GetButtonUp(throwButton));

        // Free aim from the player's unified aim (mouse/keyboard/gamepad), with a
        // fallback to the last non-zero direction so a released-aim throw still fires.
        private Vector2 AimDir()
        {
            Vector2 d = _player != null ? _player.shootingDirection : Vector2.zero;
            if (d.sqrMagnitude > 0.0001f) _lastAimDir = d.normalized;
            return _lastAimDir;
        }

        private void SetupBeam()
        {
            _beam = gameObject.AddComponent<LineRenderer>();
            _beam.useWorldSpace = true;
            _beam.positionCount = 2;
            _beam.numCapVertices = 4;
            _beam.textureMode = LineTextureMode.Stretch;
            _beam.material = new Material(Shader.Find("Sprites/Default"));
            _beam.sortingOrder = 50;
            _beam.startColor = new Color(1f, 1f, 1f, 0.5f);   // white, 50% transparent
            _beam.endColor = new Color(1f, 1f, 1f, 0.5f);
            _beam.enabled = false;
        }

        private void UpdateBeam(Vector2 dir)
        {
            Vector2 start = (Vector2)transform.position + dir * beamOriginOffset;
            Vector2 end = start + dir * Mathf.Lerp(beamLenMin, beamLenMax, Charge01);
            _beam.enabled = true;
            _beam.startWidth = beamWidthNear;   // thin at the player
            _beam.endWidth = beamWidthFar;      // wider the farther out
            _beam.SetPosition(0, start);
            _beam.SetPosition(1, end);
        }
    }
}
