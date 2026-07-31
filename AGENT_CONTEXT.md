# AGENT_CONTEXT — Super Inverters Reloaded (Multiplayer)

> **Purpose:** This file is a context handoff between agents working on this repo.
> Image/output limits in agent sessions can wipe working memory. Read this on every
> fresh session and append a log entry before you stop. **Don't lose the load-bearing
> facts** (current state, non-obvious decisions, the WebGL dead-ends) — but per the
> 2026-05-22 compression you may fold older per-session entries into a summary rather
> than keeping every one verbatim.

---

## STOP — read this first

- **Repo path:** `/Users/nadav/Documents/GitHub/super-inverters-game/`
- **The Claude default cwd `/Users/nadav/Documents/Claude/Super Inverters Reloaded/` is EMPTY.** The actual code is in the path above. `cd` there before doing anything.
- **Active branch:** `Multiplayer` (tracked to `origin/Multiplayer`). **Slice 5 feature-complete in Editor** — extensive 2-peer playtest PASS 2026-06-19 (see update log).
- **Engine:** Unity **`6000.4.4f1`** (required — see `ProjectSettings/ProjectVersion.txt`). WebGL build work upgraded from `6000.3.16f1` 2026-06-19. **Do not open this project with `6000.3.16f1`** — Editor logs show broken WebGL/OSX modules and Play mode fails. Open from Unity Hub as `super-inverters-game` and pick **6000.4.4f1**.
- **Project is registered in Unity Hub** under the name `super-inverters-game`. The user opens the project from there. Don't `Add project` again.
- **Latest published web build:** https://nmeidan.itch.io/superinverters — the live WebGL build, "the latest web version."
- **Build target for multiplayer: WebGL.** Confirmed by user 2026-04-30. Plan all networking choices around WebGL constraints (no raw UDP; use WebSocket transport or WebRTC).
- **No lobby UI.** Confirmed by user 2026-04-30. The flow is link-share only; do not build a server browser or room list.
- **Networking stack: Photon PUN 2.** Confirmed 2026-04-30 from a screenshot of the user's Photon dashboard. The app on the dashboard is named **"Super Inverters"**, type **PUN** (= PUN 2; "PUN Classic" is long deprecated), free tier 20 CCU, status Public, **App ID prefix `159a8424-...`** (full value not stored here on purpose; see security note below).
- **Photon App ID is a credential.** This repo is hosted on public GitHub. Do **not** commit the full App ID in any file (including `PhotonServerSettings.asset`, which the PUN setup wizard creates). When the SDK is installed, add the asset path PUN generates to `.gitignore`, OR keep a stub asset committed and load the real ID from a `.env`-style untracked file. Decide before the first PUN-related commit.
- **Photon PUN 2 installed** under `Assets/Photon/` (~39 MB, tracked). `PhotonServerSettings.asset` is **gitignored** — paste App ID locally via PUN Setup Wizard.
- **Goal of this branch:** add **simple multiplayer** to a 2-player local game. See "Multiplayer goal" below.
- **`CLAUDE.md`** points here for multiplayer handoff. This file remains the detailed source of truth.
- **Next ship milestone:** WebGL two-peer browser test on Unity 6.3 (was blocked on 2020.3 — see WebGL section; retest before assuming it still fails).

---

## Project snapshot

**Game:** "Super Inverters" — a 2-player local versus platformer originally built for a game jam (`GaliGuess/Algamedes_Jam2`). Two players, **Black** and **White**, move on platforms keyed to their own color/framework, shoot each other, and try to be the last alive. Scoring/lives/end-game menu already work locally with keyboard + xbox/PS4 controllers.

**Top-level layout:**
```
super-inverters-game/
├── Assets/
│   ├── Scenes/             level_1..5, level_menu, main_menu, start_scene*, level_test
│   ├── Prefabs/            Player.prefab, Game.prefab, Shot/Shell, platforms, menus
│   ├── scripts/
│   │   ├── Game/           GameManager, GameState, GameView, LivesVisualizer
│   │   ├── Player/         PlayerManager, PlayerState, PlayerView, PlayerSFX, ShootCooldownHud, etc.
│   │   ├── Controllers/    Controller (base), KeyboardController, PS4Controller, LevelMenuController
│   │   ├── Shell/, Shot/, Platform/, Movement/, Utils/, Editor/
│   │   └── (top-level)     SceneLoader, ScoreKeeper, Values, etc.
│   ├── Resources/, Materials/, Graphics/, Audio/, Animations/, Plugins/
├── Packages/manifest.json   (Unity package list — see below)
├── ProjectSettings/, Library/, Temp/, UserSettings/, Logs/
├── Web build/, super_invereters_web/   (existing WebGL build outputs)
└── .git/  (origin: GaliGuess/Algamedes_Jam2 — confirm with `git remote -v`)
```

**Networking packages currently installed:** **Photon PUN 2** (`Assets/Photon/`). Also ParrelSync (`Packages/com.veriorpies.parrelsync/`) for two-editor testing.

**Branches on origin:** `master`, `Multiplayer` (this one), `backup_branch`, `final_presentationDay`, `moshe_build`, `mouse-controller`, `moving_platforms`, `shell_backup*`, `wired-pause-menu`, `xbox_controller*`, etc.

---

## Multiplayer goal (user's vision — confirmed 2026-04-30)

