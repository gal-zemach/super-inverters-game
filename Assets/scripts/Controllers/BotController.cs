using Game;
using Photon.Pun;
using UnityEngine;

namespace Controllers
{
    // Single-player AI opponent. Drives the White player through the SAME
    // Controller interface the human/keyboard/network controllers use, so
    // PlayerManager needs zero changes — this mirrors NetworkController's role
    // (a non-human Controller feeding inputs through the polled interface).
    //
    // ACTIVATION (self-configuring; no external SP/MP fork):
    //   - Ships ENABLED on WhitePlayer.prefab.
    //   - In Awake: if we're in a Photon room, the White player is a REMOTE HUMAN
    //     (MP), so the bot disables itself and never touches gameplay. Otherwise
    //     (single-player) it disables the sibling human-input controllers
    //     (Keyboard / PS4 / GrenadeAim) so it cleanly owns this side.
    //
    // COMBAT MODEL (why v1 is simple): shots don't kill — they knock back and
    // PAINT platforms to the shooter's colour. A player only dies by falling out
    // of bounds. So painting the platform under the opponent white makes THEM
    // fall through: "aim at the opponent's feet" is attack, denial, and kill at
    // once. The bot therefore needs no separate paint planner in v1.
    public class BotController : Controller
    {
        // ---- Difficulty: six discrete tiers, easiest (1) → hardest (6) --------
        // The selected tier SHIPS (serialized). Runtime tier changes (keys + the
        // on-screen window) are editor-only debug aids, compiled out of builds.
        [SerializeField, Range(1, 10), Tooltip("Bot difficulty tier: 1 = easiest, 10 = hardest.")]
        private int difficultyTier = 4;

        // Per-tier presets (index = tier-1). Parallel arrays: tier N reads [N-1]. 10 tiers.
        private static readonly float[] TierReactionDelay = { 0.70f, 0.60f, 0.50f, 0.42f, 0.34f, 0.27f, 0.20f, 0.14f, 0.09f, 0.05f };
        private static readonly float[] TierAimErrorDeg   = { 22f,   18f,   15f,   12f,   9f,    7f,    5f,    3.5f,  2f,    1f    };
        private static readonly float[] TierBurstOn       = { 0.16f, 0.20f, 0.25f, 0.30f, 0.34f, 0.38f, 0.42f, 0.45f, 0.48f, 0.50f };
        private static readonly float[] TierBurstOff      = { 1.40f, 1.20f, 1.00f, 0.85f, 0.72f, 0.60f, 0.50f, 0.42f, 0.35f, 0.28f };
        private static readonly float[] TierAggression    = { 0.15f, 0.25f, 0.35f, 0.45f, 0.55f, 0.65f, 0.75f, 0.85f, 0.92f, 1.00f };

        private const int TierCount = 10;
        private int TierIndex => Mathf.Clamp(difficultyTier, 1, TierCount) - 1;
        private float ReactionDelay => TierReactionDelay[TierIndex];
        private float AimErrorDeg   => TierAimErrorDeg[TierIndex];
        private float BurstOn       => TierBurstOn[TierIndex];
        private float BurstOff      => TierBurstOff[TierIndex];
        private float Aggression    => TierAggression[TierIndex];

        // ---- Tuning (tier-independent) ----------------------------------------
        [Header("Behaviour tuning")]
        [SerializeField, Tooltip("How often the bot re-plans (seconds). Aim is smoothed every frame regardless.")]
        private float thinkInterval = 0.1f;
        [SerializeField, Tooltip("Horizontal distance within which the bot shoots at the opponent (world units).")]
        private float engageRange = 35f;
        [SerializeField, Tooltip("Preferred horizontal distance to hold from the opponent (world units).")]
        private float preferredRange = 10f;
        [SerializeField, Tooltip("Horizontal band half-width around preferredRange where the bot holds position.")]
        private float rangeDeadband = 3f;
        [SerializeField, Tooltip("How far ahead the bot probes for a ledge before stepping (world units).")]
        private float edgeLookAhead = 2f;
        [SerializeField, Tooltip("Max horizontal distance the bot will leap across a gap to reach a platform (world units).")]
        private float jumpReach = 10f;
        [SerializeField, Tooltip("Downward probe length for standable/paintable ground (world units).")]
        private float groundProbeDistance = 4f;
        [SerializeField, Tooltip("Aim this far below the opponent's origin when they're grounded, to paint their platform.")]
        private float feetAimOffset = 1.0f;

