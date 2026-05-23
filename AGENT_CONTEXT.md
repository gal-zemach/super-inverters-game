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
- **Active branch:** `Multiplayer` (tracked to `origin/Multiplayer`). **Dirty working tree** as of 2026-05-22 — mouse-aim + 3-dir animation work + side-aim debug (user chose **not to commit** until side pose is fixed).
- **Engine:** Unity **2020.3.48f1** (LTS). Originally a Unity 2017 project. **Do not "upgrade" the project further** — 2020 is the version that builds and runs. 2017 was tried and failed. Unity Hub shows a red icon next to this version (likely just a "no longer supported by Unity" warning, not a project error — confirm before reacting to it).
- **Project is registered in Unity Hub** under the name `super-inverters-game`. The user opens the project from there. Don't `Add project` again.
- **Latest published web build:** https://nmeidan.itch.io/superinverters — the live WebGL build, "the latest web version."
- **Build target for multiplayer: WebGL.** Confirmed by user 2026-04-30. Plan all networking choices around WebGL constraints (no raw UDP; use WebSocket transport or WebRTC).
- **No lobby UI.** Confirmed by user 2026-04-30. The flow is link-share only; do not build a server browser or room list.
- **Networking stack: Photon PUN 2.** Confirmed 2026-04-30 from a screenshot of the user's Photon dashboard. The app on the dashboard is named **"Super Inverters"**, type **PUN** (= PUN 2; "PUN Classic" is long deprecated), free tier 20 CCU, status Public, **App ID prefix `159a8424-...`** (full value not stored here on purpose; see security note below).
- **Photon App ID is a credential.** This repo is hosted on public GitHub. Do **not** commit the full App ID in any file (including `PhotonServerSettings.asset`, which the PUN setup wizard creates). When the SDK is installed, add the asset path PUN generates to `.gitignore`, OR keep a stub asset committed and load the real ID from a `.env`-style untracked file. Decide before the first PUN-related commit.
- **No Photon files in the project yet.** Verified 2026-04-30. The prior agent walked the user through Photon signup + dashboard but never started the Unity install. That's where the user got stuck.
- **Goal of this branch:** add **simple multiplayer** to a 2-player local game. See "Multiplayer goal" below.
- **No CLAUDE.md exists.** This file is the source of truth for project-wide guidance until one is written.

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
│   │   ├── Player/         PlayerManager, PlayerState, PlayerView, PlayerSFX, etc.
│   │   ├── Controllers/    Controller (base), KeyboardController, PS4Controller, LevelMenuController
│   │   ├── Shell/, Shot/, Platform/, Movement/, Utils/, Editor/
│   │   └── (top-level)     SceneLoader, ScoreKeeper, Values, etc.
│   ├── Resources/, Materials/, Graphics/, Audio/, Animations/, Plugins/
├── Packages/manifest.json   (Unity package list — see below)
├── ProjectSettings/, Library/, Temp/, UserSettings/, Logs/
├── Web build/, super_invereters_web/   (existing WebGL build outputs)
└── .git/  (origin: GaliGuess/Algamedes_Jam2 — confirm with `git remote -v`)
```

**Networking packages currently installed:** none. `Packages/manifest.json` has no `com.unity.netcode.*`, `com.unity.transport`, Mirror, or Photon. **Adding multiplayer requires installing a networking stack** (see "Approach" below — discuss with the user before picking).

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

### 2026-05-22 — Shoot cooldown, lower SFX, HUD indicator
**What I did:**
- `PlayerManager` — `turnsBetweenShots` (2 = very high fire rate), `burstFireDurationSeconds` (1.2s continuous fire per hold; release fire to reset burst).
- `PlayerSFX.shootVolume` (default 0.35).
- `ShootCooldownUI` on Black/White player prefabs — radial fill for inter-shot cooldown; move **ShootCooldownHUD/CooldownIndicator** RectTransform in prefab to reposition (or context menu **Rebuild Cooldown HUD** on component).

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
- **Slices 1–4 done & playtested:** two peers in a room, color assignment, networked input, transform sync.
- **Slice 5 (full networked round):**
  - 2a lobby→level transition — done & playtested
  - 2b networked platform paint — done & playtested
  - 2c networked death + level reload + life decrement — done & playtested
  - **2d remote shot ghosts — IMPLEMENTED 2026-05-22, NOT yet playtested.** Uncommitted in the working tree (8 files). Needs Unity recompile + a 2-peer ParrelSync test before commit/push.
- Once 2d verifies, **Slice 5 is feature-complete** and a multiplayer round plays end-to-end.

### Phase 2d implementation (uncommitted, 8 files, code-only — no prefab/Unity edits needed)
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

### WebGL build — BLOCKED (do NOT re-run these experiments)
WebGL is the ship target but the build aborts at runtime the instant Photon opens its WebSocket. This is the single most important parked problem and exists nowhere in code. Build *succeeds* (~120–140s); the wasm aborts:
```
Invalid function pointer called with signature 'vi'.
abort(163) … nullFunc_vi … WebSocket.<anonymous> (framework.js) … dynCall_vi … b163 (build2.wasm)
```
JS WebSocket callback dispatches into wasm via `dynCall_vi` → function-table index 163 is null → abort. **Deterministically baked into IL2CPP's wasm for this project + PUN + Unity 2020.3.48f1 + macOS.** None of these moved it — DO NOT retry:
- Managed Stripping Level Low (the floor in 2020.3; no Disabled/Minimal option) — same abort.
- Project-level `link.xml` preserving PUN/Realtime/Chat/WebSocket/Photon3Unity3d — same abort. (Broader preserves → different failure: `build.bc is not valid LLVM bitcode`.)
- `.NET 4.x` ↔ `.NET Standard 2.0` — identical crash.
- "Strip Engine Code" off → build fails entirely (`build.bc not valid LLVM bitcode`). Re-enabled.
- Development Build off (release) — same abort.
- Lightmap Encoding Normal Quality — same abort.
- IL2CPP cache wipe (`rm -rf Library/Bee Library/IL2CPPBuildCache Library/PlayerDataCache`) + rebuild — same abort.
Working theory: a Unity 2020.3.48 IL2CPP function-pointer-table bug hit by PUN's WebGL WebSocket callback registration. **Before spending another session on it:** search Photon forums / Unity issue tracker for `abort(163)` + `nullFunc_vi` + `WebSocket.<anonymous>` on 2020.3 macOS; try the build on Windows/Linux to isolate the macOS toolchain; consider a Unity version bump (a deliberate user decision — do NOT unilaterally upgrade, per the STOP section). Editor + ParrelSync two-peer works fine; only the WebGL build is blocked.

### Known carry-over issues & cleanup backlog
- **Only `level_1-multiplayer` is networked.** Each new MP level needs a manual PhotonView on its `Game` GameObject until `Game` is made a real prefab instance in MP scenes.
- WebGL two-peer (real browser build) never validated end-to-end — see WebGL block above.
- Cosmetic, long-deferred: doublejump sprite pivot mismatch; slight feet hover at jump-land transitions.
- Repo hygiene: merge-commit noise on `Multiplayer` from merging `claude/slice-5` twice; ~25 stale `claude/*` worktrees/branches (verify none hold unmerged work, then prune); the `claude/slice-5` worktree at `suspicious-noyce-2a5242` is behind (`3606c5cb`) — the main worktree is the source of truth.
- Lingering uncommitted-across-sessions files the user deliberately leaves: `UserSettings/EditorUserSettings.asset`, older `level_2.unity` / `start_scene_2.unity` + lighting bakes, `ProjectSettings/{SceneTemplateSettings.json,TimelineSettings.asset}`.

### Next agent should
1. Acknowledge the context-warning convention first (user auto-memory).
2. Side aim fixed 2026-05-22; commit mouse-aim / animator / controller bundle when user asks.
4. Phase 2d remote shot ghosts still need 2-peer playtest + commit if not done yet.
5. Then: **real WebGL two-peer build test** (ship target) — forum/issue-tracker search for `abort(163)` / `nullFunc_vi` first; do not unilaterally upgrade Unity.
