# Tech Spec — Paint Grenade Power-Up

> **Audience:** an implementing agent with no prior context on this repo.
> **Read `AGENT_CONTEXT.md` first** (repo conventions, Unity version, MP architecture).
> **Branch:** implement on `feature/paint-grenade` (already created from `Multiplayer`).
> **Engine:** Unity 6000.4.4f1. **Networking:** Photon PUN2. **MP build target:** WebGL.

---

## 0. Preconditions

- Work in the main checkout `/Users/nadav/Documents/GitHub/super-inverters-game/`, NOT a Claude worktree (worktrees are on master-based branches without the multiplayer code).
- ~~Uncommitted bug fixes~~ **RESOLVED 2026-07-03:** the 4 bug fixes are committed on `Multiplayer` (`85a1f2e2`) and `feature/paint-grenade` was re-pointed on top of them — the branch already contains everything you need.
- `git checkout feature/paint-grenade` (branch exists; verify with `git branch --list feature/paint-grenade`).

## 1. Feature summary (player experience)

1. Periodically, a **grenade pickup** spawns at a random X at the top of the multiplayer arena and **falls slowly** straight down (passing through platforms). It despawns if it exits the bottom bounds uncollected.
2. A player who touches it **collects one grenade** (inventory capacity: **1**; touching another while holding one does nothing).
3. Holding the throw input shows a **trajectory-arc aim UI** (dotted arc from the player). Direction comes from the existing aim; **power ramps** while held. Releasing throws the grenade.
4. The thrown grenade has **full Rigidbody2D physics**: it flies on the arc, **bounces off platforms** (any color) and walls, and **each bounce damps its velocity** by a tunable factor (so bounce height decays).
5. After a tunable **fuse time** it **detonates at its final position**: every platform whose collider overlaps the **explosion radius** is painted **the thrower's color** (WHITE thrower → platforms turn WHITE), using the existing platform-paint pipeline so both peers stay in sync.
6. Works in the MP scene `level_1-multiplayer`. Single-player support is **out of scope** for v1 (see §10).

## 2. Existing architecture you must reuse (do not reinvent)

| Concern | Existing mechanism | Where |
|---|---|---|
| Painting a platform + physics layer + carried-player release | `PlatformManager.ApplyPaintFromNetwork(Framework)` | `Assets/scripts/Platform/PlatformManager.cs` |
| Syncing paint to the other peer | `GameManager.BroadcastPaintPlatform(networkId, framework)` → `RPCPaintPlatform` (RpcTarget.Others, deliberately NOT buffered) | `Assets/scripts/Game/GameManager.cs` |
| Deterministic per-platform ids across peers | `GameManager.AssignPlatformNetworkIds()` (hierarchy-path sort) | same |
| "Owner simulates, others see a ghost" projectile pattern | `GameManager.SpawnShot` → `RPCSpawnGhostShot` (ghost flag suppresses painting on remotes) | same + `ShotFactory.MakeObject(..., bool isGhost)` |
| Master-authoritative arbitration pattern (copy this shape for pickup claims) | kill flow: `RPCReportKillToMaster` → master decides → `RPCApplyKillResult` to all | `GameManager.cs` |
| Freeze/round gating | `GameManager.CountdownActive`, `GameManager.PlatformMotionEpoch` (< 0 = pre-GO), `IsMatchStartProtectionActive` | same |
| Synced timing base for spawn schedule | `PhotonNetwork.Time` (already used for platform motion epoch) | `PlatformManager.ApplyMultiplayerSyncedPosition` |
| Tags/consts | `Values.cs` (`SHOT_TAG`, `PLAYER_TAG`, `PLATFORM_BODY_TAG`, `BOUNDRIES_TAG`) | `Assets/scripts/Values.cs` |
| Aim direction of local player | `PlayerManager.shootingDirection` (already merged from mouse/keyboard/gamepad) | `Assets/scripts/Player/PlayerManager.cs` |

**Key constraint:** `PlatformShotSensor` reacts to `Values.SHOT_TAG`. The grenade must use a **new tag** (`grenade`) so platform sensors do NOT treat it as a paint-per-hit shot. Its per-bounce contact must not paint anything; paint happens only at detonation.

**RPC plumbing:** all new RPCs live on `GameManager` (it already has the scene `PhotonView`). Do not add PhotonViews to grenades or pickups — they are locally instantiated on each peer and correlated by ids carried in the RPCs, same philosophy as ghost shots.

## 3. New components (all under `Assets/scripts/Powerups/`)