        [Header("Debug")]
        [SerializeField, Tooltip("Show the editor-only on-screen difficulty window during play.")]
        private bool showDebugWindow = true;

        // ---- Runtime outputs (read by the Controller interface) ---------------
        private float _moveX;
        private Vector2 _baseAim = Vector2.right;   // smoothed aim toward the target
        private bool _jumpQueued;
        private bool _fireHeld;                      // the actual trigger this frame

        // ---- Internal state ---------------------------------------------------
        private PlayerManager _self;
        private Rigidbody2D _rb;
        private Framework _selfFramework;
        private Transform _opponent;

        private int _standableMask;   // own-colour platforms + floor (we can land here)
        private int _paintableMask;    // opponent-colour platforms (shoot to flip them to ours)

        // Opponent position history for reaction delay (ring buffer).
        private const int HistorySize = 64;
        private readonly Vector2[] _oppPosHist = new Vector2[HistorySize];
        private readonly float[] _oppTimeHist = new float[HistorySize];
        private int _histHead;
        private int _histCount;

        private float _nextThinkTime;
        private Vector2 _desiredAim = Vector2.right;
        private int _patrolDir = 1;      // oscillation direction while in range (keeps the bot moving)
        private float _patrolFlipTime;   // next time to flip _patrolDir
        private float _nextClimbTime;    // cooldown between height-seeking climb jumps

        // Fire intent (set by Think, held between ticks) → burst cadence. Holding
        // shoot() forever hits PlayerManager's burst lockout (CanShoot blocks
        // after burstFireDurationSeconds of continuous fire and only resets on a
        // no-fire frame), so we MUST release between bursts.
        private bool _fireIntent;
        private bool _bursting;
        private float _burstPhaseEndTime;
        private float _burstAimErrorRad;

        private void Awake()
        {
            // In a room this White avatar is a remote human — the bot must never
            // run. (PhotonNetwork.Instantiate only happens in-room, so InRoom is
            // already true here for the MP path.)
            if (PhotonNetwork.InRoom)
            {
                enabled = false;
                return;
            }

            // Single-player: take exclusive ownership of this side's input by
            // switching off the human controllers. Disabled-at-Awake is safe;
            // their polled getters then return initial (false / zero) values.
            DisableComponent<KeyboardController>();
            DisableComponent<PS4Controller>();
            DisableComponent<Game.Powerups.GrenadeAimController>(); // raw KeyCode.G, outside the Controller seam
        }

        protected override void Start()
        {
            base.Start();
            _self = GetComponent<PlayerManager>();
            _rb = GetComponent<Rigidbody2D>();
            var state = GetComponent<PlayerState>();
            _selfFramework = state != null ? state.player_framework : Framework.WHITE;

            string ownPlatformLayer = _selfFramework == Framework.BLACK
                ? Values.BLACK_PLATFORM_LAYER : Values.WHITE_PLATFORM_LAYER;
            string enemyPlatformLayer = _selfFramework == Framework.BLACK
                ? Values.WHITE_PLATFORM_LAYER : Values.BLACK_PLATFORM_LAYER;
            _standableMask = LayerMask.GetMask(ownPlatformLayer, "floor");
            _paintableMask = LayerMask.GetMask(enemyPlatformLayer);

            AcquireOpponent();
        }

        protected override void Update()
        {
#if UNITY_EDITOR
            HandleDebugDifficultyKeys();
#endif
            if (PhotonNetwork.InRoom) { enabled = false; return; }

            // Frozen phases (countdown / disabled controls): emit neutral inputs
            // and advance no timers, so nothing is latched to fire at "GO".
            if (IsFrozen())
            {
                _moveX = 0f;
                _jumpQueued = false;
                _fireHeld = false;
                _bursting = false;
                _fireIntent = false;
                base.Update();
                return;
            }

            if (_opponent == null) AcquireOpponent();
            RecordOpponentSample();

            if (Time.time >= _nextThinkTime)
            {
                _nextThinkTime = Time.time + Mathf.Max(0.02f, thinkInterval);
                Think();
            }

            SmoothAim();
            UpdateBurstFire();

            base.Update();
        }