**User flow:**
1. Host opens the WebGL build, picks "Multiplayer," and **chooses their character (Black or White)**.
2. Host clicks "Generate link" → gets a shareable URL (the link encodes the session ID and the host's color choice, so the joiner knows which side is free).
3. Host manually sends the link to a friend (Discord, Messages, whatever — out of scope for the build).
4. Friend opens the link in their browser → joins the same session and **automatically occupies the opposite color** (no character-pick screen for the joiner).
5. Both players' inputs drive the live game; gameplay is otherwise identical to local 2-player.

**Explicit non-goals (per user):**
- No lobby / room browser / matchmaking UI.
- No reconnect flow, no spectator mode, no >2 players.
- No anti-cheat / authoritative-server hardening — friends only.

**Decisions locked in 2026-04-30:**
- Secret handling: **gitignore** `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset` (and `.meta`). Each dev pastes their own App ID locally via the PUN Setup Wizard. See `.gitignore` lines ~67–68.
- Photon tracking scope: **track everything under `Assets/Photon/`** (39 MB) as a single checkpoint commit. Demos and PhotonChat included for now; can be slimmed later if repo size becomes an issue. The PhotonServerSettings asset is the only excluded path.
- Hosting model: **host-authoritative**, Photon Cloud handles relay. The user's PUN app is on the **EU dev region** (set in PhotonServerSettings as the default dev region) — fine for testing; can be changed to "best region" before shipping if the player base is non-EU.

## Planned multiplayer integration (vertical slices)

Build one slice at a time and verify before moving on. Don't write all of this in one go.

**Slice 1 — Two peers in one room.** Add a tiny `MultiplayerBootstrap` MonoBehaviour that on Start: reads a `?room=XYZ` URL parameter (via `Application.absoluteURL`); if present, joins room `XYZ`; if absent, creates a new room with a random ID and logs the share URL to the console. Verify by running two Editor instances (or one Editor + one Build) and watching them recognize each other (`OnPlayerEnteredRoom` fires). No gameplay yet.

**Slice 2 — Color assignment.** Add a `hostColor` custom room property set by the room creator. The joiner reads it on `OnJoinedRoom` and takes the opposite. Each peer instantiates a single Player from `Assets/Prefabs/Player.prefab` via `PhotonNetwork.Instantiate`, with the right `Framework` set on its `PlayerState`. Verify both Players appear on both screens, with the right colors.

**Slice 3 — Networked input.** Add `NetworkController : Controller` (in `Assets/scripts/Controllers/`). On the local Player, the existing `KeyboardController` / `PS4Controller` keep working and their inputs are sent over the network via a `PhotonView` + `IPunObservable` on the Player. On the remote Player, `NetworkController` returns the replicated values via the existing `Controller` interface. Use the `Controller` polymorphism — do NOT modify `PlayerManager`. Verify: two browser tabs, both players move on both screens.

**Slice 4 — Networked spawn/kill events.** `GameManager.SpawnShot`/`SpawnShell` need to use `PhotonNetwork.Instantiate` (or a manual RPC) so projectiles appear on both peers. `GameManager.PlayerKilled` needs to be an RPC so the score and round-reload happen on both clients. `SceneManager.LoadScene` calls become `PhotonNetwork.LoadLevel` (PUN's networked equivalent that auto-syncs the joiner).

**Slice 5 — Multiplayer menu + link UX.** New `multiplayer_menu` scene with two buttons: "Host as Black" / "Host as White" (and "Cancel"). On click → bootstrap creates a room, sets `hostColor`, displays the shareable URL in copyable text. The joiner just opens the URL and lands directly in the game scene as the opposite color (no menu).

**Out of scope (per user 2026-04-30):** lobby UI, room browser, reconnect, >2 players, anti-cheat.

---

## Architecture seams that matter for multiplayer

The codebase has a clean input abstraction that makes networked play tractable. **Use these seams; don't rewrite the gameplay loop.**

- **`Assets/scripts/Controllers/Controller.cs`** — abstract base. Each `PlayerManager` does `GetComponents<Controller>()` and polls them every Update/FixedUpdate (`moving_direction()`, `aim_direction()`, `jump()`, `shoot()`, `getDown()`, `pauseMenu()`). `KeyboardController` and `PS4Controller` are the two concrete implementations today.
  - **Multiplayer seam:** add a `NetworkController : Controller` that, for the *remote* player, returns inputs received over the network instead of polling local hardware. Local player keeps existing controllers; remote player's prefab gets the `NetworkController` swapped in. This is a much smaller change than networking the rigidbody.

- **`Assets/scripts/Player/PlayerManager.cs`** — drives physics from controller inputs. The two players are distinguished by `PlayerState.player_framework` (`Framework.BLACK` / `Framework.WHITE`) which sets layers, sprite color, and platform compatibility. Physics runs locally per-client.

- **`Assets/scripts/Game/GameManager.cs`** — handles round/match flow: `PlayerKilled` → `decreaseScore` → reload scene or `endGame`. Spawns `Shot` and `Shell` via factories. **Networking these spawn calls and the kill event is the other thing the multiplayer layer must do** beyond input replication. Scene reloads on every round (`SceneManager.LoadScene(gameSceneName)`) — needs network-aware handling so both peers reload together.

- **`Assets/scripts/Player/PlayerState.cs`, `Game/GameState.cs`, `ScoreKeeper.cs`** — game-state holders. Score lives in a `DontDestroyOnLoad` `ScoreKeeper`; check whether to sync via network or recompute deterministically from kill events.

- **Scenes that matter:** `main_menu` → `start_scene` / `start_scene_2` → `level_1..5`. The link/lobby UI probably hooks into `main_menu` or a new scene before `start_scene`.

**Recommendation (for discussion, not yet implemented):** assuming WebGL target, the strongest "simple" options are:
- **Mirror + a WebSocket transport** (e.g., SimpleWebTransport) — mature, lots of tutorials, host-authoritative by default. Free.
- **Unity Netcode for GameObjects + Relay (UTP WebSocket mode)** — official, but Relay has free-tier limits and the WebGL story is newer.
- **Photon Fusion / PUN 2** — easiest "share a room code" UX out of the box; free CCU tier; closed-source, vendor lock-in.

Plumb a single host-authoritative session: host owns one color, joiner auto-assigned the other. Replicate inputs (cheap) + spawn events (Shot/Shell) + kill events. Don't bother with rollback or prediction for "simple" mode.

---

## Working agreement for agents

When you (a future agent) work on this repo:

1. **First action of every session:** read this file end-to-end.
2. **Acknowledge the context-limit warning convention** in your first message of the session. The user wants to hear: "I'll watch context and warn you ~3-5 messages before exhaustion." A prior session was wiped by limits mid-implementation; this convention exists so the user always has time to ask for a final AGENT_CONTEXT.md update before the cut-off. Don't wait for the last turn — surface the warning when you estimate 3-5 turns of usable context remain.
3. **Verify before trusting:** facts here can go stale. Before acting on a claim (file path, branch state, package presence), spot-check it. The prior agent claimed `Player.prefab` was the gameplay player; turned out to be a dead/unused template — actual prefabs are `BlackPlayer.prefab` and `WhitePlayer.prefab`. Always grep scenes for prefab GUID references before basing work on a doc claim.
4. **One Unity Editor at a time** when doing file moves via shell. ParrelSync clones share `Library/` via symlink; if both Editors are open during a `git mv`, Unity's LMDB asset database trips assertion failures (`MDB_MAP_RESIZED`, "Asset database transaction committed twice!"). Recovery is usually a clean Unity restart with only one Editor open; nuking `Library/` is the last resort.
5. **Tell the user when to save in Unity.** After changing any `.cs` under `Assets/scripts/`, say explicitly: focus Unity, **wait for the script compile spinner to finish**, check the Console for errors, then enter Play mode (⌘+P). If scripts changed during Play, stop Play, let it recompile, then test again.
6. **Confirm scope with the user before installing a networking package** or making sweeping changes — this branch is exploratory, not production.
7. **Keep edits minimal and on-branch.** Don't merge to master. Don't force-push.
8. **Last action of every session:** append an entry to "Update log" below (newest at the top of the log). Even if you accomplished nothing, log what you tried and what blocked you. Future-you needs the negative results too.
9. **If this file gets long (>300 lines):** compress older per-session entries into a single consolidated summary entry (as was done 2026-05-22), preserving the load-bearing facts — current state, non-obvious technical decisions, and the WebGL dead-ends. Keep recent entries verbatim; fold the rest.

### Update log entry format

```
### YYYY-MM-DD — <short title>
**Agent session goal:** what the user asked for this session
**What I did:** bullet list of concrete changes (with file paths)
**State left behind:** branch / dirty files / open PRs / running processes
**What's blocked or unclear:** open questions, things the user needs to decide
**Next agent should:** specific suggested next step
```

---

## Update log

<!-- Newest entries on top. Append ABOVE the consolidated 2026-05-22 entry. -->

### 2026-08-01 (later) — S3 KICKOFF: branch + recon + plan (retrofit decision made)

> Branch **`feature/mp-level-select`** off Multiplayer@75e71180 (PR #7 = S1/S2 merged;
> also: PR #6 was accidentally merged into MASTER — restored by force-push to a7f912b6,
> verified clean. Triple-check PR bases in this repo; GitHub defaults to master.)

**User decision: RETROFIT the five level_1..5 scenes to serve BOTH modes** (no duplicate
level_N-multiplayer scenes; level_1-multiplayer becomes legacy/template).

Recon — delta between level_1-multiplayer and level_1 (per-scene retrofit checklist):
- SP scenes LACK: `PhotonView` on Game (GameManager RPCs need it; scene viewIDs are
  consistent — both peers load the same file); `MultiplayerSpawner` (add next to the
  existing standalone PowerupSpawner object; per-scene `targetSceneName` gate + per-level
  blackSpawnPositions/whiteSpawnPositions — Scene-view drag handles exist).
- SP scenes HAVE scene-placed {Black,White}Player prefab INSTANCES (with PhotonViews!) —
  in a room these must be removed before MultiplayerSpawner instantiates networked ones.
- Already room-aware from S1/S2 (keep, no work): PowerupSpawner (CanSpawn),
  SpawnPlatformPainter (InRoom-gated), BotController (self-disables InRoom),
  ShootCooldownHud (IsMine binding).
- GameManager.gameSceneName empty in SP scenes → ReloadMatchScene falls back to active
  scene = generalizes to any level.

Plan (phases): **A.** Level select in MP — reuse level_menu: on room full, master
LoadLevel("level_menu") (AutomaticallySyncScene carries guest), guest gets a "host is
choosing" state with input disabled, SceneLoader gets an InRoom branch (LoadLevel), hide
BotDifficultyUI in-room. **B.** Retrofit editor pass over level_1..5 (PhotonView,
MultiplayerSpawner + spawn arrays, remove scene players when InRoom). **C.** G4
master-authoritative pickups + ClearAllPickups wiring (kickoff plan in the session-4
entry). **D.** Two-editor verification per level (paint-walk test is the desync detector;
clone_6). NOT STARTED beyond the branch — next agent begins with Phase A.

> **EOD STATUS:** branch `feature/single-player`, all pushed (`fb91f542` + docs). PR #5
> (paint-grenade) was merged into `Multiplayer` with a merge commit at the start of this
> session and the branch synced on top. User verified ALL 5 levels end-to-end.
> **NEXT: PR `feature/single-player` → `Multiplayer`, merge, then S3 on a new branch
> `feature/mp-level-select` off updated Multiplayer** (level select in MP + all levels
> MP-enabled + G4 folded in — see NEXT_PHASE_SPEC §2 S3).

**Load-bearing discovery that shrank both slices:** ALL five SP level scenes instance the
same `Assets/Resources/{Black,White}Player.prefab` — so the bot, grenade components, and
tuning live on the prefab and exist everywhere at once. The 07-12 note "bot only on
level_1" was stale.

**Shipped this session (each with its own commit):**
- Life-icon HUD fix: last life froze un-animated at game over (`timeScale=0` + scaled-time
  animator) → unscaled, like the end-menu word art. The "game over one life early" bug was
  purely this display artifact; score truth was never wrong.
- Main menu: SP button label was white-on-white (visible only while selected) → dark
  labels; grey resting buttons, darker grey on selection (user preference).
- Sound: "bass boosted" end music root-caused to `PlatformSFX.prefab` ChangeColor
  AudioSource with **serialized Pitch=0** — a pitch-0 voice never advances/ends, each
  platform invert parked a stuck DC voice on the mixer. Pitch 1 fixed it; the invert SFX
  is audible for the first time (vol 0.4). Likely related to the old "20 playing sources"
  reading. `muteMusicForTesting` now also mutes end-menu music; it was ON in level_1..5
  during testing and was REVERTED (all levels unmuted) before the S1/S2 PR.
- Bot difficulty: player-facing "BOT DIFFICULTY ◀ n/10 ▶" stepper on level_menu
  (BotDifficultyUI builds it at runtime; BotDifficulty = PlayerPrefs, default 4);
  editor-only OnGUI debug window + [/] keys REMOVED.
- S2: PowerupSpawner in all 5 SP scenes (autoFitArenaWidth derives spawn range from
  platform bounds), CanSpawn no longer requires the MP-only PlatformMotionEpoch offline;
  ShootCooldownHud added to SP scenes' Game object; HUD binds to the human (offline every
  PhotonView is IsMine — must skip bot-driven players).
- SpawnPlatformPainter (SP only): first two platforms under each spawn painted the
  player's colour one frame after load.
- Editor tool `SpawnPreviewGizmos`: spawn marker + projected fall line per player.
  **CRITICAL: movers TELEPORT to points[0] at round start** (edit-time positions lie);
  the gizmo projects against round-start footprints and draws them. Also:
  `isMovingPlatform` is runtime-computed (always false in edit mode) — detect movers by
  `points.arraySize > 1`; `PlayerManager.isGrounded` is serialized TRUE on the prefabs.
- **Bot navigation discipline** (each rule earned by a traced death; frame-tracing via an
  `EditorApplication.update` sampler → Logs/bot_trace*.txt was the ONLY way — theory
  patches failed 4x): (1) no steering until first real landing after spawn; (2) airborne
  chase-steering forbidden outside a 1.2s window opened by a deliberate jump; (3) leap and
  side-climb targets must be STATIC standable ground; (4) walking steps require static
  ground ahead (movers are boarded only by landing on them); (5) riding a mover = hold its
  centre, stay locked through contact flickers geometrically (descending shuttles outrun
  gravity for seconds — timers can't cover it); (6) falling rescue steers to the NEAREST
  footing, never toward the opponent; (7) marooned watchdog: keyed on net displacement,
  after 1s escapes preferring DROP-THROUGH (getDown; level_2's spawn islands sit directly
  above their big platform — the designed way off), else a jump-dive at static ground
  below. Result: bot survives, hunts, and can WIN on every level incl. level_3 (100%
  movers) and level_2 (spawn islands).
- **Bot grenades:** 45° lob (v = R·√(g/(R−dy)), tier aim wobble, clamped 8–36) at a
  grounded opponent 8–55 away, cooldown 6–12s (initial 2–5s — rounds reload the scene and
  a full cooldown outlived the round). Bot also contests pickups again.
- Safe spawns: level_2 static spawns; level_3 spawns above the vertical shuttles'
  round-start tops.

**Testing lesson (cost a round):** an idle-opponent survival metric cannot distinguish a
comatose bot from a careful one — the wake-probe regression "passed" 5 levels while the
bot stood paralysed; the user's playtest caught it. Activity (kills, displacement,
awake/firing flags via reflection) is the metric.

**Open (parked):** bot mid-air detonate (meanness upgrade), gamepad nav on menus untested,
SP pause-freezes-grenades not explicitly re-tested, "no PUN warnings offline" console
sweep, plus the standing parking lot (sound dropout revert pointer, G4, ClearAllPickups).

### 2026-07-11 (handoff) — USER ROADMAP for the next sessions — READ THIS FIRST

**Full technical plan + test checklist: `NEXT_PHASE_SPEC.md` (repo root)** — the
slice-by-slice spec for everything below, same format as GRENADE_FEATURE_SPEC.md.

The grenade feature is wrapped and up as a PR: **`feature/paint-grenade` → `Multiplayer`**
(base already corrected from master; description written; user creates/merges it).
User-directed plan, in priority order:

1. **Merge the grenade PR into `Multiplayer`.** User merges on GitHub; a fresh session then
   does `git checkout Multiplayer && git pull` before anything else. (If the PR is already
   merged when you read this, just pull.)
2. **New branch off `Multiplayer`: bring the grenade mechanic to SINGLE-PLAYER.** There is
   currently NO working single-player — treat "make single-player work at all" as part of
   this step, then make grenades/pickups/HUD work there (the code already has non-networked
   paths: `DoPlayerKilled`, local pickup grant, `PhotonNetwork.InRoom` guards everywhere —
   audit those seams).
3. **Expand multiplayer to ALL levels.** Today the flow SKIPS the level-select screen and
   hardwires one arena (`level_1-multiplayer`). Stop skipping it: level select must appear
   and every level must be playable in multiplayer with all the new functionality (synced
   platforms, kills, pause/exit, grenades). Note: platform networkIds come from sorted
   hierarchy paths per scene, so each level needs the same treatment/verification;
   `PowerupSpawner` lives on the Bootstrap object in the MP scene — other levels need it too.
4. **UI refinement pass** — visuals, layout polish, and UI bugs across menus/HUD.
5. Then: review and **publish to itch.io** (deploy via `Scripts/deploy-itch.sh` + butler;
   user must toggle visibility manually; REMEMBER the CC-BY credit: explosion sound
   "Big Explosion" by Blender Foundation, OpenGameArt, CC-BY 3.0 — attribution required
   on the itch page).

Still-open items that should find a home inside that plan (not user-prioritized, don't lose
them): **G4 master-authoritative pickups** (pickups still spawn per-peer with independent
timers — the two unchecked §9 items depend on it; fits naturally under step 3's "all levels
fully multiplayer" hardening), the **runtime sound dropout** (cornered to the PlayShoot call
site — see the entry below; probes were STRIPPED in `7791fc48`, `git revert 7791fc48`
restores the whole diagnostic kit), and **`ClearAllPickups` end-game wiring**.

### 2026-07-12 — Slice S1: SP diagnosed + AI bot for White (in progress)
> **EOD STATUS:** committed to branch **`feature/single-player`** (branched off `feature/paint-grenade` = the post-PR#5 Multiplayer tree). **NOT pushed** — user runs `git push -u origin feature/single-player`. The bot files listed below are IN that commit; working tree clean. Reconcile with `Multiplayer` after PR #5 lands: if PR #5 is merge/rebase-merged the diff is clean; if squash-merged, `git rebase --onto Multiplayer feature/paint-grenade feature/single-player` to drop the duplicated grenade commits. NEXT SESSION: finish S1 (bot nav refine — real reachable-platform graph + scale bot to the other levels), then S2 (grenades in SP). **Revert `muteMusicForTesting` on level_1 before ship.**

> **⚠️ KNOWN BUG (found 2026-07-12 EOD by user playtest — NOT investigated, document-only):** In single-player `level_1` (vs the bot), the **game-over / end screen fired while the human (Black) still had a life left** — the match ended ~one life too early. The bot itself was behaving well (difficulty + shooting good); this looks like a latent SP game-over/lives-count bug now *exposed* because the bot finally racks up real kills (SP was previously too passive to reach a clean game-over). **Where to look (all unverified leads):** (1) `GameManager.DoPlayerKilled` — SP path does `decreaseScore` then `GameState.hasNoLives` (`score <= 0`, not `== 0`) → `endGame`; a double- or skip-decrement ends it early. (2) Double-decrement: `PlayerManager.OnTriggerExit2D`(boundary)→`PlayerKilled`→`DoPlayerKilled` is `roundEnded`-gated within a round, but check a death landing in the spawn/countdown window or across the reload boundary (fires twice?). (3) HUD vs. truth: compare `LivesVisualizer` display against the `ScoreKeeper` score and `GameState.startLives` (level_1's serialized value) — an off-by-one in the display vs. the `<=0` end test would look exactly like "I still had a life." (4) `ScoreKeeper` is `DontDestroyOnLoad` and persists across SP round reloads (`endGame` destroys it) — confirm no stale carry-over. **Repro:** SP level_1, let the bot kill Black repeatedly, watch the lives HUD vs. when the end screen appears. **Priority: fix early next S1 session — core-loop correctness, before more bot polish.**

**Agent session goal:** Start S1 (single-player). Diagnose why SP "doesn't work", then act.

**Diagnosis (via Unity MCP live inspection of `level_1`, not just code reading):** SP is NOT hard-broken. `level_1` loads and runs — the scene-placed `BlackPlayer`/`WhitePlayer` prefab instances spawn, are tagged/layered right, controllable, and the kill/countdown/end-game paths are intact (all Photon calls are `InRoom`-gated, skipped offline). Only non-fatal console errors (`InGamePauseMenu` builtin-sprite `UI/Skin/UISprite.psd` — S4). **What the user means by "SP doesn't work":** it's *incomplete*, not crashing — (a) it's a 2-player game so P2 should be an **AI bot**; (b) SP lacks feature parity (grenade, defined spawn positions, untested pause/game-over freeze); (c) flow: SP routes through `level_menu` (MP hardwires `level_1`). Plan: **do everything on `level_1` first** (user manually picks it), then scale to other levels; skipping level-select is future.

**User decisions this session:** White = **AI bot, build now**; **keep mouse-aim for Black** (intentional, not a bug); difficulty = **6 discrete tiers** with an editor-only debug toggle. Base `feature/single-player` off **Multiplayer only after PR #5 merges** (still OPEN); work meanwhile in the main checkout. Consulted the **Fable advisor** for the bot design (architecture + v1 spec + 8 code pitfalls) — its guidance is folded into `BotController` comments.

**What I did (ALL UNCOMMITTED, main checkout on `feature/paint-grenade` working tree — NOT on a branch yet):**
- **NEW `Assets/scripts/Controllers/BotController.cs`** — `BotController : Controller`, added to `Assets/Resources/WhitePlayer.prefab`. Self-activates in `Awake`: if `PhotonNetwork.InRoom` → disables itself (MP: White stays a remote human, untouched); else (SP) disables White's `KeyboardController`/`PS4Controller`/`GrenadeAimController` and owns the side. **Zero `PlayerManager` changes** (same seam as `NetworkController`). Priority-layered reactive AI (Survive>Fight>Move); **"aim at the opponent's feet"** = paint their platform = the kill (shots don't kill, only falls do); self-sustaining burst-fire cadence; **gap-crossing jump pursuit** (`jumpReach`); **always-moving patrol** (never parks — oscillates when in range) + **height-seeking climb** (jumps to reachable platforms above to gain a downward shot on the opponent's platform); **10 difficulty tiers** (`difficultyTier` 1–10, presets for reaction/aimError/burst/aggression) + **editor-only OnGUI debug window** (`[`/`]` keys + on-screen easy/hard buttons), all `#if UNITY_EDITOR`. Iterated per user playtest: fixed human's LMB firing the bot, added independent-fire + pursuit + patrol + climb + more tiers.
- **`Assets/scripts/Controllers/MouseAimController.cs`** — `shoot()` now `enabled && Input.GetMouseButton(0)`. BUG it fixes: `PlayerManager.FixedUpdate` polls `shoot()` on ALL controllers **without an enabled check**, and this getter read live LMB, so the human's click fired the bot (White's disabled MouseAimController). (Masked in MP by the `IsMine` gate; only bit in SP.)
- **`ProjectSettings/InputManager.asset`** — `B1_Jump` (Black's SP jump) remapped from `left ctrl`→`space` (alt `h` kept); user expected Space.
- **`Assets/Scenes/level_1.unity`** — set `GameManager.muteMusicForTesting = true` (dev only — **REVERT before ship**).

**Verified via MCP:** all compiles clean; bot activates in SP (disables White human controls), aims at opponent, **fires independently** (saw 6 shots with `Input.GetMouseButton(0)=False`). Gap-jump pursuit + tighter engage range + jump=Space are compiled but **awaiting the user's next playtest**. Editor was flaky about active-scene flips mid-session (user also using it); `set_active_instance super-inverters-game@646d1cbe` then drive via `execute_code` (open scene + inspect atomically to dodge races). `read_console`/`find_gameobjects by_component` are unreliable here — reflection via `execute_code` is the trustworthy probe. **GOTCHA (cost a round):** changing a serialized field's *code default* does NOT update values already serialized on `WhitePlayer.prefab`'s `BotController` — the component kept `difficultyTier=3, preferredRange=18` from first-add, so tuning looked like "no effect." Rewrite prefab values via `PrefabUtility.LoadPrefabContents` + `SerializedObject`. Current aggressive base written to prefab: `difficultyTier=7, preferredRange=10, rangeDeadband=3, engageRange=45, jumpReach=12`. **Bot lives ONLY on `level_1`'s White** — other SP levels have no bot yet, so testing another level shows nothing. User still wants it MORE active: constantly moving, climbing to the top platforms, shooting the platform under the player far more — patrol/climb logic exists but is geometry-limited on `level_1` (isolated platforms beyond `jumpReach`); needs real nav work + on-level playtesting next. **Diagnosed the "active round 1 then parks" report:** NOT a per-round bug — round 2 re-inits fine (valid opponent, botEnabled), but greedy movement gets edge-guard-vetoed toward the opponent and it FROZE (out of engage range too). **Fix applied (compiled, awaiting playtest):** `SafePatrolDir` never-freeze fallback (oscillate the current platform when advance is blocked) + explore-climb (jump to ANY reachable platform above, no longer gated on opponent-being-above). Still greedy (no real pathfinding); if it still parks or pit-suicides, that's the next nav step. **Next round:** user reported it stopped firing independently (only when shot at) + still won't seek higher platforms. Fixes: prefab `engageRange` 45→80 (was only reactive because the human usually sat >45u away) and climb rewritten to `HigherPlatformSide` (detects higher platforms straight-up OR diagonally and steers onto them, vs. old straight-up-only `HasCeilingWithin`, now unused/harmless). Compiled+verified; awaiting playtest. If seeking-higher still fails, it needs the reachable-platform-graph nav pass — greedy raycast climbing can't plan multi-jump routes.

**Blocked / unclear:** PR #5 (`feature/paint-grenade`→`Multiplayer`) **still OPEN**. Work is uncommitted on the main checkout (Unity/MCP only operate there); the spawned worktree `infallible-margulis-b49ff2` is unused.

**Next agent should:** (1) after PR #5 merges → create `feature/single-player` off `Multiplayer`, move the uncommitted files onto it, commit (USER pushes); (2) iterate bot feel from user playtest (pursuit/gap-jumps not over-leaping into pits; tier balance); (3) S2 on `level_1` — grenades in SP + spawn positions + pause/game-over freeze tests; (4) **revert `muteMusicForTesting`** before ship.

### 2026-07-11 (later) — Grenade feature WRAPPED; game-over-exit freeze fixed (native-deadlock workaround); §9 complete except G4 items; sound bug cornered

**User declared the grenade feature DONE this session** (G4 master-authoritative pickups is
still the open networking slice — pickups spawn per-peer; see the G4 kickoff plan below).

**Game-over-exit editor freeze — root-caused & WORKED AROUND (user-verified, many clean exits):**
- Symptom returned: "Back to Main Menu" from the game-over screen deterministically hard-froze
  the CLICKING editor (other peer exited fine). Blocked at ~0% CPU = deadlock, not livelock.
- This is the **documented Unity 6000.4-macOS native engine deadlock** (see 2026-05-23 entry:
  main thread in `SpriteRenderer::MainThreadCleanup` on a mutex orphaned by a "prematurely
  finalized" thread, downstream of synchronous `SceneManager.LoadScene`). It came back with
  the 2026-06-19 return to 6.4.4 (6.3.16 had broken WebGL modules). Today's logs showed the
  known precursor warning again.
- **Workaround that holds:** `GameManager.LoadMainMenuScene` now defers the load 2 frames and
  uses `LoadSceneAsync` from a `DontDestroyOnLoad` runner (`DeferredSceneLoader`, nested in
  GameManager). Changes the thread/timing pattern the engine bug races on. If the freeze ever
  recurs: `sample <pid>` BEFORE force-quitting; fallback plan = newer 6000.4.x editor patch.
- Also fixed while chasing it (real managed races found in the frozen session's logs):
  - **Receive-side exit gates:** `RPCReportKillToMaster` / `RPCApplyKillResult` /
    `RPCRespawnWithoutScore` now early-return on `s_pendingExitToMainMenu` — a kill verdict
    arriving mid-teardown used to run `endGame()`/`ForceRespawn()` (PhotonNetwork.Instantiate!)
    against an in-flight LeaveRoom. (Send side was gated last session; receive side was the hole.)
  - **PlatformManager exception storm on exit:** platforms that ran the synced in-room path
    fell back to LOCAL motion with stale `reverse_dir` when InRoom flipped false at teardown →
    `ArgumentOutOfRangeException` every FixedUpdate. New `_usedSyncedMotion` flag freezes them
    in place instead ("once synced, stay synced-or-still").

**Game-over now freezes the world like pause (user request):** `endGame()` sets
`Time.timeScale = 0` at the verdict (grenade hangs mid-air, can never paint after game over —
§9 item). Menu reveal uses `WaitForSecondsRealtime`; end-menu title Animator runs
`UnscaledTime`; BOTH replay paths call `RestoreGameplayTimeScale()` and `GameManager.Awake`
resets timeScale to 1 (timeScale survives scene loads). PUN dispatch at timeScale 0 already
handled (`MinimalTimeScaleToDispatchInFixedUpdate = 1` in Awake).

**Grenade economy (user request):** start with 3, cap 3, pickup +2 clamped (1→3, 2→3, full →
pickup ignored & keeps falling). Serialized fields `startingGrenades`/`maxGrenades`/
`grenadesPerPickup` in `GrenadeInventory`; prefabs untouched (C# defaults). HUD scales itself.

**§9 status:** game-over-mid-air ✓, rematch ✓, WebGL smoke build ✓ (headless
`Scripts/build-webgl.sh` → Success, ~100 MB, debug kit compiled out). Remaining: the two
G4-dependent items (falls-identically, grab-race).

**Runtime sound bug — not fixed but CORNERED (evidence chain):**
1. At dropout: 0 virtual voices, listener unpaused, boom sometimes unaffected → NOT the mixer.
2. **Replay fixes it** → broken state lives in scene objects, NOT editor/OS audio.
3. Local Shoot source read `vol=1.00` during dropout (PlayShoot stamps 0.22 every call) and
   ZERO `[AudioDebug]` warnings logged → **`PlayShoot` is never being CALLED during dropout**,
   while bullets still fire. The whole bug is now pinned to `PlayerManager.cs` ~line 503:
   `if (EnableSFX) _sfx.PlayShoot();`.
4. Instrumentation in place (all `#if UNITY_EDITOR`): debug panel Audio section shows
   `shots=` (PlayerManager.DebugShotAttempts) vs `sfxCalls=` (PlayerSFX.DebugShootCalls) +
   last-call frame + instance-id MATCH/STALE + EnableSFX + player/mine counts + voice census;
   call site logs a 3-way `[AudioDebug]` warning naming the culprit (EnableSFX false /
   `_sfx` never assigned / `_sfx` DESTROYED). **Next repro: read the panel bottom line +
   console warning — that's the verdict.** Clue: user says only the MAIN editor ever gets it
   (editors aren't symmetric: master role; respawns run on the victim's editor).

**Housekeeping:** clone_5 → **clone_6** (fresh ParrelSync copy after a freeze force-quit).
Debug kit (panel/probes/H-key) is KEPT for the sound hunt — all editor-only, compiled out of
builds; strip whenever wanted. `Web build/Build/*` payloads are gitignored; only small
template files are tracked.

**Next agent should:** (1) G4 per the session-4 kickoff plan below (+ wire `ClearAllPickups`
into end-game); (2) re-run the two G4-dependent §9 items; (3) close the sound bug via the
panel verdict on next repro.

### 2026-07-11 — Pause-exit crash FIXED & verified; §9 test plan mostly done; sound bug still open

**Agent session goal:** Re-orient after a day away; verify the pause-exit fix from the
2026-07-10 fork session; update docs and commit the pause batch before the user forks for G4.

**Pause-exit crash — FIXED (user-verified 2-peer PASS):**
- Symptom: grenade mid-air → pause → "Back to Main Menu" hard-froze the clone editor
  (force-quit; clone_4 got corrupted → user made fresh `clone_5` via ParrelSync).
- Root cause (fixed in `GameManager.PlayerKilled`): a local exit tears the avatar down via
  PhotonNetwork.Destroy; the collider deactivation fires the bounds OnTriggerExit2D → phantom
  death mid-cleanup → kill RPCs + score decrement + respawn coroutine racing LeaveRoom.
  Guarded by `s_pendingExitToMainMenu` (set in both exit paths before teardown, reset after).
- NOTE: this repo ALSO has a documented NATIVE 6.4-macOS deadlock on the same exit path
  (2026-05-23 entry). This crash was the C# race (code fix resolved it); if a back-to-menu
  freeze recurs, suspect the native one — capture `sample <pid>` before force-quitting.
- Verified the fix was COMPILED into BOTH editors before the re-test (reflection probe for
  the new field via MCP; remember the deferred-recompile gotcha — verify per editor).

**Pause menu polish (same uncommitted batch, now committed):** translucent grey pause overlay
(60% alpha — frozen game stays visible); all three pause buttons uniform placeholder look;
"Exit to" → "Back to Main Menu"; EndMenuManager no longer hijacks the pause menu's runtime
buttons into game-over word art; UI png metas got WebGL platform overrides + spriteMode fixes.

**§9 test-plan status:** radius ✓, bounce decay ✓, fuse-in-pause ✓ (incl. exit-during-pause).
UNCHECKED the two G4-dependent items (falls-identically, grab-race) that had been marked [x] —
pickups still spawn per-peer, so they cannot pass until G4. Remaining: game-over with grenade
mid-air, rematch/registry reset, WebGL smoke build.

**KNOWN OPEN BUG — runtime sound dropout (recurring, cause still unproven):** sounds stop
during play; this time BOTH the detonation boom AND shot SFX died together. Facts so far:
audio state provably never corrupts (live probe 2026-07-10: 5 simultaneous booms + collect
leave the Shoot source enabled/playing/volume-correct); whole chain (EnableSFX, wiring, clip
GUIDs) intact. Multiple sounds dying TOGETHER points at listener/editor/system level, not
per-source. Prime suspects: (a) two-editor audio focus (Unity mutes the unfocused instance's
game audio depending on prefs), (b) macOS audio device switching. Next diagnostic: when it
happens, note which editor was focused + whether jump/music also died, and check the Game
view Mute Audio toggle before anything else.

**State left behind:** all of the above committed on `feature/paint-grenade` (user pushes).
`clone_5` is the live ParrelSync clone (Assets/ProjectSettings symlinked — code always in
sync, own Library, compile per editor); clones 0–4 are stale, multi-GB, deletable via the
ParrelSync Clones Manager. Debug test kit is RESTORED (1f20fe23) and should be stripped
again at ship.

**Next agent should:** (1) **G4** per the session-4 kickoff below (master-authoritative
pickups + fold in ClearAllPickups end-game wiring); (2) then the remaining §9 items incl.
the two G4-dependent re-runs and the WebGL smoke build; (3) chase the sound dropout with
the discriminating observations above if it recurs.

### 2026-07-10 (session 4) — Converge FX, HUD polish, lobby feedback, top-up pickups

**Shipped (one commit after session 3's):** DBZ converge orbs on collect (ring of white glow
orbs flies inward during the whiten; unparented PS — pickup's ×6 scale would blow up the ring);
collapsible debug panel (▶/▼, state static across round reloads); pickup fallSpeed baked 4.94;
boom volume 0.35→0.22 (long reverb tail was MASKING the quiet 0.22 shots — investigated live:
5 simultaneous booms + collect leave the Shoot source healthy, so "shot SFX broke" was masking,
not state corruption; if reported again check (a) shots still fire visually (b) jump SFX works);
grenade HUD: White's icons right-aligned to mirror its lives row, opaque white plate behind
icons; lobby "Create room" locks + animated "Creating..." until Photon answers (reset on
joined/left/failed); pickups now TOP UP to max (collect at any count below 2; ignored at full —
no accumulation). GOTCHA: a duplicate converge field block appeared in GrenadePickup.cs from a
PARALLEL edit (user-side IDE/AI?) causing CS0102 — if fields duplicate mysteriously, check for
that channel before blaming yourself.

**Slice score after this session:** G1–G3 ✓, G5 ✓ (2-peer playtested), G6 ~95% ✓ (absorbed
piecemeal: sprite, HUD, FX/SFX, debug strip). **G4 is the ONLY remaining slice.**

**Next agent should (G4 kickoff):**
1. **G4 per spec §3.3/§8** — master-authoritative pickup spawn + claim. Currently each peer
   runs `PowerupSpawner`'s local timer independently → peers see DIFFERENT drops. Master picks
   (pickupId, spawnX, PhotonNetwork.Time) → `RPCSpawnPowerup` (All) → every peer calls the
   existing `SpawnPickup(pickupId, spawnX, spawnTime)` (fall is already analytic off
   PhotonNetwork.Time — deterministic across peers). Claim: `RPCClaimPowerup` (MasterClient)
   → `RPCResolvePowerup` (All); copy the kill-flow RPC shape in GameManager. Only the master's
   scheduler runs (`PhotonNetwork.IsMasterClient` gate in PowerupSpawner.Update).
2. Fold into G4's first commit: wire `PowerupSpawner.ClearAllPickups()` into the end-game path
   (built in G3, never wired — pickups linger on the end screen).
3. Re-add a debug H-key drop (editor-only) if needed for testing — the full tooling was
   stripped in 129684c2; resurrect from that commit's parent.
4. At next itch publish: CC-BY credit — Explosion sound: "Big Explosion" by Blender Foundation
   (opengameart.org/content/big-explosion), CC-BY 3.0.

### 2026-07-10 (session 3) — Pickup collect flash + focused debug panel

**Shipped (commit after the Grenade Juice one):**
- **Collect animation:** on pickup the grenade freezes in place, gradually turns SOLID WHITE
  (anime power-up) and vanishes — 0.6s baked (`collectAnimSeconds`). The whiten uses a
  child SpriteRenderer with the built-in `GUI/Text Shader` (renders sprite alpha as flat
  colour — the only way to push a dark sprite TO white; tints only darken). NOTE for the
  WebGL build: if that shader gets stripped, add it to Always Included Shaders (there's a
  graceful fade-only fallback if Shader.Find returns null).
- **Debug panel slimmed to the pickup-test kit** (all `#if UNITY_EDITOR`): H-drop, held
  count, always-have toggle, pickup fall speed (1.9 baked), pickup hitbox radius slider
  (live on falling pickups; 0.25 prefab value confirmed by playtest), collect whiten
  seconds. Removed the fuse/blink/trail sliders AND their static overrides — those values
  are final in Grenade.prefab (fuse 2.31, blink 1.5/s, trail 0.45s@40/s).
- **Shot-SFX "regression" investigated — NOT a bug:** full chain audited (EnableSFX=1 both
  prefabs, AudioSources wired, PLAYER-SHOOT.wav guid intact since origin/Multiplayer).
  Cause was environmental — two open editors (ParrelSync clone) mess with editor audio
  focus; user confirmed sound works. Remember: remote/ghost shots are silent BY DESIGN.

### 2026-07-10 (session 2) — "Grenade Juice": sprite, spin, mid-air detonate, LED fuse light, trail, 2-pack pickups, HUD

**Agent session goal:** Real grenade sprite + a juice pass on the whole mechanic, driven by
live playtest iterations with the user.

**Shipped (single commit `Grenade Juice`):**
- **Sprite:** user-supplied grenade art imported as `Assets/Graphics/Sprites/grenade.png`
  (35×43, PPU 100, point filter; meta GUID authored by agent: `5d7cbea2dc284b4fa10088b7205e59e1`).
  Wired into `Grenade.prefab` (magenta Knob placeholder gone) AND `GrenadePickup.prefab`.
- **Physics juice:** random tumble on throw (`spinDegPerSecMin/Max` 180–540°/s opposite the
  throw direction, ±25° initial tilt); spin decays with `bounceVelocityRetention` on bounces.
- **Mid-air detonate:** pressing G with a grenade airborne detonates it (`GrenadeThrower.
  DetonateAirborne` + `HasAirborne`). While airborne you CANNOT charge a new throw (CanAim
  gate) and the detonating press is swallowed until key-up. Ghost follows via the usual RPC.
- **Fuse LED:** small white blinking dot (~8 screen px, `fuseLightSize` 0.85, offset near cap,
  sorting order 12, code-generated soft-circle sprite) on the armed grenade; blink 1.5/s.
- **Trail:** world-space ParticleSystem on the grenade — square particles, zero gravity/speed,
  thrower-coloured (near-black for Black / near-white for White — MID-GREY IS INVISIBLE against
  the grey city background), 0.45s decay, 40/s; detaches on death to finish fading.
- **Pickups grant 2:** `GrenadeInventory` is count-based (`grenadesPerPickup` 2); can't collect
  while still holding. Pickup visual = grenade sprite with a brightness pulse (tint breathes
  0.55→1; sprite tints can only darken, so "brighter" = up from a dimmed baseline).
- **HUD:** `ShootCooldownHud` now draws one grenade icon per held grenade under the lives row
  (44px invisible-box spacing); the burst-ammo bar is hidden via new `showBurstBar=false`
  (layout still anchors the icons).
- **Debug tooling re-added, ALL `#if UNITY_EDITOR`** (compiled out of builds — this was the
  user's requirement): H-key pickup drop, always-have-grenade toggle (warns it makes pickups
  no-op), sliders for fuse (2.31 baked), blink (1.5 baked), trail decay/amount (0.45/40 baked),
  and a "fuse light always ON" visibility-test toggle.

**HARD-WON GOTCHAS (do not relearn):**
- **The user's Editor defers recompiles until it regains focus.** MCP `refresh_unity`,
  `RequestScriptCompilation`, even Play-mode entry did NOT swap the assembly while unfocused —
  several "bug reports" were stale-assembly tests. Freshness marker trick: point the user at a
  UI element that only exists in the new code.
- **Two Unity instances = MCP routes to "most recent"** (was the ParrelSync clone!). Pin with
  `set_active_instance` (main = `super-inverters-game@646d1cbe`); instance list is visible in
  `~/Library/Application Support/UnityMCP/Logs/unity_mcp_server.log`.
- **The in-play arena background is a full-greyscale cityscape** (edit-mode renders show black
  — background art builds at runtime). Any single grey tone vanishes against it somewhere; the
  camera shows ~70 world units, so anything under ~0.5 world units is sub-8px on screen.
- Render-to-RenderTexture via `execute_code` works headless for visual verification (screenshots
  in scratchpad); `ps.Simulate()` to pre-warm particles; clean up strays (failed exec attempts
  leave instantiated objects in the open scene).

**Next agent should:** (1) **G4** — master-authoritative pickup spawn + claim (pickups still
per-peer, peers see different drops; `PowerupSpawner.SpawnPickup` seam ready); (2) G6 — real
pickup/explosion polish, `ClearAllPickups` end-game wiring, strip debug tooling again, CC-BY
attribution line (explosion sound) on the itch page; (3) user pushes the branch.

### 2026-07-10 — G5 COMPLETE & 2-peer playtested (ghost + explosion FX/SFX); tuning baked

**Agent session goal:** Act on the user's live-testing feedback: tuning tweaks + sliders,
always-have-grenade debug, explosion visual + sound, and the G5 ghost (remote peer now sees
the grenade fly and explode). **User confirmed 2-peer PASS on the ghost + explosion.**

**Shipped (committed this session as two commits — `Slice G5: grenade ghost + synced detonation
paint` + `chore: bake playtested grenade tuning; strip debug tooling`; the whole branch is still
UNPUSHED — user pushes):**
- **G5 ghost:** `GrenadeThrower.cs` RPCs `RPCSpawnGhostGrenade` (Others) on throw and
  `RPCDetonateGhostGrenade` (Others) on detonation. **DEVIATION from spec §3.8:** both RPCs
  live on the PLAYER's PhotonView, not GameManager — the remote copy of the thrower already
  has the grenadePrefab reference + framework, so zero scene wiring; per-view RPC ordering
  guarantees spawn-before-detonate. Ghost = same prefab via `GrenadeProjectile.InitGhost`:
  simulates physics locally, NEVER paints, snaps to the authoritative pos/radius on the
  detonate RPC; +0.75s grace fuse as fallback; if the ghost is already gone the RPC handler
  spawns the ring directly at the authoritative spot.
- **Explosion FX:** new `GrenadeExplosionRing.cs` — ring expands 0.5→paint radius over 0.35s
  (ease-out, fades, paint-colour), spawned by both real and ghost detonations; also plays the
  boom SFX (2D one-shot, `BoomVolume` const 0.35, throwaway GO — PlayClipAtPoint would be
  3D-attenuated).
- **SFX asset:** `Assets/Resources/Audio/grenade_explosion.wav` ← "Big Explosion"
  (DeathFlash.flac, converted via afconvert) by **Blender Foundation, OpenGameArt,
  CC-BY 3.0 — ATTRIBUTION REQUIRED on the published game page** (user's pick, replacing the
  CC0 NenadSimic one). Loaded lazily via `Resources.Load("Audio/grenade_explosion")`;
  missing clip = one warning, silent, no error.
- **Debug tooling: used for tuning, then STRIPPED entirely (user call — G6's debug-strip done
  early):** deleted `GrenadeDebugSpawner.cs` (H-key pickup drop + slider overlay), removed
  `PowerupSpawner.DebugSpawnNow` + `FallSpeed` accessor, `GrenadeAimController` tuning
  accessors, `GrenadeProjectile` static fuse/radius overrides, and the (never-committed)
  `GrenadeInventory.debugAlwaysHaveGrenade`. Grenades now come ONLY from falling pickups.
  The screenshot-the-sliders → bake workflow worked well; rebuild it from this entry's git
  history if another tuning pass is ever needed.
- **Tuning BAKED from the user's panel screenshot:** fallSpeed **1.9** (scene Bootstrap),
  throwForceMax **53.8** + chargeRamp 1.2 (both player prefabs), fuseSeconds **1.6**
  (user revised down from the screenshot's 2.59) + explosionRadius **14.1** (Grenade.prefab).

**Next agent should:** (1) **G4** per spec §3.3/§8 — pickups still spawn per-peer independently,
so peers see DIFFERENT falling grenades (the `PowerupSpawner.SpawnPickup` seam is ready);
(2) **G6 polish** — HUD, real grenade/pickup sprites + sizes, lifecycle gating
(`ClearAllPickups` wiring into end-game), and the CC-BY attribution line on the itch.io page
(the debug strip half of G6 is already done); (3) remind the user to push the branch.

### 2026-07-04 (session 2) — Grenade: Slice G3 shipped + G5 paint-sync pulled forward

**Agent session goal:** Continue the paint-grenade feature from the G1/G2 handoff — implement
G3 (falling pickup + local collection). A grenade paint desync surfaced in the user's 2-peer
test mid-session; user chose to fix it immediately (out of slice order).

**Shipped (committed on `feature/paint-grenade`; UNPUSHED — user pushes both):**
- `ababa1ea` **G3** — `PowerupSpawner.cs` (on `Bootstrap`): local scheduler, random 15–25s
  interval, random X (-40..38), `spawnTopY=52`, cap 2, gated on
  `PlatformMotionEpoch>=0 && !CountdownActive`. Built around `SpawnPickup()` + a `_activePickups`
  registry as the **seam for G4's master-authoritative RPCs**; `DebugSpawnNow()` for the H key.
  `GrenadePickup.cs` (+ `Assets/Prefabs/GrenadePickup.prefab`): kinematic trigger, analytic
  descent `y = spawnTopY - (NetworkNow - spawnTime)*fallSpeed` (**already epoch-ready for G4**),
  `IsMine`+tag-gated collection grants one grenade, single-slot no-op while holding, despawn
  below `despawnY=-25`. New `powerup` tag + `powerup` **layer (slot 12)**; Physics2D matrix:
  powerup collides **ONLY** with `players_black`/`players_white` (trigger detection), all else
  off. Debug key **H repurposed**: was instant-grant, now **drops a real pickup from the sky**.
  Debug overlay: force sliders replaced by a **fall-speed slider** (`PowerupSpawner.FallSpeed`,
  0.5–15) + kept charge-ramp. Scene re-serialized to 6.4 format (m_RootOrder drop etc.) —
  benign churn, no data loss (verified).
- `4712495c` **G5 paint-sync (pulled forward)** — `GrenadeProjectile.Detonate()` now, after each
  local `ApplyPaintFromNetwork`, calls `GameManager.BroadcastPaintPlatform(pm.networkId, framework)`
  to Others (lazy `FindFirstObjectByType<GameManager>()`) — the SAME call shots use. Fixes the
  reported desync (grenade paint landed only on the thrower's peer). The remote **ghost** grenade
  (see it fly/explode) is still TODO — cosmetic; platform colours now sync regardless.

**Deviations / user calls (baked in):** pickup placeholder is **WHITE, scale ×6, heartbeat
pulse** (scale breathes ±20% @1.5/s) — user's spec for the B&W game. "Six times larger" read as
**×6 absolute** (was ×3) — user may want ×18; confirm. `fallSpeed` default still 1.5 (user tuning
live via the slider — bake their chosen value when they name it).

**Verified:** G3 mechanics in a single-editor harness (spawned a fake DYNAMIC player in the fall
path, since pressing Play boots to `main_menu` via a play-mode start-scene override, not the MP
level): collection grants exactly one, single-slot no-ops, fall + despawn, white/×6/pulse — zero
grenade/powerup errors. **NOT yet 2-peer tested:** the `4712495c` paint-sync fix — needs the
spec §9 test (throw on peer A → platforms flip on BOTH; walk both players onto a painted platform,
no fall-through on either editor).

**Process notes (this session):** `refresh_unity` drops the MCP stdio bridge during the compile —
reconnects on the next call, not an error. Pressing Play from `level_1-multiplayer` boots to
`main_menu` (start-scene override) so the MP level can't be exercised standalone via MCP — real
pickup/paint testing is host-solo/2-peer by the user. Exit Play before editing scripts (unchanged).

**What's blocked / to decide:** slice order is now scrambled — G5's paint half is done before G4.
Remaining: G4 (networked pickup spawn + master-authoritative claim), G5 ghost (RPCSpawnGhostGrenade
/ RPCDetonateGrenade), G6 (lifecycle gating + HUD + strip debug tooling + real sprite/size). Confirm
order with the user. Parked-and-separate: the pre-existing MP platform-color desync (player appears
on a wrong-coloured platform) — NOT the grenade issue.

**Next agent should:** (1) have the user 2-peer test `4712495c` (grenade paint sync) first; (2) then
either G4 or the G5 ghost per user preference; (3) keep the debug tooling until G6. Prefab/scene/matrix
are wired — new work is mostly scripts + (for G4/G5) new RPCs on `GameManager`.

### 2026-07-04 — Grenade power-up: Slices G1 + G2 shipped (projectile + hold-to-charge throw)

**Shipped (committed on `feature/paint-grenade`; user pushes):**
- `b1fcad1b` **G1** — `GrenadeProjectile` (dynamic physics, per-bounce velocity damping, fuse, bounds despawn, detonation radius paint via `PlatformManager.ApplyPaintFromNetwork` — local path only). New `grenade` tag + layer (slot 8) + Physics2D matrix row (collides Default/floor/walls + `platforms_black/grey/white` + `floor`; ignores players/shots/shells/itself). `Grenade Material.physicsMaterial2D` (bounciness 0.55). Placeholder `Grenade.prefab`.
- `7c8a723d` **chore** — pre-existing Unity MCP + ProBuilder/VFX Graph package churn, committed separately so slice diffs stay feature-only.
- `5c1f925b` **G2** — player-side throw: `GrenadeInventory` (capacity 1), `GrenadeThrower` (spawns from player, coloured by `player_framework`; local only — ghost RPC is G5), `GrenadeAimController` (hold-to-charge, force lerps min..max over time, gated on `IsMine && HasGrenade && !CountdownActive && !LocalPauseActive && !ControlsDisabled`, aim from `PlayerManager.shootingDirection`). Added `PlayerManager.ControlsDisabled` getter. Both player prefabs (`Assets/Resources/{Black,White}Player.prefab`) wired.

**Decisions / deviations (baked in):**
- Aim UI = a **widening white `LineRenderer` beam** toward the mouse (charge = length), NOT the spec §3.5 dotted trajectory-arc. User's call.
- `throwForceMax` = **47.8** (playtest), min 8, chargeRamp 1.2.
- Grenade prefab is a **placeholder** (magenta, oversized ×4 for visibility) — user supplies the real sprite; resize + real sprite in G6 polish.

**Temporary debug tooling — REMOVE in G3:** `Assets/scripts/Powerups/GrenadeDebugSpawner.cs` is a self-bootstrapping `DontDestroyOnLoad` AUTO object — press **H** to grant the local player a grenade, hold **G** to throw, on-screen sliders tune force/ramp live. Exists only until real pickups land in G3.

**Process gotchas learned this session (do not repeat):**
- **Exit Play before editing/recompiling scripts.** A mid-play domain reload corrupts `PlatformManager` runtime indices → `ArgumentOutOfRangeException` spam at `PlatformManager.cs:220` (NOT a real bug — it's this).
- **One editor only during asset/script writes** (ParrelSync clone shares `Library/` symlink → asset-DB corruption). Open the clone only for 2-peer tests, close before edits.
- **Test in `level_1-multiplayer`, not `main_menu`** (menu has no arena/camera). Ungated debug key + baked platforms means a host-solo Play is enough for local slices.
- Unity MCP (stdio) `execute_code`: needs `action:"execute"`, no `using` directives (fully-qualify types), must `return` a value.

**Parked (separate from grenade):** MP **platform-color desync** — a player appears to stand on a wrong-coloured platform on one peer; likely a missing/unbuffered paint sync. Needs repro. Task flagged.

**Next agent should:** implement **G3** (spec §8 / §3.1 / §3.2) — falling `GrenadePickup` + `PowerupSpawner` running locally (no RPCs yet), kinematic descent, player-trigger collection granting one grenade, despawn below bounds, single-slot rule; **remove the debug spawner**. Then G4 (networked pickup + master-authoritative claim), G5 (grenade ghost + synced detonation paint), G6 (lifecycle gating + HUD + real sprite/size + strip all debug).

### 2026-07-03 (session 2) — Grenade power-up: branch + tech spec (NO implementation yet)
**Agent session goal:** Plan a new feature — falling grenade power-up (collect 1, hold-to-charge throw, physics bounce with decay, fuse-timer detonation paints all platforms in a radius the thrower's color). MP-only v1.

**What exists now:**
- Branch **`feature/paint-grenade`** off `Multiplayer`, re-pointed on top of the bug-fix commit `85a1f2e2` so the feature builds on the fixed kill/paint flow.
- **`GRENADE_FEATURE_SPEC.md`** at repo root — the full tech spec (architecture, RPC design, physics, tunables, scene wiring, gating rules, implementation order, 2-peer test plan). An implementing agent should read AGENT_CONTEXT.md then that spec and start at its §8 **Slice G1**; the spec's §8 defines 6 slices (G1–G6), each ending in exactly ONE commit with a prescribed message — the user reviews each slice's diff before the next begins. User-confirmed decisions baked in: hold-to-charge clamped ramp (no ping-pong), fuse-timer detonation, MP-only, grenade lost on death, user supplies the grenade sprite later (placeholder until then).
- The spec was largely authored in a parallel session and adopted/amended in this one (preconditions resolved, ramp mode + death rule corrected) — treat the committed version as canonical.

**Uncommitted, deliberately left for the user to decide:** MCP-dependency churn — `Packages/manifest.json` + lock (re-adds `com.coplaydev.unity-mcp`, adds ProBuilder + VFX Graph from the MCP window's Deps tab), `ProjectSettings/InputManager.asset` (Unity 6 auto-added Debug axes), `VFXManager.asset`, `ShaderGraphSettings.asset`, `ProjectSettings/Packages/`. Harmless but should be committed as separate editor/deps churn if kept.

**Next agent should:** implement `GRENADE_FEATURE_SPEC.md` §8 in order on `feature/paint-grenade`; each step must compile + be testable; finish with the §9 playtest matrix.

### 2026-07-03 — Code review: 4 bug fixes (kill-race softlock, pause-exit strand, RestartMusic NRE, hasNoLives)
**Agent session goal:** Verify repo path/state after user's return, then review MP code for bugs and fix findings.

**Project-state finding (already resolved by user):** On 2026-06-20 the project had been opened with **6000.3.16f1** (rewrote ProjectVersion.txt, mono crash 2 min later — the exact scenario the 2026-06-19 entry warns about). User reopened with 6000.4.4f1 and committed the pending working tree as `08387490`.

**Code fixes this session (code-only, compile NOT yet verified in Editor, NOT playtested):**
1. **Kill-race softlock** — `GameManager.RPCReportKillToMaster` used to `return` silently when the master's `CountdownActive` was still true. Countdown end is per-peer coroutine timing, so a death right at GO could pass the victim's `IsMatchStartProtectionActive` gate but hit the master's gate → no `RPCApplyKillResult` → victim never respawned (out of bounds forever). Now the master broadcasts new `[PunRPC] RPCRespawnWithoutScore` (no score change, no HUD animation, just `HandleMultiplayerRoundDeath`) instead of dropping. `_matchOver` drop unchanged (endGame follows anyway).
2. **Pause-exit strand** — synced pause menu freezes BOTH peers (timeScale 0), but pause-menu "Exit to Main Menu" is a local-only exit; the remaining peer stayed frozen with no notification. Added `GameManager.OnPlayerLeftRoom` override → `ForceDismissInGamePauseMenu()` on MP levels.
3. **RestartMusic use-after-destroy** — `GameManager.RestartMusic` had no null checks; `_audioSource` is destroyed by `waitThenEndGame` at game over and `EndMenuManager.CheckButton` (gamepad path) can still call it. Guarded.
4. **hasNoLives robustness** — `GameState.hasNoLives` checked `== 0`; ScoreKeeper returns -1 for unknown names, so a name-key mismatch made SP unwinnable (score drifts negative forever). Now `!= DOESNT_EXIST && <= 0` (matches the MP path's `<= 0`).

**Reviewed but deliberately NOT changed:** remote avatars intentionally kinematic+unsimulated (PhotonInputView owns position — do not "fix" SetCountdownPhysicsFrozen leaving them unsimulated); grey platforms resolve to the SAME color every match in MP (deterministic path-hash — sync-correct, no per-match variety); per-platform Debug.Log spam at scene load (`[Platform IDs]`, `[Grey Platform Init]`) — candidate for stripping in WebGL builds.

**State left behind:** On `Multiplayer` @ `08387490` + uncommitted: `GameManager.cs`, `GameState.cs`, this file. Compile later verified clean (zero `error CS`) in 6000.4.4f1 Editor.log after user reopened with the right version. **Also this session:** wrote `GRENADE_FEATURE_SPEC.md` (paint-grenade power-up tech spec, user-requested) and created branch `feature/paint-grenade` off `Multiplayer` @ `08387490` — NOTE the branch predates the 4 fixes above; commit the fixes to `Multiplayer` and re-branch (or rebase) before implementing, as the spec's §0 instructs.

**Next agent should:** (1) open in Unity 6000.4.4f1, confirm zero `error CS`; (2) 2-peer ParrelSync playtest: normal round kills, a kill immediately at GO (fix 1), pause → one peer exits (fix 2), game over → gamepad button on end menu (fix 3); (3) then user commits/pushes.

### 2026-06-19 — Post-Slice 5 roadmap: SP, gamepad, UI art, WebGL build pipeline
**Agent session goal:** Implement post-Slice 5 roadmap (WebGL ship, re-enable SP, browser gamepad, UI polish).

**WebGL ship:**
- Added `Assets/scripts/Editor/WebGLBuildPipeline.cs` + `Scripts/build-webgl.sh`, `serve-webgl.sh`, `deploy-itch.sh`, `DEPLOY.md`.
- **Blocker:** Unity **6000.3.16f1** has no WebGL Build Support module installed (`build target was unsupported`). Copied 6.4 module + 6.4 build both hit *script class layout is incompatible* in batch mode. **User action:** Unity Hub → 6000.3.16f1 → Add modules → **WebGL Build Support**, then run `./Scripts/build-webgl.sh`.

**Single-player:** `MainMenuUI.cs` re-enabled — Single Player → `level_menu`.

**Browser gamepad:** `WebGamepadController.cs` + `PhotonInputView` sampling; Input Manager axes `WebGL_RightStickX/Y`.

**UI polish:** `UiArt.cs` + designed sprites in `Assets/Resources/UI/`; pause menu, lobby background, game-over banner wired in `InGamePauseMenu`, `MultiplayerLobbyUI`, `EndMenuManager`.

**Also:** `.gitignore` adds `.cursor/`; `EditorBuildSettings` empty scene entry removed; `Assets/link.xml` for Photon WebGL.

**Next:** Install WebGL module on 6.3 → build → `./Scripts/serve-webgl.sh` two-tab test → `./Scripts/deploy-itch.sh` → remote friend test.

**Agent session goal:** User re-orientation after hiatus; document extensive 2-peer MP playtest confirming Slice 5 works as expected.

**Playtest results (user, extensive 2-peer session — multiple replays):**
- **Death + round reload:** both players killable; reload after each death works on both peers.
- **Game-over sync:** end-game screen appears on **both** clients with matching outcome (extends 2026-05-24 desync-fix PASS).
- **Phase 2d remote shot ghosts:** on each kill the non-shooter **sees the shooter's projectile** — ghost shots working as expected.
- **Spawn-fall bug (2026-05-24):** **NOT reproduced** across extensive testing including many round starts — treat as dormant / fixed-by-other-changes until it resurfaces; no code change this session.

**Slice 5 verdict:** **Feature-complete in Editor** for `level_1-multiplayer`. Remaining ship gap = **WebGL two-peer browser test** on Unity 6.3 (2020.3 `abort(163)` may not apply on 6.3 — retest fresh).

**State left behind:** On `Multiplayer` @ `ee70f1a`. Uncommitted working-tree work still present (in-game pause menu, scene/prefab tweaks, deleted `Example.mov`). Git `git diff` may fail on the deleted `.mov` if its blob is missing locally (`8215cfe…` — run `git fetch origin` or commit the deletion to clear). **AGENT_CONTEXT.md updated this session; not yet committed** (user to commit when ready).

**Next agent should:** (1) **WebGL build + two-browser MP test** (top priority for ship); (2) commit pause-menu / scene work when user asks; (3) prune stale `claude/*` worktrees; (4) optionally compress pre-2026-05-23 update-log entries (doc still >300 lines).

### 2026-05-24 — Desync fix PLAYTESTED ✅ (PASS) + spawn-fall bug diagnosed (NOT fixed)
**Agent session goal:** Playtest the master-authoritative game-over desync fix; then fix the MP spawn-fall bug.

**Priority 1 — game-over desync fix: PLAYTESTED & PASSED.** 2-peer ParrelSync round (room `C4DA73`), verified from BOTH editors' logs (MCP was down — see below):
- Both peers logged the SAME verdict `WhitePlayer Lost` via `RPCApplyKillResult` (GameManager.cs:418). Master ran the decider `RPCReportKillToMaster` 7×; guest ran it 0× and only applied the broadcast. Exactly one verdict each, identical → **desync is GONE. The fix works.**
- **Log-based verification technique (reuse — works without MCP):** map editor→log with `lsof -p <pid> | grep Editor.log` (whichever Unity instance grabs `~/Library/Logs/Unity/Editor.log` first owns it; the other gets `Editor-prev.log`). Record `wc -c <log>` as a baseline, have user play a round, then `tail -c +<baseline+1> <log> | grep -E '^(Black|White)Player Lost$'` to read only the new round. This session: main editor → `Editor-prev.log`, clone `_clone_4` → `Editor.log`.

**master / PR#2 decision (user): LEAVE master as-is.** `origin/master` (a7f912b6) and `origin/Multiplayer` (e0227e78) have BYTE-IDENTICAL trees (`git diff` empty) — master is just caught up to the healthy 6.3 state, not holding stale work. Reverting = pure churn. Convention going forward: keep work on Multiplayer.

**MCP for Unity — STILL NOT CONNECTING (do not rabbit-hole; verify via logs instead).**
- stdio server IS loaded & responds, but returned "No Unity Editor instances found" all session.
- Root: the MAIN editor's `Library/MCPForUnity/RunState/` is EMPTY (no stdio bridge registration). The `_clone_4` editor is running an HTTP server on 127.0.0.1:8080 (`mcp_http_8080.pid` in its RunState) — HTTP is the WRONG transport for Claude (already flagged in the prior entry). Two editors open at once muddies discovery.
- The in-editor "MCP for Unity" panel showed green in the main editor, yet RunState stayed empty and stdio never discovered it. **Untested fix for next time:** close the clone, keep ONLY the main editor, enable Auto-Connect / the stdio bridge in its panel, confirm a port/registration file appears in `Library/MCPForUnity/RunState/`, then retest. Config itself is correct (stdio entry in both `claude_desktop_config.json` and `~/.claude/mcp.json`).

**Priority 2 — SPAWN-FALL BUG (NEW, diagnosed, NOT fixed).** Repro: start an MP round; during the READY countdown a player spawns and immediately falls out of bounds → status bar "WhitePlayer(Clone): Killed" before the round starts. Intermittent (depends on which random spawn is picked).
- Current baked spawn coords in `level_1-multiplayer.unity` (MultiplayerSpawner on Bootstrap) — Black: (-30.7,32.13)/(-39.48,24.37)/(-28.29,16.96); White: (29.94,15.59)/(37.99,26.17)/(30.7,34.89). (User re-tuned since the 2026-05-22 values.)
- **All 6 spawn gizmos show GREEN** ("lands here") in the Scene view → static geometry is fine. Logs show valid resolved `standPos` (e.g. anchor (-28.29,16.96)→stand (-28.29,10.74)), and the "no platform under spawn" warning never fired. So the player DOES resolve onto a platform, then falls THROUGH it (kill = `PlayerManager.OnTriggerExit2D`, i.e. left the alive-bounds).
- Paint DOES switch the physics layer (`PlatformManager.ApplyPaintFromNetwork` → `game_manager.ChangeLayer`). `EnsureSpawnPlatformMatchesPlayer` (GameManager.cs:528) finds the SAME closest platform (both it and `SpawnPlatformPreview` raycast down 12f, take closest) and repaints it to the player's color. So in principle the landing platform should be standable — but empirically the player still falls.
- **PRIME SUSPECT — grey-platform randomization overwriting the spawn paint.** Grey platforms pick BLACK/WHITE at init via a path-hash (`PlatformManager.cs` lines 70-95, `UpdateFramework(init_platform_framework)`). If a spawn's platform is grey and its init runs AFTER the spawner repaints it, it resets to the hash color — possibly the OPPONENT's — and the player falls through. Fits "green in editor, falls at runtime" + intermittency. CAVEAT to check: the spawner spawns from a coroutine that waits for `PhotonNetwork.InRoom`, so the paint may actually land several frames after `PlatformManager.Start` — needs runtime confirmation. (Script order: spawner -50, GameManager +100, PlatformManager default 0.)
- Secondary suspect: the countdown freeze ("CountdownActive locks late `PhotonNetwork.Instantiate`'d players") not catching the network-spawned player before it falls. NOTE: kills ARE logged during READY even though `RPCReportKillToMaster` has `if (CountdownActive) return;` — so the fall happens and the player is lost for the round (no respawn while countdown active).
- **To confirm root cause next session:** add a one-line `Debug.Log` in `EnsureSpawnPlatformMatchesPlayer` printing the platform name + its `platform_framework` BEFORE and AFTER paint + the player's standPos; OR get MCP working and query the platform color under each spawn anchor live. Then pick the fix: re-assert the spawn paint after grey init (paint on the next frame / after the player grounds), make spawn platforms grey, or guarantee the freeze catches the spawned player.

**State left behind:** On `Multiplayer` @ e0227e78. **No code changes this session — diagnosis only.** Two Unity editors open (main + ParrelSync `_clone_4`). Deliberately-left pre-existing working-tree files (level_1-multiplayer.unity scene diff, {Black,White}Player.prefab, SceneTemplateSettings.json) untouched.

**Next agent should:** (1) confirm the spawn-fall root cause (diagnostic log or MCP — grey-overwrite hypothesis above) then FIX it; (2) WebGL build retest on 6.3 (priority 3, untouched this session); (3) optionally fix MCP via close-clone + enable-stdio-bridge-in-main. Doc is >300 lines — consider compressing pre-2026-05-23 entries per working agreement #9.

### 2026-05-23 (session 2) — MP game-over desync FIXED (master-authoritative) + Unity MCP installed
**Agent session goal:** Fix the top bug (MP game-over score desync) and set up Unity MCP for Claude.

**Game-over desync — FIXED (code-only, compiles clean; NOT yet playtested).**
- `Assets/scripts/Game/GameManager.cs`: removed the old `PlayerKilled → RPCPlayerKilled (AllViaServer) → DoPlayerKilled` path where every peer decremented its own ScoreKeeper and decided game-over independently (the drift that made one peer log "BlackPlayer Lost" and the other "WhitePlayer Lost"). New flow: dying peer → `RPCReportKillToMaster` (RpcTarget.MasterClient) → master decrements the authoritative score, decides round-respawn vs game-over behind a new `_matchOver` latch (blocks a 2nd near-simultaneous final kill from naming a 2nd winner) → `RPCApplyKillResult` (RpcTarget.All) broadcasts authoritative lives + the single winnerId. Every peer force-sets its score via new `GameState.setScore` + `_gameView.updateScore()`, so HUD and end-game are identical everywhere. `WinnerIdFor` helper shared by SP/MP; `DoPlayerKilled` is now SP-only; `_matchOver` reset in `Awake`.
- `Assets/scripts/Game/GameState.cs`: added `setScore(name, lives)` passthrough to ScoreKeeper.
- **Verified:** Unity 6.3 Editor.log has ZERO `error CS` — compiles. **TODO next session: 2-peer ParrelSync playtest** — play one round to game-over, confirm BOTH editors show the end screen for the SAME winner.
- Committed on `Unity-upgrade` (NOT pushed — user pushes).

**Unity MCP (CoplayDev "MCP for Unity" v9.7.0) — installed, ONE step left.**
- `com.coplaydev.unity-mcp` git dep added to `Packages/manifest.json` (+ `packages-lock.json` lock) — committed. Imported in Editor; first-run wizard green (Python 3.14 / uv 0.11); clicked **Configure All Detected Clients**.
- Claude uses **STDIO** transport (NOT HTTP). Configure wrote the stdio entry to the Claude Desktop config (`~/Library/Application Support/Claude/claude_desktop_config.json`) and `~/.claude/mcp.json`. The Unity panel's "HTTP Local :8080" server is only for HTTP clients (Cursor/VSCode).
- **GOTCHA (cost most of this session):** a manual HTTP `.mcp.json` (`localhost:8080/mcp`) is the WRONG transport for Claude — it connects to the Unity-launched HTTP server, which never exposes the Editor as an "instance" → "No Unity Editor instances found". Project `.mcp.json` is intentionally emptied (`{"mcpServers":{}}`); the real config is the global stdio one. **Don't recreate an HTTP `.mcp.json`.**
- **PENDING:** MCP is not live yet — the Claude Desktop app must be **fully quit (Cmd+Q) and relaunched** (a new chat is NOT enough; MCP loads only at app startup). After relaunch with Unity open, `uvx mcp-for-unity --transport stdio` attaches to the Editor and tools like `read_console` / `manage_scene` work.

**Left UNcommitted on purpose (not edited this session):** `Assets/Resources/{Black,White}Player.prefab` + `Assets/Scenes/level_1-multiplayer.unity` (big ~1177-line scene diff — pre-existing, likely the user's in-progress spawn-point work or a 6.3 reimport), `ProjectSettings/SceneTemplateSettings.json` (on the deliberately-left list), and untracked `.mcp.json` (empty placeholder). User to decide on these in GitHub Desktop.

**Next agent should:** (1) 2-peer ParrelSync playtest of the desync fix; (2) finish the MP spawn-point add (select `Bootstrap` in `level_1-multiplayer` → `MultiplayerSpawner`, Scene-view drag handles, green ring = lands on platform); (3) WebGL build retest on 6.3 (old 2020.3 `abort(163)`/`nullFunc_vi` may behave differently).

### 2026-05-23 — Unity 6 migration: compiles & runs on 6000.4.4f1, but host hard-freezes on MP exit (engine deadlock) → switching to 6.3 LTS
**Agent session goal:** Open project in Unity 6 LTS, migrate, fix compile errors, re-run MP checklist.

**MCP status:** No Unity Editor MCP is connected to Claude Code this session. Setting up CoplayDev "MCP for Unity" was chosen but DEFERRED (compile/runtime issues came first). Resume path: Unity `Window > Package Manager > + > Add package from git URL` → `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`; prereqs (python3 3.14, uv 0.11, node) already installed; then Configure the Claude Code client + **RESTART Claude Code** (MCP servers load only at startup, so the configuring session can't use them).

**Migration so far (UNCOMMITTED on Unity-upgrade):**
- Opened in **6000.4.4f1** → Safe Mode on 4 compile errors. ProjectVersion.txt now 6000.4.4f1; Library regenerated (gitignored).
- **Fix (KEEP):** removed `[SerializeField]` from 4 *properties* (Unity 6's Roslyn enforces field-only target → CS0592): `Assets/scripts/Shot/ShotState.cs` (Rotation/Position/Forward) + `Assets/scripts/Shell/ShellView.cs` (Position). No-ops anyway (Unity never serializes get/set props). Exiting Safe Mode restored the Photon/ParrelSync editor menus (they were missing only because Safe Mode loads a subset of assemblies).
- Left alone: CS0108 warning `BlinkingPlatformManager.FixedUpdate hides PlatformManager.FixedUpdate` (non-blocking).

**MP works on 6.4:** ParrelSync 2-peer join works. First join threw `ExceptionOnConnect` (WSS-fallback timeout) but was **transient — retry fixed it**. Photon Cloud up; host connects EU master over UDP. PUN NOT broken by the upgrade. (PhotonServerSettings present, App ID `159a8424-…`, gitignored.)

**BLOCKING BUG — host editor HARD-FREEZES on "back to main menu" after MP game-over** (guest exits fine):
- Diagnosed via macOS `sample` of the frozen PID (snapshots in /tmp/uhang_*.txt): main thread 100% deadlocked at `DelayedCallManager::Update → SpriteRenderer::MainThreadCleanup → PersistentManager::GetPathName → _pthread_mutex_firstfit_lock_wait`. No other thread holds that native lock → **orphaned by a "prematurely finalized" thread** (recurring log warning). Reproduced identically twice.
- **NOT game code, NOT a Photon API call, NOT the exit C# path** — pure native engine deadlock downstream of `SceneManager.LoadScene`. Ruled out: exit logic (while-loops are yielding coroutines), lightmapper switch (no effect), PUN version (already near-latest lib 4.1.8.17 / PUN2 v2.50, Unity-6-compatible → updating PUN deprioritized).
- `Thread … prematurely finalized` is a documented Unity **macOS** issue (GPU-lightmapper context). Verdict: a Unity **6.4-on-macOS engine threading deadlock**. KEY: **6000.4 is an Update release, NOT LTS.** True LTS = 6000.0 (until Oct 2026) and 6000.3 (until Dec 2027).
- **Decision (user):** test a different Unity patch → **Unity 6.3 LTS `6000.3.15f1`**.

**Temp `[ExitDebug]` breadcrumbs** were added to GameManager exit methods then **removed** this session (red herring).

**State left behind:** Unity-upgrade, uncommitted: ShotState.cs + ShellView.cs `[SerializeField]` fixes (KEEP), ProjectVersion.txt bump + migration Library/ProjectSettings churn. Nothing committed or pushed.

**Next agent should:**
1. After user installs **6000.3.15f1**: quit all editors, delete `Library/` (clean reimport), open project in 6.3 LTS, recreate the ParrelSync clone, re-test host "back to main menu" after an MP round.
2. If freeze GONE on 6.3 → finish the MP checklist, then commit the migration (the 2 SerializeField fixes + ProjectVersion) on Unity-upgrade ONLY; set up Unity MCP if still wanted.
3. If freeze PERSISTS on 6.3 → engine-wide: try `6000.0.75f1` (6.0 LTS), test single-player level→menu to isolate Photon, and/or report to Unity with a /tmp/uhang sample.

**RESOLVED 2026-05-23 (later):** Freeze is GONE on **Unity 6.3 LTS (`6000.3.16f1`)** after a clean `Library/` reimport — confirms the back-to-main-menu hang was a Unity **6.4** (non-LTS Update build) macOS engine deadlock. **6.3 LTS is the migration target now.** Migration still UNCOMMITTED on Unity-upgrade (KEEP the 2 SerializeField fixes; ProjectVersion now 6000.3.16f1).
**NEW BUG found on 6.3:** MP **game-over desync (= score desync)** — guest showed the game-over screen, host did NOT and kept playing. **Confirmed from logs:** one peer logged `BlackPlayer Lost`, the other `WhitePlayer Lost` — the two editors **disagree on who ran out of lives**. Root: lives live in a **per-peer local `ScoreKeeper`** (`GameState.cs` Start/initializeScores/decreaseScore) and **each peer independently decides game-over** in `GameManager.DoPlayerKilled` (line 380; check `hasNoLives && _endGameMenu!=null` → `endGame`). The counts DRIFT, so peers reach game-over at different times / for different players.
  - Likely divergence sources to check: (a) `if (CountdownActive) return;` (GameManager:382) dropping a kill on only one peer (per-peer countdown timing); (b) a non-networked/local `DoPlayerKilled` path double-counting; (c) victim-name derivation / "(Clone)" normalization mismatch so `decreaseScore` hits a different key on each peer. Kill path today: `PlayerManager.OnTriggerExit2D` (IsMine-gated) → `PlayerKilled` → `RPC RPCPlayerKilled AllViaServer` → `DoPlayerKilled` on both.
  - **Robust fix direction:** make game-over **authoritative** — master decides `hasNoLives`/win and RPCs the end-game to ALL (so both peers end together for the same winner), instead of each peer deciding from its own drifting score. Optionally also sync ScoreKeeper from the master. NOT yet implemented. Likely a pre-existing MP weakness, not migration-specific.

**END OF SESSION (2026-05-23) — committed & handed off:**
- **Migration COMMITTED** on `Unity-upgrade`: `96c5da9b` "Migrate project to Unity 6.3 LTS (6000.3.16f1)" = the 2 `[SerializeField]` property fixes + API-Updater `velocity→linearVelocity` across game+Photon scripts + ProjectVersion/manifest/packages-lock bumps + new ProjectSettings (MemorySettings, MultiplayerManager). **NOT pushed yet** — user pushes via GitHub Desktop (Claude can't reach Keychain).
- **Cleanup:** deleted untracked junk `Assets/_Recovery/` (editor crash backups from force-quits) + `Assets/MobileDependencyResolver/` (Google EDM4U, regenerates, unused on WebGL). Added both + `_Recovery` to `.gitignore` — that `.gitignore` edit is a separate small commit the user is making in GitHub Desktop (so they'll push ~2 commits ahead). `PhotonServerSettings.asset` stays gitignored (App ID credential).
- **Project healthy on 6000.3.16f1:** compiles, runs, ParrelSync 2-peer MP works, back-to-main-menu freeze GONE.
- **In-progress Q (unfinished):** user wants to add a multiplayer **spawn point** → select `Bootstrap` in `level_1-multiplayer`, add an X/Y to `MultiplayerSpawner.blackSpawnPositions`/`whiteSpawnPositions` (`Vector2[]`) via the Inspector array or the Scene-view drag handles (green ring = lands on platform). The spawner random-picks among them; there is no single "default".

**Open work next session (priority):**
0. **Compress THIS doc first (working agreement #9)** — do it at the start with full context budget. Fold the individual 2026-05-22 per-session entries (Shoot burst HUD; main-menu layout; lobby auto-assign; lobby UX; countdown sync; side-aim ×2; mouse-aim — all committed & superseded) into the consolidated 2026-05-22 summary; the doc is read in full every session so length costs tokens. **KEEP verbatim:** the STOP section, the pre-upgrade test checklist, the spawn coords + "Tuning MP spawn positions" instructions, the architecture seams, the WebGL dead-end section, and this 2026-05-23 migration entry.
1. **MP game-over score desync** — make game-over master-authoritative (see fix direction above). Highest-value gameplay bug.
2. Finish the spawn-point add the user was asking about.
3. **Unity MCP setup** (CoplayDev "MCP for Unity") if still wanted — Package Manager git URL `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`; configure Claude Code client; **RESTART Claude Code** after (MCP loads at startup).
4. **WebGL build retest on 6.3** — the 2020.3.48 `abort(163)`/`nullFunc_vi` blocker (see WebGL section below) may behave differently on Unity 6.3; verify before assuming it persists.

### 2026-05-22 — Pre-upgrade baseline (Unity 2020.3.48f1) → Unity 6 LTS migration branch
**Agent session goal:** Lock a known-good **2020.3.48f1** baseline before Unity upgrade; hand off migration to a **Unity MCP-capable agent** (user switched agents — baseline agent could not drive the Editor).

**Agent handoff:** User switching to another Cursor agent with **Unity Editor MCP** configured. That agent owns: **`Unity-upgrade`** branch, Unity 6 LTS project open, package/scene upgrades, compile fixes, and play-mode verification via MCP. Baseline agent committed + pushed on `Multiplayer` only.

**Pre-upgrade engine:** Unity **2020.3.48f1** (`ProjectSettings/ProjectVersion.txt`).

**Target upgrade:** Unity **6 LTS (6000.x)** — for Unity MCP / editor control during migration (not 2022.3).

**Baseline commit:** `5a3323ef` on `Multiplayer` — last known-good state before upgrade branch (`origin/Multiplayer`).

**Migration branch:** **`Unity-upgrade`** @ `5a3323ef`, pushed to `origin/Unity-upgrade` — branched from baseline; **do not upgrade on `Multiplayer` directly**.

**Pre-upgrade test checklist (re-run on Unity 6 after migration; optional on 2020.3.48 before upgrade):**
- Editor opens `main_menu` without console errors
- **Multiplayer** button → lobby → create room / join / share link
- **Single Player** intentionally disabled (gray, non-clickable) — `Assets/scripts/MainMenuUI.cs`
- `level_menu` → level buttons load `level_1`..`level_4` by name
- `level_1-multiplayer` → ParrelSync two-peer: spawn, synced countdown, one full round (death/reload optional)
- WebGL build: **expect failure on 2020.3.48** (`abort(163)` / `nullFunc_vi` — see WebGL section below); retest after upgrade

**Baseline commit included:** menu/lobby UI gray polish, `MainMenuUI`, level menu scene-by-name loading, MP spawn/session/lobby fixes (`MultiplayerSpawner`, `MultiplayerBootstrap`, `MultiplayerLobbyUI`, `GameManager`, etc.).

**State left behind:** `Multiplayer` and `Unity-upgrade` both at `5a3323ef`, pushed; user on `Unity-upgrade`; Unity 6 migration not started (`ProjectVersion.txt` still 2020.3.48f1).

**Next agent should (Unity MCP agent):**
1. Confirm Unity MCP connected (`unity_status` / compilation tools).
2. Stay on **`Unity-upgrade`**; open project in Unity 6 LTS; accept upgrade prompts; fix compile errors incrementally.
3. Re-run pre-upgrade test checklist on Unity 6; append pass/fail deltas in a follow-up AGENT_CONTEXT entry.
4. Commit migration changes to **`Unity-upgrade`** only; do not merge to `Multiplayer` until verified.

### 2026-05-22 — Shoot burst HUD, tuning, and quieter SFX (committed)
**Agent session goal:** Per-player shoot cooldown HUD under lives; tune burst length and SFX; color-match bar to player.
**What I did:**
- **`ShootCooldownHud`** (`Assets/scripts/Player/ShootCooldownHud.cs`) on **Game** (`Game.prefab` + baked `Game` in `level_1-multiplayer.unity`) — 8px burst-ammo bar under local player's lives row; **black** for Black, **white** for White; wired to `PlayerManager.BurstAmmo01`. Removed per-prefab `ShootCooldownUI`.
- **`PlayerManager.BurstAmmo01`** — HUD tracks continuous-fire window, not inter-shot cooldown.
- **Burst window** — `burstFireDurationSeconds` **0.5s** (prefabs + code); release fire to reset.
- **`PlayerSFX`** — `shootVolume` **0.22**; new `impactVolume` **0.2** (shot-hit sound ~80% quieter).
- **`LivesVisualizer.RowWorldWidth`** — bar width tracks lives row.
- **`HideSceneUIInEditMode.cs.meta`** — fixed invalid 33-char GUID; restored script ref on scene `Game`.
**State left behind:** pushed to `origin/Multiplayer`.
**Next agent should:** playtest MP burst bar + SFX levels; tune `barHeight` / offsets on Game if needed.

### 2026-05-22 — Random MP spawn points (3 per color)
**Agent session goal:** Opponent could camp the single predictable spawn platform and spawn-kill loop victims.
**What I did:**
- `MultiplayerSpawner` — `blackSpawnPositions` / `whiteSpawnPositions` (3 each); random pick on each `TrySpawn`/`ForceRespawn`; `_cachedLocalSpawnPosition` for countdown reset (same point for that life).
- `level_1-multiplayer` — six coordinates on Bootstrap **MultiplayerSpawner** (see spawn tuning below).
**ParrelSync test:** Die repeatedly — respawn moves among 3 points; match countdown reset does not re-roll spawn for that life.

**Current spawn coords (user-tuned in Scene view, 2026-05-22):**
- Black: `(-30.7, 31.04)`, `(-10.7, 34.46)`, `(-40.7, 42.88)`
- White: `(38.12, 24.73)`, `(20.12, 25.90)`, `(30.7, 34.89)`

**Tuning MP spawn positions:** Open `level_1-multiplayer` → select **Bootstrap** (`MultiplayerSpawner`). Use **Scene view** (not Game view): drag colored spawn handles; **green ring** = landing on platform below; dotted yellow line = drop from spawn height. Inspector button **Frame spawn points in Scene view**, or menu **Multiplayer → Frame level_1-multiplayer spawn area**. Scene auto-frames spawn area on open. `HideSceneUIInEditMode` on **Game** hides BG / EndGameMenu / countdown UI while editing so platforms are visible (restored in Play mode).

### 2026-05-22 — Main menu button layout (user-tuned)
**What changed:** `main_menu.unity` — **multiplayer button** anchored `y: 29.6` (was `0`); **singleplayer button** `y: -30.2` (was `-50`). Spacing only; multiplayer still loads `Multiplayer` lobby scene.

### 2026-05-22 — Lobby: auto-assign colors (no pre-game pick) — confirmed, debug removed
**Agent session goal:** Two players could both pick White via lobby Host buttons; user wanted no color choice before match.
**What I did:**
- Removed Host as Black/White UI → single **Create room** button.
- `MultiplayerColorAssignment.cs` — master gets `roomMasterColor` (Bootstrap default Black); joiner opposite; claims `myFramework` in `OnJoinedRoom`.
- Lobby status: “You are playing as Black/White”.
**Next agent should:** commit when user asks.

### 2026-05-22 — Multiplayer lobby UX (waiting room + join UI)
**Agent session goal:** Formal lobby: host picks color and shares link; guest joins via UI; auto-start game when room is full.
**What I did:**
- `MultiplayerSceneNames.cs` — `Multiplayer` (lobby) vs `level_1-multiplayer` (game). `GameManager.IsMultiplayerLevel()` uses active scene name only (lobby no longer triggers countdown).
- `MultiplayerBootstrap.cs` — connect-only in lobby; `CreateRoom()` / `JoinRoomCode(string)`; `InRoom` guard on reconnect; room-full → `LoadLevel` unchanged.
- `MultiplayerLobbyUI.cs` — Create room, guest join field, share URL + Copy, status text. Replaces deleted `LinkShareUI.cs`.
- Scenes: `Multiplayer.unity` = Bootstrap + lobby UI only (removed Game prefab + Spawner). `level_1-multiplayer` Bootstrap = Spawner only.
- `main_menu` multiplayer button → `LoadSceneByName("Multiplayer")`. `main_menu.unity` added to build settings.
**ParrelSync test:** Host: menu or Play `Multiplayer.unity` → Create room → copy link. Guest: lobby → paste code → Join (no color buttons). Status should show opposite colors; both load game scene → spawn → synced countdown.
**WebGL test:** Guest with `?room=CODE` auto-joins from lobby (no form).
**Next agent should:** playtest; commit when user asks.

### 2026-05-22 — MP countdown sync + host lobby freedom; debug instrumentation removed
**Agent session goal:** User confirmed MP countdown/host-wait behavior fixed; remove debug logging.
**What I did:**
- Removed `DebugSessionLog.cs` and agent-log regions from `GameManager.cs`.
- **Kept:** RPC-synced match countdown (`RPCStartMatchCountdown`); host/joiner **not** input-locked until countdown; `IsReadyForMatchCountdown()` (room full + 2 scene avatars); spawn reset via `MultiplayerSpawner.ResetLocalPlayerToSpawnPosition()` before Ready/Set/Go.
**State left behind:** uncommitted mouse-aim + animator + MP countdown bundle on `Multiplayer`.
**Next agent should:** commit when user asks.

### 2026-05-22 — Side aim animation fixed; debug instrumentation removed
**Agent session goal:** User confirmed side mouse-aim pose works; remove debug logging.
**What I did:**
- Removed `Assets/scripts/Utils/DebugSessionLog.cs` and all `#region agent log` blocks from `PlayerView.cs` / `PlayerManager.cs`.
- **Fix kept:** `PlayerView.AssignDirectionalClip` resolves `idle_0/1/2` by exact clip name (prefab override → Editor asset path → `animationClips`); prevents slot 1 from getting `idle_2` (up pose) when aiming side.
**State left behind:** uncommitted mouse-aim + animator + side-aim fix bundle on `Multiplayer`; user to commit when ready.
**Next agent should:** commit when user asks; optional MP/SP playtest matrix from mouse-aim entry.

### 2026-05-22 — Side aim animation still broken (debug session `5efe84`, parked) — RESOLVED
**Agent session goal:** Fix side (horizontal) aim pose after mouse-aim + 3-direction animator work; user stopped for the day without committing.
**What I did (debug, runtime-evidence):**
- Instrumented `PlayerManager.updateDirection` (hypothesis A) and `PlayerView` clip cache / sampling (B–E) via `Assets/scripts/Utils/DebugSessionLog.cs` → `.cursor/debug-5efe84.log` (Editor-only NDJSON).
- **Confirmed:** aim classification is correct (`verticalDir:0`, layer `not_shooting_1`, `wantsSide:true`) — not a `PlayerManager` bucketing bug.
- **Confirmed root cause:** `idle_1` / `white_idle_1` is **not** in `RuntimeAnimatorController.animationClips` (only `idle_0` + `idle_2` from synced override layers). Side sampling calls `SampleAnimation` with a null clip → no side pose.
- **Why briefly correct at load:** base-layer idle state still references `idle_1`; first frame shows side, then `ApplyAnimatorState` + failed sampling leaves a non-side pose.
- **Attempted fixes (still broken in playtest):**
  - Added `idle_1` motion override on `not_shooting_1` in black/white `.controller` — did **not** add clip to `animationClips`.
  - `Resources.FindObjectsOfTypeAll` at `Awake` — failed (`resolvedViaFind:false`) because clip not loaded yet.
  - `PlayerView` prefab `_directionalIdleClipsOverride` arrays on `BlackPlayer` / `WhitePlayer` + Editor `AssetDatabase.LoadAssetAtPath` fallback + lazy re-resolve in `applyDirectionalAimPose`.
- **Latest log anomaly (post-fix3):** cache reports `"idle1":"idle_2"` and sampling uses `idle_2` for `idx:1` — wrong clip for side bucket; next agent should verify override slot 1 resolves to **`idle_1` by name**, not `idle_2` (possible overwrite order or asset rename mismatch).
- Fire input moved to **LMB** in `MouseAimController`; `isShooting` OR across controllers in `PlayerManager`.
**State left behind:** large **uncommitted** diff on `Multiplayer` (controllers, `PlayerView`, `PlayerManager`, animator assets, idle clip renames/deletes, both player prefabs, debug logger). User explicitly **not committing** until side aim works. Debug instrumentation still in code — remove after verified fix.
**Next agent should:**
1. Read `.cursor/debug-5efe84.log` (or re-run with `DebugSessionLog`) and fix slot-1 clip resolution so `idle1` is `idle_1` and `sampled:true` for `verticalDir:0`.
2. Consider skipping `SampleAnimation` for side when reference layer + base idle already show `idle_1`, **or** assign side clip via override layer the same way as `not_shooting_0` / `not_shooting_2` (full `m_Motions` set on synced layers).
3. Remove `DebugSessionLog` + `#region agent log` blocks after user confirms fix; then commit when user asks.

### 2026-05-22 — Mouse aim + 3-direction animations (mostly done; side pose open)
**Agent session goal:** Continuous mouse aim from player position; WASD move only; simplify aim anim to down/side/up; replicate full aim + separate moveX over Photon.
**What I did:**
- `Assets/scripts/Controllers/MouseAimController.cs` — screen-to-world aim; LMB shoot; added to `BlackPlayer` / `WhitePlayer` prefabs (disabled by default; enabled on local MP peer via `PhotonInputView` + `PlayerManager.ConfigureMouseAim`; SP: Black only).
- `MultiplayerKeyboardController` / `KeyboardController` — movement unchanged; aim returns zero when mouse controller active; MP keyboard no longer fires.
- `PlayerManager` — mouse aim priority; `ClassifyAimDirection` dominant-axis bucketing; side-bucket landing guard; `isShooting` OR across controllers.
- `PlayerView` — 3 aim layers (`not_shooting_0/1/2`); reference `not_shooting_1`; directional idle clip cache + `SampleAnimation` in `LateUpdate` (order 150); white clip prefix.
- `PhotonInputView` / `NetworkController` — stream `Vector2 aim` + `float moveX` (no axis snap on aim).
- Animator assets: removed diagonal layers/clips; renumbered `idle_0/1/2` and `white_idle_*`; fixed black/white `.controller` layer names.
**Playtest status:** up/down and **side** aim work (2026-05-22). Side fix: exact-name clip assignment in `PlayerView.AssignDirectionalClip`.
**State left behind:** same uncommitted tree; do not commit until side pose fixed (user 2026-05-22).

### 2026-05-22 — Spawn-first countdown (multiplayer)
- **`GameManager`:** On scenes with `MultiplayerSpawner`, never use single-player `TryStartCountdownSinglePlayer` (`countDownEveryRound=1` would start before spawns). Each peer polls until **two** `player`-tagged objects with `PhotonView` exist, then runs `startCountDown` locally (players visible first, then frozen until "Go"). Script order: spawner −50, GameManager +100.
- **Once per match:** `s_matchStartCountdownPlayed` survives `PhotonNetwork.LoadLevel` round reloads (death → 1.5s → reload) so Ready/Set/Fight does **not** loop every life; cleared on Replay / leaving room. Freeze + `CountdownActive` during the wait-for-both-players phase; deaths ignored while `CountdownActive`.

## 2026-05-22 — Log compressed into this summary; Slice 5 phase 2d (remote shot ghosts) implemented

> **Note:** all per-session entries before 2026-05-22 were folded into this single
> summary to shrink the doc (it's read in full every session). The load-bearing facts
> — current state, non-obvious decisions, and the WebGL dead-ends — are preserved
> below. Append new dated entries **above** this one going forward.

### Current status (where we stand)
- **Branch `Multiplayer`**, tracks `origin/Multiplayer`. Multiplayer stack: Photon PUN 2, host-authoritative cloud relay, EU dev region, **WebGL ship target**, link-share only (no lobby UI), exactly 2 players, friends-only (no reconnect / anti-cheat / >2 players).
- **Engine:** Unity **6.3 LTS (`6000.3.16f1`)**.
- **Slices 1–4 done & playtested:** two peers in a room, color assignment, networked input, transform sync.
- **Slice 5 (full networked round) — FEATURE-COMPLETE IN EDITOR** (user playtest PASS 2026-06-19):
  - 2a lobby→level transition — done & playtested
  - 2b networked platform paint — done & playtested
  - 2c networked death + level reload + life decrement — done & playtested
  - 2d remote shot ghosts — done & playtested (remote peer sees shooter's projectile on kills)
- **Spawn-fall during countdown:** diagnosed 2026-05-24, **not reproduced** in extensive 2026-06-19 testing — parked unless it resurfaces.
- **Next ship milestone:** WebGL two-peer browser test on 6.3 (see WebGL section — was blocked on 2020.3 only).

### Phase 2d implementation (committed; playtested PASS 2026-06-19)
The owner's shot spawns + paints locally as before; remote peers now see a **visual-only ghost** projectile.
- `Assets/scripts/Shot/ShotState.cs` — new `[HideInInspector] public bool isGhost`.
- `Assets/scripts/Shot/ShotManager.cs` `Activate` + `Assets/scripts/Shot/ShotFactory.cs` `MakeObject` — new optional `bool isGhost = false` param (single-player path unchanged).
- `Assets/scripts/Game/GameManager.cs` `SpawnShot` — when `PhotonNetwork.InRoom`, RPCs `(pos, startVelocity, rotation, framework)` to `RpcTarget.Others`; new `[PunRPC] RPCSpawnGhostShot` instantiates the ghost via the factory.
- `Assets/scripts/Player/PlayerManager.cs` `shoot` — in a room, only `photonView.IsMine` spawns the real shot/shell (remote players get the ghost instead → no double-spawn / double-paint). The shooting animation still plays on the remote.
- `PlatformShotSensor.cs`, `PlatformView.cs`, `PlatformLetterView.cs` — all three shot→paint collision handlers skip `UpdateHit` when `shot_state.isGhost` (paint stays authoritative via the 2b paint RPC).
- Ghost reuses the existing Shot prefab (no PhotonView on it) via a plain `Instantiate`, so **no Unity/prefab work is required** — just let the Editor recompile.
- Intentional scope cuts: ejected **shells** are NOT ghosted; the remote peer won't hear the opponent's shoot SFX (`EnableSFX` is off by default). Both extend later with the same RPC pattern if wanted.

### Condensed history & key non-obvious decisions (slices 1 → 5)
Most "current behavior" detail now lives as code comments; this is the orientation map.
- **Setup:** PUN 2 imported (~39 MB under `Assets/Photon/`, tracked as one checkpoint). `PhotonServerSettings.asset` (+ `.meta`) is **gitignored** — each dev pastes their own App ID via the PUN Setup Wizard (App ID is a credential; repo is public GitHub). Region = EU dev.
- **Two-peer testing = ParrelSync**, embedded at `Packages/com.veriorpies.parrelsync/` (file: path) and patched (`Editor/Preferences.cs` line 91: `string.Split(string)` → `Split(new[]{token}, …)` for .NET Standard 2.0 / Unity 2020.3). Run **one Editor at a time for shell file moves** — concurrent Editors share `Library/` via symlink and trip LMDB asserts (`MDB_MAP_RESIZED`).
- **Prefabs:** `Assets/Prefabs/Player.prefab` is DEAD (0 scene refs). Real gameplay prefabs are `Assets/Resources/{Black,White}Player.prefab` (moved to Resources for `PhotonNetwork.Instantiate`). **Edit prefabs from the host Editor only** (ParrelSync clone save-block corrupts otherwise).
- **Color assignment:** each peer claims its color via a `myFramework` player custom property (rejoin-safe); falls back to the `hostColor` room-property heuristic only if unclaimed.
- **Networked input (Slice 4):** `PhotonInputView` owns the whole stream (aim + 4 button bools + position) as a single `IPunObservable`; `NetworkController : Controller` replays it on the remote; `MultiplayerKeyboardController` (WASD / Space / Shift) drives the local MP player. **PhotonTransformView was removed** — it drifted remotes "into the sky"; position is now Lerp'd inside `PhotonInputView`. PhotonView Synchronization = **Unreliable** (not "Unreliable On Change", which suppressed packets while idle). Remote rigidbody = Kinematic + `simulated = false`.
- **MP scenes:** `Multiplayer.unity` = **lobby** (`MultiplayerBootstrap` + `MultiplayerLobbyUI` — Create room, guest join field, share link; auto color assign). `level_1-multiplayer.unity` = **game** (`MultiplayerSpawner` — 3 random spawn points per color; synced countdown in `GameManager`). Room full → master `PhotonNetwork.LoadLevel`.
- **Slice 5 phase 2b/2c (current behavior):**
  - Platform paint RPC keyed by deterministic `networkId` = index after sorting all `PlatformManager` by **scene hierarchy path** (identical across peers; InstanceID/position were not, and caused id drift). Paint RPC = `RpcTarget.Others` (NOT buffered — buffered replays onto the fresh scene after reload).
  - Grey platforms pick their start color from a **deterministic FNV-style hash of the hierarchy path** in a room (not `Random.Range`, which differs per process). Single-player keeps Random.
  - Death: `PlayerManager.OnTriggerExit2D` gates `PlayerKilled` by `IsMine`; `GameManager.PlayerKilled` RPCs `RpcTarget.AllViaServer` (ordered, fires once); player name normalized (strip "(Clone)").
  - Reload: **every peer** calls `PhotonNetwork.LoadLevel` locally in `waitThenReloadGame` (NOT master-only, NOT `SceneManager.LoadScene`). Reason: PUN's same-scene `AutomaticallySyncScene` trigger doesn't fire for joiners, and plain `SceneManager.LoadScene` leaves the new scene's PhotonView unregistered → RPC NREs next round. `AutomaticallySyncScene = true` is set on every peer in bootstrap.
  - `GameState.initializeScores` hardcodes seed names `"BlackPlayer"`/`"WhitePlayer"` (FindGameObjectsWithTag returns empty in the MP scene).
  - `GameManager.CountdownActive` static flag locks late-spawned (PhotonNetwork.Instantiate'd) players during the countdown. `muteMusicForTesting` toggle stops music restarting on every LoadLevel reload.

### WebGL build — BLOCKED on 2020.3; RETEST on 6.3 before assuming still broken
WebGL is the ship target. On **Unity 2020.3.48f1** the build aborts at runtime the instant Photon opens its WebSocket:
```
Invalid function pointer called with signature 'vi'.
abort(163) … nullFunc_vi … WebSocket.<anonymous> (framework.js) … dynCall_vi … b163 (build2.wasm)
```
Working theory: a Unity 2020.3.48 IL2CPP function-pointer-table bug hit by PUN's WebGL WebSocket callback registration. **None of the 2020.3 experiments below moved it — do NOT re-run on 2020.3:**
- Managed Stripping Level Low (the floor in 2020.3; no Disabled/Minimal option) — same abort.
- Project-level `link.xml` preserving PUN/Realtime/Chat/WebSocket/Photon3Unity3d — same abort. (Broader preserves → different failure: `build.bc is not valid LLVM bitcode`.)
- `.NET 4.x` ↔ `.NET Standard 2.0` — identical crash.
- "Strip Engine Code" off → build fails entirely (`build.bc not valid LLVM bitcode`). Re-enabled.
- Development Build off (release) — same abort.
- Lightmap Encoding Normal Quality — same abort.
- IL2CPP cache wipe (`rm -rf Library/Bee Library/IL2CPPBuildCache Library/PlayerDataCache`) + rebuild — same abort on 2020.3.
**Next step (2026-06-19):** project is now on **Unity 6.3 LTS** — do a **fresh WebGL build + two-browser MP test** before investing more time in 2020.3 workarounds. Editor + ParrelSync two-peer works fine.

### Known carry-over issues & cleanup backlog
- **Only `level_1-multiplayer` is networked.** Each new MP level needs a manual PhotonView on its `Game` GameObject until `Game` is made a real prefab instance in MP scenes.
- **WebGL two-peer (real browser build) never validated** — top remaining ship task; retest on Unity 6.3 (see WebGL section).
- **Uncommitted local work:** in-game pause menu (`InGamePauseMenu.cs`, `PauseMenu.prefab`, related `GameManager` RPCs) + scene/prefab tweaks — user-tested MP core loop; commit when user asks.
- Cosmetic, long-deferred: doublejump sprite pivot mismatch; slight feet hover at jump-land transitions.
- Repo hygiene: ~25 stale `claude/*` worktrees/branches (verify none hold unmerged work, then prune); some remote-tracking refs may have invalid SHAs locally (`git fsck` warnings) — `git fetch --prune origin` may help.
- Lingering uncommitted-across-sessions files the user deliberately leaves: `UserSettings/EditorUserSettings.asset`, older `level_2.unity` / `start_scene_2.unity` + lighting bakes, `ProjectSettings/{SceneTemplateSettings.json,TimelineSettings.asset}`.

### Next agent should
1. Acknowledge the context-warning convention first (user auto-memory).
2. **WebGL build + two-browser MP test** on Unity 6.3 (top priority for ship).
3. Commit pause-menu / scene work when user asks.
4. Prune stale `claude/*` worktrees if user wants repo cleanup.
5. Optionally compress pre-2026-05-23 update-log entries (doc still >300 lines).