### 3.1 `PowerupSpawner` (scene component, add to the `Bootstrap` object in `level_1-multiplayer` next to `MultiplayerSpawner`)

Master-authoritative scheduler; both peers simulate the identical fall locally.

- Master only, when `PhotonNetwork.InRoom && IsMasterClient && PlatformMotionEpoch >= 0` (i.e. after GO): every `spawnInterval` seconds (randomized in `[spawnIntervalMin, spawnIntervalMax]`), pick `spawnX = Random.Range(spawnXMin, spawnXMax)` and a fresh `pickupId` (int, incrementing), then
  `photonView.RPC(nameof(RPCSpawnPowerup), RpcTarget.All, pickupId, spawnX, PhotonNetwork.Time)`.
- `RPCSpawnPowerup(int pickupId, float spawnX, double spawnTime)` on every peer: `Instantiate(grenadePickupPrefab)` at `(spawnX, spawnTopY)`; the pickup positions itself kinematically each frame:
  `y = spawnTopY - (PhotonNetwork.Time - spawnTime) * fallSpeed` — identical on both peers regardless of join latency, same trick as platform motion.
- Registry: `Dictionary<int, GrenadePickup> _activePickups` for claim resolution.
- Suspend scheduling while `CountdownActive`; clear all live pickups on scene reload (they die naturally — scene objects) and when the match ends (`_matchOver` → stop scheduling; a `ClearAllPickups()` called from the same place the end-game path runs).
- Cap concurrent pickups at `maxConcurrentPickups` (default 2): skip a spawn tick if at cap.

### 3.2 `GrenadePickup` (prefab: `Assets/Prefabs/GrenadePickup.prefab`)

- `Rigidbody2D` **Kinematic** + `CircleCollider2D` **isTrigger**. Tag: `powerup` (new). Layer: a new `powerup` layer that collides with nothing (position driven by script; overlap with players detected via trigger + tag check, like `PlayerManager.OnTriggerExit2D` does with boundaries).
- Fields: `pickupId`, `spawnTime`, `fallSpeed` (injected by spawner).
- `OnTriggerEnter2D(Collider2D other)`: if `other.CompareTag(Values.PLAYER_TAG)` and that player's `PhotonView.IsMine` (only the local peer reports for its own avatar — same IsMine gating as the kill path) and the local player's `GrenadeInventory.HasGrenade == false`:
  `GameManager.RPC(nameof(RPCClaimPowerup), RpcTarget.MasterClient, pickupId, PhotonNetwork.LocalPlayer.ActorNumber)`.
