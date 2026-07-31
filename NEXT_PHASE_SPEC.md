# NEXT_PHASE_SPEC — Road to the itch.io release

> **Audience:** the agent working the sessions after the grenade feature (see
> `GRENADE_FEATURE_SPEC.md` for the previous phase and its conventions — this doc follows
> the same vertical-slice + test-checklist format). Read `AGENT_CONTEXT.md` FIRST every
> session; it has the non-negotiables (credentials, engine version, editor workflow).
> User-set priority order: **single-player → all-levels multiplayer → UI → release.**

---

## 0. Ground rules (carried over — do not relearn these the hard way)

- **Branch flow:** the grenade PR merges `feature/paint-grenade` → `Multiplayer`. Every
  slice below starts from an up-to-date `Multiplayer` (`git checkout Multiplayer && git
  pull`) on a NEW branch (suggested names per slice below). Never commit to `master`.
- **The user pushes.** Agent commits locally, writes the push command for the user
  (`git push origin <branch>`); the agent's shell cannot reach Keychain credentials.
- **Photon App ID is a credential** — `PhotonServerSettings.asset` stays gitignored. Never
  commit it; repo is public GitHub.
- **Debug tooling** must be `#if UNITY_EDITOR` (compiled out of WebGL). The grenade-era
  debug kit (overlay panel, audio probes, H-key pickup drop) was stripped in `7791fc48` —
  `git revert 7791fc48` resurrects all of it if needed.
- **Two-editor verification workflow:** main editor + ParrelSync clone (currently
  `clone_6`). Editors defer recompiles until focused; after editing scripts, request a
  compile via Unity MCP (`refresh_unity`) **per editor** (`set_active_instance` first) and
  verify the new code is loaded via a reflection probe on a new symbol before the user
  playtests. Instance names live in `~/Library/Application Support/UnityMCP/Logs/`.
- **Unity 6000.4.4f1 on macOS has a native engine deadlock** on synchronous scene loads
  during Photon teardown (see AGENT_CONTEXT 2026-05-23 + 2026-07-11). Worked around with
  the deferred `LoadSceneAsync` runner in `GameManager.LoadMainMenuScene`. If ANY editor
  hard-freezes: `sample <pid>` BEFORE force-quitting; keep scene loads async on exit paths.
- **Time freeze conventions:** synced pause AND game-over set `Time.timeScale = 0`. New
  UI/animation on those screens must use unscaled time; new gameplay must use scaled time
  so it freezes correctly. PUN keeps dispatching at timeScale 0
  (`MinimalTimeScaleToDispatchInFixedUpdate = 1` in `GameManager.Awake`).

## 1. Current state (post-merge)

Works (multiplayer, ONE arena — `level_1-multiplayer`): 2-peer Photon matches, synced
platforms (epoch-based), master-authoritative kills/game-over, synced pause, clean exits,
paint grenade feature complete (throw/charge/mid-air detonate, ghost replication, pickups
with 3-cap economy, HUD icons, collect animation).

Known gaps / open bugs:
- **No working single-player** (exact breakage undiagnosed — Slice S1 starts with diagnosis).
- **Level select is skipped in MP:** `MultiplayerBootstrap.TryLoadGameScene()` hardwires
  `MultiplayerSceneNames.GameSceneName = "level_1-multiplayer"` when the room fills.
- **G4 not done:** pickups spawn per-peer on independent random timers (no master
  arbitration; simultaneous-grab race unresolved). Kickoff plan in AGENT_CONTEXT (session-4
  entry). `PowerupSpawner.ClearAllPickups()` exists but is not wired to end-game.
- **Runtime sound dropout** (editor-observed): cornered to `PlayerManager.cs` ~line 503
  `if (EnableSFX) _sfx.PlayShoot();` — during dropout PlayShoot is NOT called while bullets
  still fire; state heals on scene reload. Diagnostics stripped; revert `7791fc48` to re-arm.
- The scene flow: `main_menu` → (`Multiplayer` lobby scene | `level_menu`) →
  `level_1..5` (SP) or `level_1-multiplayer` (MP).

## 2. Slices

### S1 — Single-player works again (branch: `feature/single-player`)