        // ---- The brain: Survive > Fight > Move --------------------------------
        private void Think()
        {
            _fireIntent = false;

            if (_opponent == null)
            {
                _moveX = 0f;
                _desiredAim = FacingAim();
                return;
            }

            Vector2 pos = _rb != null ? _rb.position : (Vector2)transform.position;
            Vector2 target = DelayedOpponentPosition();
            float dx = target.x - pos.x;
            int towardOpp = dx >= 0f ? 1 : -1;

            // Aim: at the opponent's feet when grounded (paint their floor), at
            // their body when airborne (knockback). Always non-zero.
            Vector2 aimPoint = target;
            if (OpponentGrounded()) aimPoint.y -= feetAimOffset;
            _desiredAim = (aimPoint - pos).normalized;
            if (_desiredAim == Vector2.zero) _desiredAim = FacingAim();

            // Fight: shoot whenever the opponent is within engage range.
            float absDx = Mathf.Abs(dx);
            if (absDx <= engageRange) _fireIntent = true;

            // Move + Survive. Never freeze: if advancing toward the opponent is
            // blocked by a gap/edge, patrol the current platform so the bot keeps
            // moving instead of parking (and DecideVertical climbs to find a route).
            int moveDir = DecideMoveDir(absDx, towardOpp);
            moveDir = ApplyEdgeGuard(pos, moveDir);
            if (moveDir == 0 && _self.isGrounded)
                moveDir = SafePatrolDir(pos);
            _moveX = moveDir;

            DecideVertical(pos, target);
        }

        private int DecideMoveDir(float absDx, int towardOpp)
        {
            // Always moving — the bot never parks. Close in when out of range;
            // once near, patrol back and forth (harder to hit, keeps repositioning
            // to line up a shot on the platform under the opponent).
            if (absDx > preferredRange) return towardOpp;

            if (Time.time >= _patrolFlipTime)
            {
                _patrolFlipTime = Time.time + Random.Range(0.5f, 1.2f);
                _patrolDir = -_patrolDir;
            }
            return _patrolDir;
        }

        // Keep moving on the current platform when progress is blocked — oscillate
        // toward whichever side still has standable ground under it.
        private int SafePatrolDir(Vector2 pos)
        {
            if (Time.time >= _patrolFlipTime)
            {
                _patrolFlipTime = Time.time + Random.Range(0.4f, 1.0f);
                _patrolDir = -_patrolDir;
            }
            if (HasGround(pos + new Vector2(_patrolDir * edgeLookAhead, 0.2f), _standableMask)) return _patrolDir;
            if (HasGround(pos + new Vector2(-_patrolDir * edgeLookAhead, 0.2f), _standableMask)) { _patrolDir = -_patrolDir; return _patrolDir; }
            return 0; // boxed in on a tiny platform — DecideVertical's climb gets us out
        }

        // Never walk off a ledge into the void. If own-colour ground continues
        // ahead, go. If only opponent-colour ground is ahead, stop and paint it
        // (fire; the aim toward the opponent paints intervening platforms too).
        // If nothing is ahead, hold.
        private int ApplyEdgeGuard(Vector2 pos, int moveDir)
        {
            if (moveDir == 0) return 0;
            if (!_self.isGrounded) return moveDir; // air control continues an in-progress leap

            Vector2 probe = pos + new Vector2(moveDir * edgeLookAhead, 0.2f);
            if (HasGround(probe, _standableMask)) return moveDir;            // safe to step

            // Gap ahead. If a standable platform sits within a jump's reach in
            // that direction, LEAP it — keep moveDir so air control carries us
            // across (double jump extends the reach if the first isn't enough).
            if (StandableWithinJumpReach(pos, moveDir))
            {
                QueueJump();
                return moveDir;
            }

            // Can't cross. Paint an adjacent opponent-colour platform if present, then hold.
            if (HasGround(probe, _paintableMask)) { _fireIntent = true; return 0; }
            return 0;                                                        // abyss: hold
        }

