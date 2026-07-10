using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Game.Powerups
{
    // Grenade projectile.
    //   * full Rigidbody2D physics, bounces off platforms/floor/walls (see the
    //     `grenade` layer's Physics2D matrix row), each bounce damped by
    //     bounceVelocityRetention;
    //   * detonates after a fuse and paints every platform whose collider overlaps
    //     explosionRadius the thrower's colour (paint broadcast per platform);
    //   * ghost mode (Slice G5): the remote peer's copy simulates the same physics but
    //     never paints — it waits for the owner's RPCDetonateGhostGrenade to explode at
    //     the authoritative position (with a grace-fuse fallback if that never arrives).
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class GrenadeProjectile : MonoBehaviour
    {
        [Header("Physics")]
        [Tooltip("Gravity multiplier applied to the Rigidbody2D. Higher = steeper arc.")]
        [SerializeField] private float grenadeGravityScale = 1.0f;

        [Tooltip("Fraction of speed kept after each bounce (0..1). 1 = no decay; " +
                 "lower makes bounces die out faster. The PhysicsMaterial2D bounciness " +
                 "handles the reflection, this scalar handles the height decay.")]
        [Range(0f, 1f)]
        [SerializeField] private float bounceVelocityRetention = 0.6f;

        [Header("Fuse")]
        [Tooltip("Seconds from spawn until the grenade detonates.")]
        [SerializeField] private float fuseSeconds = 2.5f;

        [Header("Detonation")]
        [Tooltip("World-space radius around the final position. Every platform whose " +
                 "collider overlaps this circle is painted the thrower's colour.")]
        [SerializeField] private float explosionRadius = 6f;

        [Header("Bounds")]
        [Tooltip("If true, leaving the arena bounds detonates the grenade; " +
                 "if false it just vanishes with no paint.")]
        [SerializeField] private bool detonateOnBoundsExit = false;

        private Rigidbody2D _rb;
        private Framework _framework = Framework.BLACK;
        private float _fuseRemaining;
        private bool _detonated;
        private int _platformLayersMask;
        private GameManager _gameManager;

        // Ghost state (G5): a ghost is cosmetic-only. It normally detonates via
        // GhostDetonateAt (owner's RPC, exact position); the grace below is how long past
        // its nominal fuse it waits before detonating in place as a fallback.
        private const float GhostFuseGraceSeconds = 0.75f;
        private bool _isGhost;
        private float _ghostPaintRadius = -1f;
        private GrenadeThrower _ownerThrower;
        private int _ownerGrenadeId = -1;

        // The fuse this grenade will actually use; read right after Init by the thrower
        // so the ghost RPC carries the real value.
        public float FuseSecondsAtSpawn => _fuseRemaining;

        public void SetOwnerCallback(GrenadeThrower thrower, int grenadeId)
        {
            _ownerThrower = thrower;
            _ownerGrenadeId = grenadeId;
        }

        public void InitGhost(Framework framework, Vector2 initialVelocity, float fuseSeconds)
        {
            Init(framework, initialVelocity);
            _isGhost = true;
            _fuseRemaining = fuseSeconds + GhostFuseGraceSeconds;
        }

        // Owner told us exactly where it exploded: snap there and detonate.
        public void GhostDetonateAt(Vector2 pos, float paintRadius)
        {
            transform.position = pos;
            _ghostPaintRadius = paintRadius;
            Detonate();
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = grenadeGravityScale;
            _platformLayersMask = LayerMask.GetMask("platforms_black", "platforms_grey", "platforms_white");
            _fuseRemaining = fuseSeconds;
        }

        // Local launch (G1). framework = the colour the detonation will paint;
        // initialVelocity = launch velocity in world units/sec.
        public void Init(Framework framework, Vector2 initialVelocity)
        {
            _framework = framework;
            _fuseRemaining = fuseSeconds;
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = grenadeGravityScale;
            _rb.linearVelocity = initialVelocity;
        }

        private void Update()
        {
            if (_detonated) return;
            // Scaled time on purpose: a synced pause (timeScale 0) freezes the fuse.
            _fuseRemaining -= Time.deltaTime;
            if (_fuseRemaining <= 0f) Detonate();
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (_detonated) return;
            // Bounciness (material) reflects the velocity; this decays its magnitude so
            // successive bounces lose height. Two independent tunables by design.
            _rb.linearVelocity *= bounceVelocityRetention;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_detonated) return;
            // Mirror ShotView: the arena boundary is detected on trigger *exit*.
            if (other.CompareTag(Values.BOUNDRIES_TAG))
            {
                if (detonateOnBoundsExit) Detonate();
                else Destroy(gameObject);
            }
        }

        private void Detonate()
        {
            if (_detonated) return;
            _detonated = true;

            Vector2 pos = transform.position;
            float radius = _isGhost && _ghostPaintRadius > 0f ? _ghostPaintRadius : explosionRadius;
            GrenadeExplosionRing.Spawn(pos, radius, _framework);

            if (!_isGhost)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(pos, radius, _platformLayersMask);
                var painted = new HashSet<PlatformManager>();
                foreach (var hit in hits)
                {
                    var pm = hit.GetComponentInParent<PlatformManager>();
                    if (pm == null || painted.Contains(pm)) continue;
                    painted.Add(pm);
                    // Local apply: colour + collision layer + release of mismatched carried
                    // players. ApplyPaintFromNetwork deliberately does NOT re-broadcast, so we
                    // broadcast each platform explicitly below — same shape as the shot-hit and
                    // spawn-repaint paths. THIS is what makes the remote peer's platforms flip.
                    pm.ApplyPaintFromNetwork(_framework);
                    if (PhotonNetwork.InRoom && pm.networkId >= 0)
                    {
                        if (_gameManager == null) _gameManager = FindFirstObjectByType<GameManager>();
                        if (_gameManager != null)
                            _gameManager.BroadcastPaintPlatform(pm.networkId, _framework);
                    }
                }

                // Tell the remote ghost exactly where and how big the boom was (explicit
                // null check, not ?. — the thrower may be a destroyed Unity object mid-reload).
                if (_ownerThrower != null)
                    _ownerThrower.NotifyOwnerDetonated(_ownerGrenadeId, pos, radius);
            }

            // TODO(G6): explosion VFX polish per spec 3.7a (ring + SFX live in GrenadeExplosionRing).
            Destroy(gameObject);
        }
    }
}