Goal: main_menu → Single Player → level_menu → any level → a full local 2-players-1-keyboard
match with lives, kills, game over, rematch.

1. **Diagnose first, fix second.** Play `level_1` in the editor and write down what
   actually breaks. Prime suspects (all from the MP retrofit):
   - SP scenes contain scene-placed player objects — do they still exist / have the
     current component set? (MP instantiates `Assets/Resources/{Black,White}Player.prefab`
     at runtime instead.)
   - `GameManager` paths: `PlayerKilled` → `DoPlayerKilled` (non-networked branch exists
     and is guarded by `PhotonNetwork.InRoom` — verify it still runs), countdown, rematch.
   - `PlatformManager`: local motion path is intact (`_usedSyncedMotion` only freezes
     platforms after a room session; irrelevant in SP).
   - HUD (`ShootCooldownHud`) finds "the local player" via PhotonView-IsMine — in SP both
     players are local; verify it degrades sanely (pv == null branch).
2. Keep fixes surgical; SP and MP share almost all code — prefer `PhotonNetwork.InRoom`
   guards over forks of logic.

### S2 — Grenades in single-player (same branch)

Goal: both local players can use grenades in SP with the same feel as MP.

- Grenade components (`GrenadeThrower`, `GrenadeInventory`, `GrenadeAimController`) live on
  the two Resources player prefabs. If SP uses different player objects, port the component
  set (and input mapping — two players on one keyboard need distinct grenade keys; G is
  player-keyboard-specific today, check `GrenadeAimController` input source).
- `GrenadePickup.NetworkNow` already falls back to `Time.timeAsDouble` outside a room; the
  analytic fall works offline by design.