        // Any own-colour / floor platform to land on within a horizontal jump window.
        private bool StandableWithinJumpReach(Vector2 pos, int dir)
        {
            for (float ahead = 3f; ahead <= jumpReach; ahead += 2f)
            {
                Vector2 p = pos + new Vector2(dir * ahead, 1f);
                if (HasGround(p, _standableMask, 6f)) return true;
            }
            return false;
        }

        // Falling: steer toward standable ground below; if only opponent-colour
        // ground is below, aim down and paint-to-land; else pulse a save jump.
        private void DecideVertical(Vector2 pos, Vector2 target)
        {
            if (_self.isGrounded)
            {
                // Seek height: find a higher platform reachable straight up OR
                // diagonally, jump for it and drift toward it — so the bot climbs
                // offset/staircase platforms, not only ones directly overhead.
                if (Time.time >= _nextClimbTime && Random.value < 0.6f + 0.4f * Aggression)
                {
                    int side = HigherPlatformSide(pos);
                    if (side != NoHigherPlatform)
                    {
                        _nextClimbTime = Time.time + Random.Range(0.3f, 0.7f);
                        QueueJump();
                        if (side != 0) _moveX = side; // steer onto the offset platform
                    }
                }
                return;
            }

            if (_rb == null || _rb.linearVelocity.y >= 0f) return; // only act while falling

            float drift = Mathf.Clamp(_rb.linearVelocity.x * 0.2f, -3f, 3f);
            Vector2 below = pos + new Vector2(drift, 0f);

            if (HasGround(below, _standableMask, 12f)) return; // will land safely
            if (HasGround(below, _paintableMask, 12f))
            {
                _desiredAim = Vector2.down;
                _fireIntent = true;
                return;
            }

            _moveX = target.x >= pos.x ? 1 : -1;
            QueueJump();
        }

        // ---- Controller interface (read by PlayerManager) ---------------------
        protected override float update_moving_direction() => _moveX;

        protected override Vector2 update_aim_direction()
        {
            // During a burst, offset the aim by that burst's error so misses read
            // as human. Computed from the clean base aim each frame (never mutate
            // _baseAim, or the error would compound and the aim would spin).
            return _bursting ? Rotate(_baseAim, _burstAimErrorRad) : _baseAim;
        }

        public override bool jump()
        {
            // One-shot latch: a held 'true' would burn the double-jump at apex.
            if (!_jumpQueued) return false;
            _jumpQueued = false;
            return true;
        }

        public override bool shoot() => _fireHeld;
        public override bool getDown() => false;   // dropping through our own platform is suicide
        public override bool pauseMenu() => false; // must be constant false (polled without an enabled check)

        // ---- Fire cadence -----------------------------------------------------
        private void UpdateBurstFire()
        {
            if (!_fireIntent)
            {
                _bursting = false;
                _fireHeld = false;
                return;
            }

            // Self-sustaining on/off cycle while engaged, independent of the
            // think tick — always gives PlayerManager release frames.
            if (Time.time >= _burstPhaseEndTime)
            {
                _bursting = !_bursting;
                _burstPhaseEndTime = Time.time + (_bursting ? BurstOn : BurstOff);
                if (_bursting)
                    _burstAimErrorRad = Random.Range(-AimErrorDeg, AimErrorDeg) * Mathf.Deg2Rad;
            }

            _fireHeld = _bursting;
        }

        private void SmoothAim()
        {
            _baseAim = Vector2.Lerp(_baseAim, _desiredAim, Time.deltaTime * 12f);
            if (_baseAim == Vector2.zero) _baseAim = _desiredAim;
        }

        // ---- Sensing helpers --------------------------------------------------
        private void AcquireOpponent()
        {
            foreach (var go in GameObject.FindGameObjectsWithTag(Values.PLAYER_TAG))
            {
                if (go == gameObject) continue;
                var st = go.GetComponent<PlayerState>();
                if (st != null && st.player_framework != _selfFramework)
                {
                    _opponent = go.transform;
                    return;
                }
            }
        }

        private void RecordOpponentSample()
        {
            if (_opponent == null) return;
            _oppPosHist[_histHead] = _opponent.position;
            _oppTimeHist[_histHead] = Time.time;
            _histHead = (_histHead + 1) % HistorySize;
            if (_histCount < HistorySize) _histCount++;
        }