- Despawn locally when `y < killY` (read from the boundary trigger's bottom or a serialized `despawnY`).

### 3.3 Claim arbitration (in `GameManager`, copy the kill-flow shape)

```
[PunRPC] RPCClaimPowerup(int pickupId, int actorNumber)   // master only
    if pickup already claimed or unknown -> ignore
    mark claimed
    photonView.RPC(RPCResolvePowerup, RpcTarget.All, pickupId, actorNumber)

[PunRPC] RPCResolvePowerup(int pickupId, int winnerActor)  // every peer
    destroy local pickup instance for pickupId (if still alive)
    if winnerActor == my actor -> localPlayer.GrenadeInventory.Grant()
```

Race outcome: both players touch on the same frame on different peers → master receives two claims, first one wins, second is ignored — identical guarantee to the `_matchOver` latch.

### 3.4 `GrenadeInventory` (component on the player prefabs — add to `Assets/Resources/BlackPlayer.prefab` and `WhitePlayer.prefab`)

- `public bool HasGrenade { get; private set; }` + `Grant()` / `Consume()`.
- Only meaningful on the local avatar (`PhotonView.IsMine`); remote avatars never read it.
- Optional HUD: a small grenade icon near the player's `ShootCooldownHud` — follow that component's pattern. Sprite via `UiArt`-style `Resources` load (`Assets/Resources/UI/Powerups/grenade_icon`). Nice-to-have, not required for v1.

### 3.5 `GrenadeAimController` (component on player prefabs, local-only)

- Active only when `pv.IsMine && GrenadeInventory.HasGrenade && !GameManager.CountdownActive && !GameManager.LocalPauseActive && controls not disabled`.
- Input (do **not** touch the `Controller` hierarchy or `PhotonInputView`'s serialize stream — the throw is replicated by RPC, not by input replication):
  - `throwKey` (default `KeyCode.G`) + optional `throwButton` string for gamepad (serialized, may be empty).
  - **Hold** = aiming: power ramps `power = Mathf.Clamp01(heldTime * powerRampPerSecond)` between `throwForceMin..throwForceMax` — monotonic charge that holds at max (user-confirmed 2026-07-03: hold-to-charge, NOT an oscillating meter). Direction = `PlayerManager.shootingDirection` (fallback: facing dir, same fallback logic as `TryShoot`).
  - **Release** = throw → `GrenadeThrower.Throw(dir, force)`; consume inventory.
  - Pressing the key with no grenade does nothing.
- **Aim UI (trajectory preview):** a `LineRenderer`-free approach — pool of `previewDotCount` (default 12) small sprite dots (children of the player, world-space), positioned analytically each frame while aiming:
  `p(t) = p0 + v0*t + 0.5*g*t²` with `v0 = dir.normalized * force / rb.mass`, `g = Physics2D.gravity * grenadeGravityScale`, sampled at `t = i * previewTimeStep` (default 0.08 s).
  The preview shows the **initial arc only** (no bounce prediction — standard, and honest since bounces damp). Also render a small text or radial fill showing fuse seconds (optional).
  Dots hidden when not aiming. Tint dots the player's color.

### 3.6 `GrenadeThrower` + `GrenadeProjectile` (prefab: `Assets/Prefabs/Grenade.prefab`)

`GrenadeThrower` (on player, local-only): spawns the real grenade and RPCs the ghost:

```
Throw(Vector2 dir, float force):
    v0 = dir.normalized * force
    spawnPos = playerPos + dir.normalized * spawnOffset   // don't spawn inside own collider
    grenade = Instantiate(grenadePrefab, spawnPos, ...)   // real, isGhost=false
    grenade.Init(framework, fuseSeconds, v0, isGhost:false)
    if PhotonNetwork.InRoom:
        gameManager.photonView.RPC(RPCSpawnGhostGrenade, RpcTarget.Others,
                                   spawnPos, v0, (int)framework, fuseSeconds, grenadeId)
```

`grenadeId`: `(ActorNumber << 16) | localCounter` — unique across peers without coordination.

`GrenadeProjectile` (on the prefab):

- `Rigidbody2D` **Dynamic**, `gravityScale = grenadeGravityScale`, `CircleCollider2D` (NOT trigger), `PhysicsMaterial2D` with `bounciness = baseBounciness`, `friction` low.
- Tag `grenade`; layer: reuse the **shot** collision layer setup so it collides with all three platform layers + floor + walls (check the Shot prefab's layer in the editor and mirror it). It must NOT collide with players (matrix) — simplest: same layer as shots if shots already ignore players, else new `grenade` layer configured in the Physics2D matrix: collide platforms_black, platforms_white, platforms_grey, floor, walls; ignore player layers, powerup, shots.
- **Bounce damping** (the "each bounce reduces height" control): in `OnCollisionEnter2D`:
  `rb.linearVelocity *= bounceVelocityRetention;` (default 0.6; 1 = no decay). PhysicsMaterial bounciness handles the reflection; this scalar handles the decay — two independent tunables.
- **Fuse:** `fuseSeconds` counted in `Update` with `Time.deltaTime` (scaled time — pause freezes it, correct). At zero → `Detonate()`.
- **Bounds:** on `OnTriggerEnter2D`/`Exit2D` with `Values.BOUNDRIES_TAG` (mirror `ShotView`'s handling — check whether boundaries are enter or exit there and match it): despawn **without** detonating (tunable bool `detonateOnBoundsExit`, default false).
- **Ghost mode** (`isGhost == true`, spawned by `RPCSpawnGhostGrenade` on remote peers): fully simulated locally (same initial conditions; platform poses match because platform motion is epoch-synced), but `Detonate()` does **visuals only** — no paint, no RPCs. Divergence between real and ghost trajectories is acceptable for flight; the detonation position is corrected:
- **Detonation sync:** the real grenade's `Detonate()`:
  1. `photonView.RPC(RPCDetonateGrenade, RpcTarget.Others, grenadeId, (Vector2)transform.position)` — remote finds its ghost by id, teleports it to the authoritative position, plays the explosion visual, destroys it.
  2. Applies paint locally (below) — the per-platform paint RPCs make the remote's *platforms* correct even though its ghost was only cosmetic.

### 3.7 Detonation paint (the core function)

On the **real** grenade only:

```
Detonate():
    hits = Physics2D.OverlapCircleAll(pos, explosionRadius, platformLayersMask)
        // mask = platforms_black | platforms_white | platforms_grey
    painted = HashSet<PlatformManager>()
    foreach hit in hits:
        pm = hit.GetComponentInParent<PlatformManager>()   // same pattern as FindPlatformBelow
        if pm == null or pm in painted: continue
        painted.Add(pm)
        pm.ApplyPaintFromNetwork(throwerFramework)          // local apply: color + layer + carried release
        if PhotonNetwork.InRoom:
            gameManager.BroadcastPaintPlatform(pm.networkId, throwerFramework)  // remote apply
    play explosion SFX/VFX at pos
    Destroy(gameObject)
```

Notes:
- `ApplyPaintFromNetwork` (not `UpdateHit`) — it sets color + collision layer + releases mismatched carried players, and does **not** re-broadcast, so no feedback loop; we broadcast explicitly per platform.
- Painting to the thrower's OWN color — a platform already that color is still fine to re-apply (idempotent), but you may skip via `PlatformState.platform_framework` check like `EnsureSpawnPlatformMatchesPlayer` does.
- GREY platforms: after `Init()` they are BLACK or WHITE; radius paint just overwrites. No special case.

### 3.8 New RPCs on `GameManager` (summary table)

| RPC | Target | Args | Purpose |
|---|---|---|---|
| `RPCSpawnPowerup` | All | pickupId, spawnX, spawnTime | deterministic falling pickup on every peer |
| `RPCClaimPowerup` | MasterClient | pickupId, actorNumber | claim request |
| `RPCResolvePowerup` | All | pickupId, winnerActor | destroy pickup everywhere; winner gains inventory |
| `RPCSpawnGhostGrenade` | Others | pos, v0, framework, fuse, grenadeId | cosmetic grenade on remote |
| `RPCDetonateGrenade` | Others | grenadeId, pos | snap ghost to authoritative detonation point |

All follow existing conventions: plain (non-buffered) targets, ints for enums, `Vector2` for positions.

## 4. Tunables (all `[SerializeField]` with `[Tooltip]`, grouped with `[Header]`)

| Variable | Component | Default | Meaning |
|---|---|---|---|
| `spawnIntervalMin/Max` | PowerupSpawner | 15 / 25 s | random period between pickup spawns |
| `fallSpeed` | PowerupSpawner | 1.5 u/s | pickup descent speed ("falls slowly") |
| `spawnXMin/Max` | PowerupSpawner | arena width | horizontal spawn range |
| `maxConcurrentPickups` | PowerupSpawner | 2 | cap on live pickups |
| `throwForceMin/Max` | GrenadeAimController | 8 / 28 | power ramp bounds |
| `powerRampPerSecond` | GrenadeAimController | 0.8 | ramp speed (clamped at max, no ping-pong) |
| `previewDotCount` / `previewTimeStep` | GrenadeAimController | 12 / 0.08 | arc UI resolution |
| `fuseSeconds` | GrenadeProjectile | 2.5 s | time to detonation after throw |
| `grenadeGravityScale` | GrenadeProjectile | 1.0 | arc shape |
| `baseBounciness` | PhysicsMaterial2D asset | 0.55 | reflection strength |
| `bounceVelocityRetention` | GrenadeProjectile | 0.6 | per-bounce energy keep (the decay metric) |
| `explosionRadius` | GrenadeProjectile | 6 u | paint radius around final position |
| `detonateOnBoundsExit` | GrenadeProjectile | false | explode vs vanish at arena edge |
| `spawnOffset` | GrenadeThrower | 1.2 u | spawn distance from player center |

## 5. Assets

- **Sprite: USER PROVIDES the grenade sprite** (caveat from the feature request). Until then use a placeholder (Unity built-in `Knob` sprite or a 32×32 circle) wired into both `Grenade.prefab` and `GrenadePickup.prefab` — one `SpriteRenderer` each, so the swap is a 2-slot drag-and-drop. Ask the user for the sprite before polish; do not block implementation on it.
- Pickup should be visually distinct while falling (e.g. slow spin: `transform.Rotate(0,0,pickupSpinDegPerSec*dt)` — cosmetic).
- Explosion VFX: v1 = simple expanding-circle sprite flash + reuse an existing SFX (`PlatformSFX` has paint sounds; check `Assets/Audio`). Do not build a particle system unless trivial.
- New tags `grenade`, `powerup` and (if needed) layer `grenade`, `powerup` — added via Project Settings; document in the PR description that the Physics2D matrix changed (scene/project settings diffs are easy to miss in review).

## 6. Scene wiring checklist (`level_1-multiplayer.unity`)

1. `Bootstrap` object: add `PowerupSpawner`, assign `GrenadePickup.prefab`, set spawn X range to the arena width (use the existing spawn point Xs in `MultiplayerSpawner` as reference, roughly -40..38), `spawnTopY` just above the camera top.
2. Player prefabs (`Assets/Resources/BlackPlayer.prefab`, `WhitePlayer.prefab`): add `GrenadeInventory`, `GrenadeAimController`, `GrenadeThrower`; assign `Grenade.prefab` + preview-dot sprite.
3. `Grenade.prefab` + `GrenadePickup.prefab` in `Assets/Prefabs/` — deliberately NOT in `Assets/Resources`: nothing `PhotonNetwork.Instantiate`s them (they're locally instantiated on each peer), so they're plain inspector-slot references.
4. Physics2D matrix per §3.6.

## 7. Gating & lifecycle rules (get these right — this codebase has been burned before)

- No pickups and no throwing before GO: gate on `PlatformMotionEpoch >= 0 && !CountdownActive`.
- Round death → scene does NOT reload in MP (respawn only) — live grenades/pickups **persist** through a round death. That's fine and intended.
- Game over (`_matchOver` / `endGame`): stop the spawner; destroy live grenades without detonating (no paint after the match is decided). Hook: the same code path that runs `endGame` on each peer.
- Rematch reloads the scene via `PhotonNetwork.LoadLevel` → everything scene-owned dies naturally; `PowerupSpawner` state must be instance (non-static) so reload resets it. **Do not add statics** unless reset in `Awake` (see the `s_matchStartCountdownPlayed` pattern and its comments).
- Pause (synced, timeScale 0): physics and fuse freeze automatically (scaled time) — verify, don't assume.
- Player dies while holding a grenade: **loses it** (v1). This is the natural behavior, not extra work: `MultiplayerSpawner.ForceRespawn` destroys and re-instantiates the avatar, so `GrenadeInventory` state resets to empty. Do NOT add persistence plumbing to keep it — revisit only if playtesting says otherwise.
- Player disconnects mid-claim: master's claim registry keys off pickupId, not actor state — a resolve to a gone actor is harmless (no peer matches `winnerActor`).

## 8. Implementation slices (one commit per slice — review each diff before starting the next)

Follow the repo's vertical-slice convention (see "Planned multiplayer integration" section above). Rules for every slice: it **compiles with zero `error CS`**, it is **testable on its own** via the listed gate, and it ends in **exactly one commit** on `feature/paint-grenade` using the given message prefix so the user can review slice-by-slice. Do not start slice N+1 until slice N is committed.

### Slice G1 — Grenade projectile core (local only)
- **Build:** `Grenade.prefab` (placeholder sprite), `Grenade Material.physicsMaterial2D`, new tag `grenade` (+ layer & Physics2D matrix per §3.6), `GrenadeProjectile` (physics, `bounceVelocityRetention` damping, fuse, bounds despawn), detonation radius paint **local-path only** (§3.7 without the broadcast), and a temporary editor-only debug spawn key to lob grenades from the mouse position.
- **Files:** `Assets/scripts/Powerups/GrenadeProjectile.cs`, prefab + material assets, ProjectSettings (tags/layers/matrix).
- **Gate:** in one editor, spawn grenades; bounces visibly decay (compare retention 1.0 vs 0.4); fuse detonates; platforms inside radius flip color; zero console errors.
- **Commit:** `Slice G1: grenade projectile physics + local radius paint`

### Slice G2 — Throw input, charge, arc preview
- **Build:** `GrenadeInventory` (granted via the debug key for now), `GrenadeAimController` (clamped charge ramp, preview dots, gating per §3.5), `GrenadeThrower` (spawn offset, consume). Remove/repurpose G1's direct-spawn debug key into "grant grenade" debug key.
- **Files:** `GrenadeInventory.cs`, `GrenadeAimController.cs`, `GrenadeThrower.cs`, player prefab edits (`Assets/Resources/{Black,White}Player.prefab`).
- **Gate:** hold key → arc appears and power visibly ramps then holds at max; release → grenade follows the previewed initial arc; can't throw without a grenade; one grenade per grant.
- **Commit:** `Slice G2: hold-to-charge throw with trajectory preview`

### Slice G3 — Falling pickup + collection (local logic)
- **Build:** `GrenadePickup.prefab` + `GrenadePickup.cs` (kinematic fall, player trigger detect, despawn below bounds), `PowerupSpawner` scheduling running **locally** (no RPCs yet), single-slot rule (touch while holding = no-op).
- **Files:** `PowerupSpawner.cs`, `GrenadePickup.cs`, pickup prefab, scene wiring (`Bootstrap` in `level_1-multiplayer.unity`).
- **Gate:** pickups appear on the configured interval at random X, fall slowly, collecting grants exactly one grenade, uncollected ones vanish at the bottom.
- **Commit:** `Slice G3: falling grenade pickup + collection (local)`

### Slice G4 — Networked pickups (spawn + claim arbitration)
- **Build:** convert the spawner to master-authoritative: `RPCSpawnPowerup` (epoch-based deterministic fall), `RPCClaimPowerup` → master → `RPCResolvePowerup` (§3.3), pickup registry, IsMine-gated claim reporting.
- **Files:** `GameManager.cs` (RPCs), `PowerupSpawner.cs`, `GrenadePickup.cs`.
- **Gate (2-peer ParrelSync):** pickups fall identically on both peers; simultaneous-grab race gives the grenade to exactly ONE player and the pickup vanishes on both.
- **Commit:** `Slice G4: master-authoritative pickup spawn + claim`

### Slice G5 — Networked grenade (ghost + detonation sync + paint broadcast)
- **Build:** `RPCSpawnGhostGrenade` (ghost mode per §3.6), `RPCDetonateGrenade` (snap ghost to authoritative position), per-platform paint broadcast at detonation (§3.7 full version), `grenadeId` scheme.
- **Files:** `GameManager.cs`, `GrenadeThrower.cs`, `GrenadeProjectile.cs`.
- **Gate (2-peer):** thrower's grenade appears + flies + explodes on BOTH editors at the same final position; painted platform sets identical (walk both players onto painted platforms — no fall-through on either editor).
- **Commit:** `Slice G5: grenade ghost + synced detonation paint`

### Slice G6 — Lifecycle gating, HUD, polish
- **Build:** all §7 rules (pre-GO gating, game-over cleanup, rematch reset, pause behavior verification), HUD grenade icon (§3.4 optional part), pickup spin/explosion flash cosmetics.
- **Files:** `GameManager.cs` end-game hooks, `PowerupSpawner.cs`, `GrenadeHud` (new, modeled on `ShootCooldownHud.cs`).
- **Gate:** full §9 test matrix passes, including WebGL smoke build.
- **Commit:** `Slice G6: grenade lifecycle gating + HUD polish`

## 9. Test plan (2-peer ParrelSync, per AGENT_CONTEXT technique — Editor.log grep works when MCP is down)

- [x] Pickup falls identically on both peers (visually compare Y at same wall-clock moment).
- [x] Simultaneous grab race: both players stand in the fall path → exactly ONE gets it, pickup vanishes on both peers.
- [x] Throw on peer A: peer B sees ghost arc + explosion at the same final position; painted platforms identical on both (walk both players onto a painted platform — no fall-through on either editor: the real desync detector in this game).
- [ ] Radius correctness: platforms partially inside radius get painted; platforms outside don't.
- [ ] Bounce decay visibly tunable: retention 1.0 vs 0.4 comparison.
- [ ] Fuse in pause: pause mid-flight → grenade freezes, resumes correctly.
- [ ] Game over with grenade mid-air → no paint after verdict, no errors.
- [ ] Rematch: spawner restarts fresh, no duplicate pickups, ids restart safely (registry cleared).
- [ ] WebGL smoke build (`Scripts/build-webgl.sh`) still compiles — WebGL is the ship target.

## 10. Out of scope (v1)

- Single-player grenade support (two humans share one keyboard; input mapping needs design).
- Bounce-aware trajectory preview.
- Multiple stacked grenades / other power-up types (but keep `PowerupSpawner`/pickup generic-ish: pickup grants "a power-up" via one method call, so a second power-up type later is a small diff).
- Player-adjustable fuse time in the aim UI (fuse is a designer tunable in v1; the UI displays it read-only at most).

## 11. Done criteria

All §9 boxes checked in a 2-peer Editor playtest, zero `error CS`, tunables all live in inspectors with tooltips, **exactly one commit per slice (G1–G6) with the §8 commit messages** so each diff is reviewable in isolation, AGENT_CONTEXT.md update-log entry appended describing what shipped and any deviations from this spec.