- Add a `PowerupSpawner` to SP levels (it lives on the MP scene's Bootstrap object today).
- Ghost RPCs are `InRoom`-guarded in `GrenadeThrower` — verify no RPC is attempted offline.
- HUD grenade rows: in SP BOTH players need icon rows (today: one local row per peer).

### S3 — Level select + all levels in multiplayer (branch: `feature/mp-level-select`)

Goal: after the room fills, players see the level-select screen and every level is playable
in MP with full functionality (synced platforms, kills, pause/exit, grenades, pickups).

1. **Selection flow:** replace the hardwired `TryLoadGameScene()` jump. Simplest robust
   design (decide with user if deviating): master picks on a level-select screen; the pick
   is written as a room property (or just `PhotonNetwork.LoadLevel(picked)` — with
   `AutomaticallySyncScene = true` the guest follows automatically). Guest sees a
   "Master is choosing a level…" state, NOT a dead screen.
2. **Per-level MP enablement.** `level_1-multiplayer` is the template. Each level needs:
   - the Bootstrap object (`MultiplayerSpawner` + `PowerupSpawner` + scene-gate name —
     `MultiplayerSpawner` has a "only spawn when this scene is active" gate that must
     match each scene);
   - spawn-position arrays (`blackSpawnPositions`/`whiteSpawnPositions`) placed on
     platforms per level (Scene-view drag handles exist — see AGENT_CONTEXT);
   - platform networkId sanity: ids derive from sorted hierarchy paths per scene — both
     peers load the same scene file so this generalizes, but VERIFY per level with the
     paint-a-platform-and-walk-on-it test (the real desync detector);
   - GameManager/end-menu/pause objects present and wired (copy the template scene's).
   - Decide: duplicate scenes (`level_N-multiplayer`) vs. retrofitting the SP scenes to
     serve both. Duplication is safer short-term but doubles maintenance — surface the
     trade-off to the user before committing to one.
3. **Fold G4 in here** (master-authoritative pickups) — this is the natural time: master
   picks `(pickupId, spawnX, spawnTime=PhotonNetwork.Time)` → `RPCSpawnPowerup(All)` →
   existing `SpawnPickup`; claims via `RPCClaimPowerup(MasterClient)` →
   `RPCResolvePowerup(All)`; gate `PowerupSpawner.Update` scheduling on `IsMasterClient`;
   wire `ClearAllPickups()` into end-game. Full plan: AGENT_CONTEXT session-4 kickoff.

### S4 — UI refinement (branch: `feature/ui-polish`)

Scope with the user at session start (they said "visuals and possible UI bugs and whatever
it is"). Known candidates: pause-menu placeholder buttons, lobby screens (Create/Join
states, error surfacing), level-select visuals (both SP and the new MP one), end-menu
word-art on all levels, HUD placement across level layouts, main-menu polish. Screenshot
workflow: user screenshots, agent adjusts, bake final values.

### S5 — Release to itch.io

- WebGL build via `Scripts/build-webgl.sh` (headless; main editor must be CLOSED — it
  takes the project lock). Serve-test via `Scripts/serve-webgl.sh`.
- 2-browser MP smoke test of the WebGL build (the ship target — editor-passing ≠ shipped).
- Deploy via `Scripts/deploy-itch.sh` (butler). Agent can deploy; **the user must toggle
  itch visibility to Public manually** (Cloudflare blocks automated login).
- **CC-BY credit is REQUIRED on the itch page:** explosion sound = "Big Explosion"
  (DeathFlash) by Blender Foundation, opengameart.org, CC-BY 3.0.
- Confirm no editor-only/debug code in the diff; `PhotonServerSettings.asset` untracked.

## 3. Test checklist (tick as verified; annotate with date like §9 of the grenade spec)

### S1/S2 — Single-player
- [x] main_menu → Single Player → level_menu shows and is navigable — 2026-08-01, user
      (keyboard + mouse; includes the new BOT DIFFICULTY stepper. Gamepad nav untested.)
- [x] Each of level_1..5 loads and a full match plays vs the bot: move, jump, shoot, kills
      decrement lives, game over shows the right winner — 2026-08-01, user ("tested all
      levels, it works"). Bot survives + fights on every level (automated sweeps + user).
- [x] Grenades in SP: human charge/throw/mid-air-detonate; BOT throws 45° lobs on a
      6–12s cooldown; explosion paints platforms — 2026-08-01 (user levels 1–2 + all-level
      pass; bot throw verified live). NOTE: the bot does NOT mid-air-detonate (future).
- [x] Pickups fall in SP; 3-start/3-cap/+2 economy; bot contests pickups — 2026-08-01.
      HUD grenade row exists for the LOCAL player only (bot count not displayed — fine).
- [~] Game over freezes the world and the last life icon animates (unscaled) — verified.
      Pause-freezes-grenades in SP specifically: not yet explicitly re-tested.
- [ ] No Photon calls fire offline (console clean of PUN warnings/errors in a full SP loop).

### S3 — Multiplayer levels
- [ ] Room fills → level select appears (master chooses; guest sees waiting state).
- [ ] EVERY level: both peers land in the same scene, spawn on their color platforms,
      countdown runs, platforms move identically (epoch), paint syncs (walk-on-painted
      test), kills/game-over agree, pause/exit clean on both.
- [ ] Grenade ghost replication correct on every level (arc + explosion position + paint).
- [ ] G4: pickups appear at the same place/time on both peers; simultaneous grab → exactly
      one winner, pickup vanishes on both; loser can grab the next one (no lockout).
- [ ] Rematch on every level: spawner restarts fresh, no duplicate pickups, registry clear.
- [ ] Game over with grenade mid-air (re-run post-G4 §9 leftovers: falls-identically +
      grab-race from GRENADE_FEATURE_SPEC §9).

### S4/S5 — UI + release
- [ ] UI pass signed off by user (list concrete items when scoped).
- [ ] WebGL build compiles headless; boots in browser to main menu with no console errors.
- [ ] WebGL 2-browser MP match end-to-end (create/join/play/game-over/rematch/exit).
- [ ] WebGL SP match end-to-end.
- [ ] itch deploy uploaded; CC-BY 3.0 credit on the page; user flipped visibility Public.

## 4. Parking lot (don't lose, don't block on)

- **Sound dropout bug:** if it reproduces, `git revert 7791fc48` re-arms the full
  diagnostic kit (panel `shots=/sfxCalls=` line + 3-way call-site warning names the
  culprit). Everything known is in AGENT_CONTEXT 2026-07-11 entries.
- Editor freeze recurrence → `sample <pid>` first, then consider newer 6000.4.x patch.
- Stale ParrelSync clones 0–5 are deletable; `clone_6` is current.
