using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Game.Powerups
{
    // Slice G1: local-only grenade projectile.
    //   * full Rigidbody2D physics, bounces off platforms/floor/walls (see the
    //     `grenade` layer's Physics2D matrix row), each bounce damped by
    //     bounceVelocityRetention;
    //   * detonates after a fuse and paints every platform whose collider overlaps
    //     explosionRadius the thrower's colour.
    // Networking (ghost + detonation sync + paint broadcast) is added in Slice G5;
    // the TODO markers below are the seams for that.
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
            Collider2D[] hits = Physics2D.OverlapCircleAll(pos, explosionRadius, _platformLayersMask);
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

            // TODO(G5): RPCSpawnGhostGrenade + RPCDetonateGrenade so the REMOTE peer also
            //           sees the grenade fly and explode at `pos` (cosmetic — platform colours
            //           already sync via the per-platform broadcast above).
            // TODO(G6): spawn explosion VFX/SFX (detach or delay-destroy per spec 3.7a).
            Destroy(gameObject);
        }
    }
}