        private Vector2 DelayedOpponentPosition()
        {
            if (_opponent == null) return _rb != null ? _rb.position : (Vector2)transform.position;
            float wantTime = Time.time - ReactionDelay;
            for (int i = 1; i <= _histCount; i++)
            {
                int idx = (_histHead - i + HistorySize) % HistorySize;
                if (_oppTimeHist[idx] <= wantTime)
                    return _oppPosHist[idx];
            }
            int oldest = (_histHead - _histCount + HistorySize) % HistorySize;
            return _histCount > 0 ? _oppPosHist[oldest] : (Vector2)_opponent.position;
        }

        private bool OpponentGrounded()
        {
            if (_opponent == null) return false;
            var pm = _opponent.GetComponent<PlayerManager>();
            return pm != null && pm.isGrounded;
        }

        private bool HasGround(Vector2 origin, int mask) => HasGround(origin, mask, groundProbeDistance);

        private bool HasGround(Vector2 origin, int mask, float dist)
        {
            // Live raycast every call — paint moves platforms between layers, so
            // standability must never be cached.
            return Physics2D.Raycast(origin, Vector2.down, dist, mask).collider != null;
        }

        private const int NoHigherPlatform = -2;

        // Direction of the nearest higher platform reachable by a jump: 0 = straight
        // up, -1 / +1 = up-and-to-that-side, NoHigherPlatform = none within reach.
        private int HigherPlatformSide(Vector2 pos)
        {
            Vector2 from = pos + Vector2.up * 1.2f;
            int mask = _standableMask | _paintableMask;
            if (Physics2D.Raycast(from, Vector2.up, jumpReach, mask).collider != null) return 0;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 dir = new Vector2(s * 0.7f, 1f).normalized;
                if (Physics2D.Raycast(from, dir, jumpReach, mask).collider != null) return s;
            }
            return NoHigherPlatform;
        }

        private Vector2 FacingAim()
        {
            var view = GetComponent<PlayerView>();
            bool left = view != null && view.facingLeft;
            return left ? Vector2.left : Vector2.right;
        }

        private void QueueJump() => _jumpQueued = true;

        private bool IsFrozen()
        {
            return GameManager.CountdownActive || (_self != null && _self.ControlsDisabled);
        }

        private void DisableComponent<T>() where T : MonoBehaviour
        {
            var c = GetComponent<T>();
            if (c != null) c.enabled = false;
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float cs = Mathf.Cos(radians), sn = Mathf.Sin(radians);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }

#if UNITY_EDITOR
        // Editor-only difficulty toggle for playtesting: '[' easier, ']' harder
        // (clamped 1..6). Compiled out of player builds.
        private void HandleDebugDifficultyKeys()
        {
            if (Input.GetKeyDown(KeyCode.RightBracket)) SetTier(difficultyTier + 1);
            else if (Input.GetKeyDown(KeyCode.LeftBracket)) SetTier(difficultyTier - 1);
        }

        private void SetTier(int tier)
        {
            difficultyTier = Mathf.Clamp(tier, 1, TierCount);
            Debug.Log($"[BotController] Difficulty tier → {difficultyTier} " +
                      $"(reaction {ReactionDelay:0.00}s, aimErr {AimErrorDeg:0.#}°, " +
                      $"burst {BurstOn:0.00}/{BurstOff:0.00}, aggr {Aggression:0.00})");
        }

        // On-screen debug window: current tier + easier/harder buttons.
        private void OnGUI()
        {
            if (!showDebugWindow) return;

            const float w = 208f, h = 78f;
            var box = new Rect(12f, 12f, w, h);
            GUI.Box(box, "AI Bot — debug");

            GUI.Label(new Rect(box.x + 12f, box.y + 24f, w - 24f, 20f),
                $"Difficulty: {difficultyTier} / {TierCount}");

            if (GUI.Button(new Rect(box.x + 12f, box.y + 46f, 46f, 22f), "◀ easy"))
                SetTier(difficultyTier - 1);
            if (GUI.Button(new Rect(box.x + 62f, box.y + 46f, 52f, 22f), "hard ▶"))
                SetTier(difficultyTier + 1);
            GUI.Label(new Rect(box.x + 120f, box.y + 48f, w - 130f, 20f), "or  [  ]");
        }
#endif
    }
}
