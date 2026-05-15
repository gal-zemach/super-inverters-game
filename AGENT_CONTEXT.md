# AGENT_CONTEXT — Super Inverters Reloaded (Multiplayer)

> **Purpose:** This file is a context handoff between agents working on this repo.
> Image/output limits in agent sessions can wipe working memory. Read this on every
> fresh session and append a log entry before you stop. **Do not delete prior entries.**

---

## STOP — read this first

- **Repo path:** `/Users/nadav/Documents/GitHub/super-inverters-game/`
- **The Claude default cwd `/Users/nadav/Documents/Claude/Super Inverters Reloaded/` is EMPTY.** The actual code is in the path above. `cd` there before doing anything.
- **Active branch:** `Multiplayer` (tracked to `origin/Multiplayer`, currently clean).
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
5. **Confirm scope with the user before installing a networking package** or making sweeping changes — this branch is exploratory, not production.
6. **Keep edits minimal and on-branch.** Don't merge to master. Don't force-push.
7. **Last action of every session:** append an entry to "Update log" below (newest at the top of the log). Even if you accomplished nothing, log what you tried and what blocked you. Future-you needs the negative results too.
8. **If this file gets long (>300 lines):** compress old log entries into a single "Older history" summary block at the bottom, but keep the last ~10 entries verbatim.

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

<!-- Newest entries on top. Append above the previous entry; never delete history. -->

## 2026-05-07 — Slice 5 phases 2b + 2c (networked paint, death, level reload)

**Agent session goal:** Resume Slice 5 from yesterday's phase 2a stop and land the two remaining gameplay-sync features so a multiplayer round is playable end-to-end.

**What landed (all committed to `Multiplayer`, pushed by user at end of session):**

- **`f323e5a7` — Slice 5 phase 2c: networked death + level reload.** Three changes:
  - `PlayerManager.OnTriggerExit2D` — gates the `_gameManager.PlayerKilled` call by `photonView.IsMine` in a Photon room. Without this each peer's view of the same player independently detects the off-screen trigger and `PlayerKilled` would fire twice.
  - `GameManager` is now `MonoBehaviourPun`. `PlayerKilled` normalizes the player name (strips `"(Clone)"` suffix from `PhotonNetwork.Instantiate`) and broadcasts via `RPCPlayerKilled` with `RpcTarget.AllViaServer` (ordered delivery). Each peer's RPC handler runs the existing local death/score/reload logic.
  - `waitThenReloadGame` was originally master-only `PhotonNetwork.LoadLevel` + auto-sync to joiner. **That didn't work** — see fix in `3606c5cb` below.
- **`0ba492e2` — Add PhotonView to `Assets/Prefabs/Game.prefab`** so `GameManager.photonView` resolves for the RPC calls. User added the component in Unity before this commit; the diff is mostly the Unity 2020 prefab format upgrade (`serializedVersion: 5` → `6`, `m_PrefabParentObject` → `m_CorrespondingSourceObject`) that auto-fired when the prefab was opened.
- **`d078bf32` — Slice 5 phase 2b: networked platform paint via RPC.**
  - `GameManager.AssignPlatformNetworkIds` runs at `Start`: sorts every `PlatformManager` by initial position (x → y → InstanceID) and assigns sequential ids. Both peers run the same sort on the same scene → platform N is the same physical platform on every peer.
  - `GameManager.BroadcastPaintPlatform` — `photonView.RPC` with `RpcTarget.Others` (NOT `OthersBuffered`; buffered RPCs would replay onto the fresh scene after a `PhotonNetwork.LoadLevel` reload).
  - `GameManager.RPCPaintPlatform` — looks up platform by id, calls `ApplyPaintFromNetwork`.
  - `PlatformManager` — new `networkId` field, `ApplyPaintFromNetwork` method (`SetFramework` + `ChangeLayer`, no re-broadcast), and a broadcast call from `UpdateHit`'s threshold path. Per-peer `num_lives` tracking is fine for default `init_num_lives=1`.
- **`3606c5cb` — Slice 5 phase 2c fixes: joiner-reload + life-decrement.** Two bugs surfaced in playtest:
  - `PhotonNetwork.LoadLevel` only reloaded the **master**, not the joiner. PUN's `AutomaticallySyncScene` triggers off a property-changed event on the room property `curScn`; reloading the same scene name doesn't change the property, so joiners never get the trigger. Replaced `PhotonNetwork.LoadLevel` with `SceneManager.LoadScene` on every peer — since the kill RPC is `AllViaServer` (ordered), all peers' coroutines start within RTT and reload within a frame of each other.
  - `GameState.initializeScores` iterated `GameObject.FindGameObjectsWithTag("player")` which returned **zero in `level_1-multiplayer`** (the static `BlackPlayer`/`WhitePlayer` GameObjects were deleted from the scene during phase 2a setup so the spawner could `PhotonNetwork.Instantiate` them at runtime). With an empty `players` array, `ScoreKeeper._scores` got no entries, and every `decreaseScore` call was a no-op (`ContainsKey` returned false). Hardcoded the seed to `"BlackPlayer"` and `"WhitePlayer"` unconditionally.

**Things confirmed working in playtest (one Editor + one ParrelSync clone):**

- ✅ Both peers transition from lobby (`Multiplayer.unity`) → level scene (`level_1-multiplayer.unity`)
- ✅ Both peers spawn at correct colors via the rejoin-safe color claim (`myFramework` player property)
- ✅ Movement, aim, shooting all networked, near-real-time on both screens
- ✅ Platform paint syncs: when peer A shoots a platform, peer B sees the color change
- ✅ Player falls off screen → both editors reload simultaneously after ~3s → both players respawn
- ✅ Life count decrements on death; presumably end-game triggers when one peer hits 0 lives (not deeply re-tested in this session)

**Known bugs left for next session (NOT FIXED):**

- **Phase 2d — visual shot ghosts on remote.** When peer A shoots, peer B doesn't see the shot fly. Paint sync still works (so the platform recolor appears on both) but the projectile is invisible on the non-shooter side. Plan: RPC `(startPos, velocity, framework)` from the shooter; receiver instantiates a *ghost* shot prefab variant that flies and self-destructs visually but doesn't process paint (paint is already handled by the shooter's collision + the existing paint RPC).
- **Doublejump sprite pivot mismatch.** Cosmetic art polish, deferred since 2026-05-06.
- **Slight feet-on-ground hover/sink at jump-land transitions.** Cosmetic, deferred.
- **`Game` GameObject in `level_1-multiplayer.unity` is baked (not a prefab instance).** I had the user manually add a `PhotonView` to that scene's `Game` GameObject because the prefab→scene linkage was severed. If you copy the level to another multiplayer variant (e.g. `level_2-multiplayer`), you'll need to repeat that step. Worth fixing later by converting Game to a real prefab instance in every multiplayer scene.
- **Two consecutive merge commits** keep landing on `Multiplayer` whenever `claude/slice-5` gets merged in. Functional but log is noisier than necessary. Could squash later.

**State left behind:**

- Branch `Multiplayer` ends at merge commit `4210123d`, plus the scene change for the `PhotonView` add on `level_1-multiplayer.unity`'s `Game` GameObject (committed by user as part of the session push). All pushed to origin/Multiplayer.
- `claude/slice-5` in `.claude/worktrees/suspicious-noyce-2a5242/` at `3606c5cb`, fully merged.
- `level_1-multiplayer.unity` has the `Game` GameObject with `PhotonView` attached directly (since it's not a prefab instance). Spawn positions tuned to actual platforms.
- All gizmos / inspector configuration unchanged from yesterday.

**What's blocked or unclear:**

- Edge case: if a player runs out of lives mid-multiplayer, does `endGame` fire correctly on both peers? `endGame` runs locally in `DoPlayerKilled` when `hasNoLives` is true; both peers reach the same state via the RPC, so should work. Not deeply tested.
- Edge case: rejoin during an in-flight round. The color-rejoin fix from 2026-05-06 handles initial color, but the rejoiner doesn't see the platform paint state that has happened so far (RPCs aren't buffered — see phase 2b commit message). For our friends-only no-reconnect spec this is acceptable.

**Next agent should:**

1. **Acknowledge context-warning convention in first message** (per user's auto-memory at `~/.claude/projects/-Users-nadav-Documents-GitHub-super-inverters-game/memory/`).
2. **Start Slice 5 phase 2d** — visual shot ghosts on remote peers. Shooter's `PlayerManager.shoot` (or `ShotFactory.MakeObject`) needs to RPC the shot's spawn parameters to other peers, who instantiate a ghost shot that flies visually but does NOT process paint collisions (paint is already broadcast separately by phase 2b). Easiest design: add a `[SerializeField] bool isGhost` to `ShotView`; ghosts skip the `PlatformShotSensor`-triggered `UpdateHit` call.
3. **After 2d**, Slice 5 is end-to-end complete. Optional follow-ups: clean up merge-commit noise on `Multiplayer`, prune stale `claude/*` worktree branches (verify none have unmerged work first), compress this `AGENT_CONTEXT.md` if it gets over 300 lines again.

---

### 2026-05-06 — Multi-bug pass + Slice 5 phase 2a (lobby→level scene transition)

**Agent session goal:** Resume after Slice 4. User flagged a hover-after-landing visual; that opened a multi-hour pass through several player-mechanics bugs, then started Slice 5 of the integration plan.

**What got fixed (one commit each, all on `Multiplayer`, all pushed):**
- **`1ab6cab2` — Fix post-landing sprite hover by aligning player sprite pivots.** (Landed via master → merged into Multiplayer.) Idle and doublejump pivots had drifted from the jump/land convention; standardized all four to `{0.375, 0.61}` and flipped doublejump alignment from Center (0) to Custom (9) so the pivot is actually used. 222 sprite `.meta` files. Doublejump still has visible mismatch because the doublejump *art* (curled legs) doesn't fit the standardized pivot; deferred as art polish.
- **`cabe644c` — Fix player snapping back to spawn-side facing on aim release.** `BlackPlayer.prefab` and `WhitePlayer.prefab` were created in Slice 2 with `defaultAimToMove=1` on the PS4Controller (Player.prefab had `=0`). With no joystick attached, PS4Controller.Update was forcing aim to `lastNonZeroFacingDirection` (initialized to face the screen center based on spawn x), overriding KeyboardController on key release. Restored to 0 on both prefabs. Bug was visible in single-player level_1 too because those scenes use these prefabs (level_1 has 11 BlackPlayer + 10 WhitePlayer refs).
- **`3770ecd8` — Fix standing-still shoot direction + keyboard diagonal aim animations.** Two fixes in `PlayerManager`:
  1. `PlayerManager.shoot` now falls back to `_playerView.facingLeft ? Vector2.left : Vector2.right` when `direction == Vector2.zero`. Previously, standing still + Shift → `GetAngle((0,0)) == 0` → shot fired `Vector2.right` regardless of facing.
  2. `_playerView.vertical_dir` is now `atan2(y, |x|) / (π/2)` instead of raw `shootingDirection.y`. The animator's `animGetDirectionIndex` buckets at `±0.4` and `±0.85`; with the raw `y`, keyboard W+A produced `y=1` (always "up" bucket) so the up_diag/down_diag sprites never played. Using verticalness-angle, W+A → 0.5 → up_diag bucket. Single principled fix; helps analog stick too.
- **`605f6419` — Crosshair: idle-fallback to facing direction, white tint for black player.**
  1. `PlayerView.changeCrosshairDirection` falls back to `facingLeft ? left : right` when input is zero so the crosshair doesn't sit at the player's center hidden inside the body sprite.
  2. Black player's crosshair switched from `Color.black` to `Color.white` (visibility against gray background and the black player sprite). Important detail: needed to set `_crosshair_spriteRenderer.color` (per-instance tint), not `material.color` — `material.color * .color` multiplies, and Black's prefab has the SpriteRenderer's `m_Color` baked to `(0,0,0,1)`, which would clamp any material tint back to black.
- **`9f0ff3fe` — Fix color assignment when host disconnects and rejoins.** `MultiplayerSpawner` was `IsMasterClient ? hostColor : Opposite(hostColor)`; when the original host disconnected and rejoined, they were no longer master, so they took `Opposite(hostColor)` = same color as the joiner who'd taken Opposite originally. Now each peer claims its color via a `myFramework` player custom property at spawn; new joiners scan `PhotonNetwork.PlayerList` for an existing claim and take whichever color isn't claimed. Falls back to the old hostColor heuristic only when no one's claimed yet.

**Slice 5 phase 2a — lobby→level scene transition (started, partially landed):**
- User created `Assets/Scenes/level_1-multiplayer.unity` (a copy of `level_1.unity` with the static BlackPlayer/WhitePlayer GameObjects deleted, Bootstrap GameObject from Multiplayer scene pasted in, registered in Build Settings). Committed as user's `a27a74ba`.
- Code-side commits on `claude/slice-5` (merged into `Multiplayer` at merge commits `35970c53` then `9fc88873`):
  - `89bc03a4` — `MultiplayerBootstrap.TryLoadGameScene`: when room is full and we're the master client and not already in the game scene, calls `PhotonNetwork.LoadLevel(gameSceneName)` (default `"level_1-multiplayer"`). Joiner gets the LoadLevel auto-synced by PUN. `MultiplayerSpawner` gains a `targetSceneName` SerializeField guard (default `"level_1-multiplayer"`) so the same Bootstrap (copied between lobby and level scenes) only spawns in the game scene. Spawn fires from `Start` in addition to `OnJoinedRoom`, since `OnJoinedRoom` doesn't fire after a `PhotonNetwork.LoadLevel` transition.
  - `d210c82b` + `1fc02f57` — `OnDrawGizmos` for `MultiplayerSpawner`: solid colored sphere + yellow wire ring + downward drop line + `Handles.Label` with "BLACK SPAWN" / "WHITE SPAWN" so the user can see in the Scene view (with Gizmos toggled on) where players will spawn before pressing Play, and tune positions to land on platforms.

**Things tested and confirmed working in `level_1-multiplayer`:**
- Lobby → level scene transition fires when both peers connect.
- Both peers spawn at correct (and distinct) colors via the new `myFramework` claim mechanism.
- Movement (WASD), jump (Space), shoot (Shift) all responsive on the local player. Animations and crosshair update correctly. Diagonal aim now triggers up_diag/down_diag sprite buckets.

**Known issues left for next session (NOT YET FIXED):**
- **Cross-peer desync after a player dies.** Currently `GameManager.waitThenReloadGame()` calls `SceneManager.LoadScene(gameSceneName)` — local-only. When one peer's player falls off-screen, only that peer reloads; the other peer continues in the old scene and sync breaks. Plus both peers' physics independently detect the death so `PlayerKilled` fires twice. **This is Slice 5 phase 2c work** (planned next):
  - Gate `PlayerManager.OnTriggerExit2D`'s death detection by `photonView.IsMine` so each death is reported only once
  - `GameManager.PlayerKilled` becomes an RPC (need to add a PhotonView to the Game GameObject in `level_1-multiplayer`)
  - `SceneManager.LoadScene` swap for `PhotonNetwork.LoadLevel` (master triggers, joiner auto-syncs)
- **Networked paint not yet implemented** (Slice 5 phase 2b). Each peer locally simulates shot-platform collisions and locally paints. Other peer doesn't see the paint event. So platforms diverge in color across peers. Plan: when a paint event happens on the shooter's machine, RPC it via a per-platform PhotonView (or a singleton NetworkPaint with platform ID).
- **Visual shot ghosts on remote** (Slice 5 phase 2d) not implemented. Joiner doesn't see host's shots flying. Plan: RPC `(startPos, velocity, framework)` so each peer instantiates a visual-only ghost shot.
- **Doublejump sprite pivot** still slightly off (legs-curled art doesn't fit the standardized pivot). Cosmetic art-polish, deferred.
- **Hover/sink at edges of jump/land transitions** still slightly off in some states — same per-state pivot tuning issue. Cosmetic, deferred.
- **Two consecutive merge commits on Multiplayer** (`35970c53` and `9fc88873`) from merging `claude/slice-5` twice. Functional but ugly. Could squash later if the user cares.

**State left behind:**
- Branch `Multiplayer` at merge commit `9fc88873`, working tree clean, all changes committed and pushed to origin/Multiplayer (user pushes manually per `feedback_user_handles_pushes.md`).
- `claude/slice-5` branch (in `.claude/worktrees/suspicious-noyce-2a5242/`) at `1fc02f57`, fully merged into Multiplayer. Can be deleted next session.
- Other claude/* worktree branches still around from prior sessions (cool-panini, happy-keller, etc.) — none have unmerged work as far as I know; could prune later.
- `level_1-multiplayer.unity` exists with: `Bootstrap` (with all three Multiplayer components configured), `Game` (GameManager etc.), the level's standard platforms/floor/menus, no static player GameObjects. Build Settings checked.
- `Multiplayer.unity` is now the lobby scene. Bootstrap there has its `gameSceneName` set to `level_1-multiplayer` so it auto-transitions both peers to the level when the room fills.
- Unity user's main worktree at `/Users/nadav/Documents/GitHub/super-inverters-game/` had the Unity Editor open during most of the session. All my code edits via the Edit tool went into the main worktree path (not my `suspicious-noyce-2a5242` worktree) — caused a couple of merge complications when I forgot which worktree I was in. Future agent: use `git -C` or be explicit about which worktree you're editing.

**What's blocked or unclear:**
- Spawn position tuning: User has gizmos visible in Scene view but may still need to drag the Black/White Spawn Position values in the Inspector to land on actual platforms in `level_1-multiplayer`. White spawn at `(3, 1)` was reported to fall (no platform there).
- Whether to do networked paint (2b) or networked death (2c) first. I recommended 2c first (makes the game playable across rounds). User agreed. Hadn't started yet at session end.

**Next agent should:**
1. **Acknowledge context-warning convention in first message** (per Working agreement #2 — though this convention now lives in user's auto-memory at `~/.claude/projects/-Users-nadav-Documents-GitHub-super-inverters-game/memory/`).
2. **Start Slice 5 phase 2c (networked death + level reload).** Three sub-changes:
   a. `PlayerManager.OnTriggerExit2D` — gate the `_gameManager.PlayerKilled` call by `photonView.IsMine` (only the dying player's owner reports the death; remote peers don't double-report).
   b. `GameManager.PlayerKilled` — when in a Photon room, RPC the kill to all peers via a new PhotonView on the Game GameObject. The RPC handler runs the existing local death/score logic on every peer. Add the PhotonView component to `Game.prefab` (or to the Game GameObject in `level_1-multiplayer.unity` if you want to keep prefab clean).
   c. `GameManager.waitThenReloadGame` — when in a Photon room AND we're the master client, call `PhotonNetwork.LoadLevel(gameSceneName)` instead of `SceneManager.LoadScene`. Joiner gets it auto-synced. Single-player keeps using `SceneManager.LoadScene`.
3. **Then phase 2b (networked paint).** Per-platform PhotonView is the most-Photon-native approach but invasive (every platform prefab needs a PhotonView, scene must be saved with PUN's setup wizard to allocate scene ViewIDs). Singleton NetworkPaint with platform-ID-keyed RPCs is simpler but needs a stable platform identifier across peers — use scene-load order (`FindObjectsOfType<PlatformManager>().IndexOf(platform)` with consistent ordering) or hash of position.
4. **Then phase 2d (visual shot ghosts on remote).** RPC `(startPos, velocity, framework)` from shooter; receiver Instantiate's a ghost-only shot prefab variant that flies but doesn't process paint (paint already handled by the shooter's own collision + RPC).
5. **Re-test the rejoin flow** after phase 2c since the death-triggered scene reload may interact with the rejoin logic in `MultiplayerSpawner.PickMyColor`.
6. **Compress old log entries.** This file is now well past 300 lines; keep the last ~6 entries verbatim and fold the rest into an "Older history" block at the bottom.

---

### 2026-05-03 — Slice 4 unblocked; networked input + transform sync working two-peer

**Agent session goal:** Resolve the in-sky bug from yesterday's Slice 4 entry and finish multiplayer controls.

**What got resolved:**
- **Diagnostic confirmed `OnPhotonInstantiate` fires correctly** and `Rigidbody2D.simulated = false` is being applied on remotes. Yesterday's hypothesis (callback not firing) was wrong; logs showed `IsMine=False rb=True bodyType=Kinematic simulated=False` exactly when expected. The body still drifted to Y=1097 anyway.
- **Real cause: PhotonTransformView's prediction logic was misbehaving on the host side.** Hard to be certain whether it was stream alignment with PhotonInputView's IPunObservable or something inside PhotonTransformView's `m_NetworkPosition += m_Direction * lag` extrapolation, but the symptom was reproducible: joiner reports Y=2.09, host's view drifts to Y=46–1097 over time, and corrects only when the joiner moves enough to send a fresh "On Change" packet.
- **Fix: replaced PhotonTransformView entirely with position handling inside PhotonInputView.** PhotonInputView now owns the whole stream (Vec2 aim + 4 button bools + Vec3 position), and on the remote it does `transform.position = Vector3.Lerp(transform.position, _remotePosition, Time.deltaTime * 15f)` in `Update`. Single IPunObservable, no alignment risk, no built-in prediction to fight. **PhotonTransformView component removed from both prefabs.**
- **PhotonView Synchronization changed from `Unreliable On Change` (3) to `Unreliable` (2)** on both prefabs. With "On Change", packets were suppressed while the joiner was idle, so newly-spawned remotes had no position data. With plain "Unreliable", every serialization tick streams position regardless.
- **Inadvertent prefab body-type modification reverted again.** Same pattern as yesterday: an attempt to set the prefab Rigidbody2D to Kinematic via Unity Editor while the ParrelSync clone was open produced ParrelSync's "asset save blocked" dialog but the change had already saved on BlackPlayer. Reverted in YAML to Dynamic. Reminder for future agents: prefab edits must come from the **host** Unity Editor only — see Working Agreement #4.

**New: multiplayer-only key layout (WASD / Space / Shift):**
- New `Assets/scripts/Controllers/MultiplayerKeyboardController.cs : Controller` — uses `Input.GetKey` / `GetKeyDown` directly (no axis-name indirection). WASD for movement, Space for jump (or "get down" through platform if S is held), Shift (Left or Right) for fire, Esc for pause.
- Default-disabled on the prefab (via Inspector checkbox) so single-player co-op is untouched.
- `PhotonInputView.OnPhotonInstantiate` enables `MultiplayerKeyboardController` and disables `KeyboardController + PS4Controller` on the local-mine instance. On remotes, all three are disabled (NetworkController drives via replicated input).
- PhotonInputView also samples `MultiplayerKeyboardController` in `LateUpdate` (in addition to the existing — but now disabled in MP — `KeyboardController` block).

**Slice 4 validation outcome:** Confirmed two-peer:
- Both players spawn at correct positions (host's local Black at (-3, 1) → falls to floor; joiner's local White at (3, 1) → falls to floor).
- Host's view of WhitePlayer Y matches joiner's local Y (within Lerp catch-up window) once joiner is stationary.
- Movement (WASD) and jump (Space) on each side moves only that side's owned player — IsMine gating works.
- Walking-direction animation flips correctly with the axis-snap fix from yesterday (`_localAim` snapped to discrete -1 / 0 / +1, deadzone 0.2).
- One transient `wss://` Photon disconnect during retesting (per Slice 3's log, this is a known transient flake; cleared on next Play attempt).
- **Shooting still NullRefs** — `PlayerManager.shoot` line 311 (`_gameManager.SpawnShot`) throws because `Multiplayer.unity` has no GameManager. Confirmed harmless; deferred to Slice 5.

**State left behind:**
- Branch `Multiplayer` on user's main worktree at `/Users/nadav/Documents/GitHub/super-inverters-game/`. Working tree dirty there (NOT committed):
  - New: `Assets/scripts/Multiplayer/PhotonInputView.cs` (+ `.meta`)
  - New: `Assets/scripts/Controllers/NetworkController.cs` (+ `.meta`, `executionOrder: -100`)
  - New: `Assets/scripts/Controllers/MultiplayerKeyboardController.cs` (+ `.meta`)
  - Modified: `Assets/Resources/BlackPlayer.prefab` — 3 new components added (PhotonInputView, NetworkController, MultiplayerKeyboardController), PhotonView Synchronization changed to Unreliable, root Rigidbody2D body type back to Dynamic via YAML revert. PhotonTransformView removed (was added during the day, removed again).
  - Modified: `Assets/Resources/WhitePlayer.prefab` — same 3 components added, PhotonView Synchronization changed to Unreliable, PhotonTransformView removed.
  - Modified: `AGENT_CONTEXT.md` (this entry + yesterday's superseded WIP entry below)
- `PhotonInputView.OnPhotonInstantiate` still has two `Debug.Log` diagnostic lines from yesterday — left in for one more session in case the in-sky bug recurs; should be removed before the Slice 4 commit lands.
- Side-branch state (still local-only, NOT pushed yet — same as yesterday):
  - `master` is 2 commits ahead of `origin/master` — `b1830198 Fixed key` (pre-existing) + `00e91e18 Add CLAUDE.md breadcrumb`.
  - `claude/add-claude-md-pointer` (local-only): one commit `466d5e8f` on top of `origin/Multiplayer` adding CLAUDE.md.
  - `claude/slice-4-network-input` (in `.claude/worktrees/zealous-panini-57f4e8/`): vestigial; no commits, .cs files duplicated. Delete after Slice 4 lands.
- Push commands the user still needs to run when ready (per `feedback_user_handles_pushes.md`):
  ```
  git push origin master
  git push origin claude/add-claude-md-pointer:Multiplayer
  git branch -d claude/add-claude-md-pointer
  cd /Users/nadav/Documents/GitHub/super-inverters-game && git pull --ff-only origin Multiplayer
  ```

**What's blocked or unclear:**
- Why exactly PhotonTransformView misbehaved is still not pinned down — but bypassing it solved the problem, so this is curiosity-only.
- Whether the joiner's local Y settles cleanly on the floor or hovers slightly above (yesterday's reading was Y=2.09 on the joiner with floor at ~-2). Not critical; the visual position looked fine in today's tests.
- Shooting in `Multiplayer.unity` requires either a GameManager in the scene or a guard in `PlayerManager.shoot`. This is the entry-point for Slice 5.

**Next agent should:**
1. **Acknowledge context-warning convention in first message** (per Working agreement #2).
2. **Confirm with user whether to commit + push Slice 4 now** before starting Slice 5. Suggested commit scope (one bundled commit on `Multiplayer`):
   - `Assets/scripts/Multiplayer/PhotonInputView.cs` (+ `.meta`) — drop the two Debug.Log diagnostic lines first.
   - `Assets/scripts/Controllers/NetworkController.cs` (+ `.meta`)
   - `Assets/scripts/Controllers/MultiplayerKeyboardController.cs` (+ `.meta`)
   - `Assets/Resources/BlackPlayer.prefab` + `Assets/Resources/WhitePlayer.prefab`
   - `AGENT_CONTEXT.md`
   Suggested commit message subject: `Slice 4: networked input + transform sync via PhotonInputView`.
3. **Push the parked meta-improvement commits** (master + `claude/add-claude-md-pointer` → Multiplayer) before or after the Slice 4 commit; independent of Slice 4.
4. Delete `claude/slice-4-network-input` branch after Slice 4 commits.
5. Then **start Slice 5: networked spawn/kill events.** Per the original integration plan: `GameManager.SpawnShot`/`SpawnShell` need to use `PhotonNetwork.Instantiate` (or RPCs) so projectiles appear on both peers; `GameManager.PlayerKilled` becomes an RPC; `SceneManager.LoadScene` calls become `PhotonNetwork.LoadLevel`. **Prerequisite:** add a GameManager GameObject to `Multiplayer.unity` (or guard `PlayerManager.shoot`/`PlayerKilled` against null) so shooting stops NullRef'ing.
6. **Compress old log entries.** Doc is well past 500 lines; Working Agreement #8 says to compress when >300. Suggest: keep the last ~10 entries verbatim (this entry, yesterday's WIP, Slice 3, Slice 2, Slice 1, plus the goal-confirmation entries from 2026-04-30) and fold the rest into a single "Older history" block at the bottom.

---

### 2026-05-02 — Slice 4 (networked input + transform sync) WIP, blocked on remote-position drift

**Agent session goal:** Implement Slice 4 — networked input replication + transform sync — fixing the two limitations from Slice 3 (position not replicated, local input drives both players).

**What got built (mostly working):**
- New `Assets/scripts/Multiplayer/PhotonInputView.cs` — `MonoBehaviourPun, IPunObservable, IPunInstantiateMagicCallback`. On the local player (photonView.IsMine), samples KB inputs each LateUpdate and accumulates pressed-since-last-sync button events. Serializes `Vector2 aim` + 4 bools (jump / shoot / getDown / pauseMenu) per OnPhotonSerializeView. On the remote (`!IsMine`), `OnPhotonInstantiate` disables KeyboardController + PS4Controller and (current state of fix) sets the local Rigidbody2D's `bodyType = Kinematic` AND `simulated = false`. Caches Rigidbody/KB/PS4 refs in Awake.
- New `Assets/scripts/Controllers/NetworkController.cs : Controller` — `executionOrder: -100` so its Update runs before PlayerManager.Update. Returns zero/false when `photonView.IsMine` to avoid polluting the local game's controller iteration. On remote, consumes pending button events from PhotonInputView once per network event (so jump() / shoot() / getDown() / pauseMenu() return true for exactly one frame per replicated press) and reads `RemoteAim` for direction values.
- Both prefabs (`Assets/Resources/{Black,White}Player.prefab`) — user added 3 components in Editor: PhotonInputView, NetworkController, PhotonTransformView (Synchronize Position only). Photon View's `Auto Find All` picks up the two IPunObservables (PhotonTransformView + PhotonInputView).
- **Axis-snap fix in PhotonInputView**: `_localAim` is snapped to discrete `-1 / 0 / +1` with a 0.2 deadzone before serializing. Without this, Input.GetAxis decay residuals (e.g. 0.05 mid-release) were sent and Controller.Update normalized them to magnitude-1 on the remote, causing stuck-running animations and ghost movement.
- **PS4 polluting network — fixed by ignoring PS4 in PhotonInputView**: PS4Controller's `defaultAimToMove: 1` (set on both prefabs) forces `aim_direction()` to a spawn-direction default (`(-1, 0)` for White at x=3, `(1, 0)` for Black at x=-3) every frame. That permanently overrode any KB input in the network stream — White was permanently sending "move left." PS4 still drives the LOCAL player via PlayerManager's controller iteration; PhotonInputView just doesn't sample it for the network.
- **Inadvertent prefab body-type change reverted**: at one point during debugging the user attempted to set BlackPlayer's prefab Rigidbody2D body type to Kinematic. ParrelSync's "Asset modifications saving blocked" dialog appeared in the clone, but the change had already saved on the prefab (root Rigidbody2D went `m_BodyType: 0 → 1`). Reverted via direct YAML edit (line 477 of `BlackPlayer.prefab`). Reminder: the safe-to-modify rule for prefabs is "host editor only, never the ParrelSync clone" — see Working Agreement #4.

**Meta improvements made this session (separate from Slice 4, both committed locally, NOT yet pushed):**
- `master` branch: new commit `00e91e18 Add CLAUDE.md breadcrumb pointing to Multiplayer's AGENT_CONTEXT.md`. Auto-loaded by Claude Code in any session on master / a worktree off master, so future agents discover the doc instead of concluding it doesn't exist (which is exactly what happened at the start of this session).
- New local branch `claude/add-claude-md-pointer` (commit `466d5e8f`) on top of origin/Multiplayer adds a thin CLAUDE.md saying "read AGENT_CONTEXT.md end-to-end before doing anything." Designed to fast-forward into Multiplayer when pushed.
- Push commands the user still needs to run (not done yet — agent's shell can't reach Keychain creds, see auto-memory `feedback_user_handles_pushes.md`):
  ```
  git push origin master
  git push origin claude/add-claude-md-pointer:Multiplayer
  git branch -d claude/add-claude-md-pointer
  ```

**What still doesn't work — blocked here:**
- **Host's view of WhitePlayer remains "in the sky" after the latest fix.** Diagnostic snapshot: host-side `Transform.position.y = 62.73`, `Rigidbody2D.bodyType = Kinematic` (confirmed my IsMine path was reached), Photon View `IsMine = False (master)`. Hypothesis was: replicated jump events triggered `PlayerManager.tryToJump` which added upward velocity to the Kinematic body (no gravity to counter, body drifts up indefinitely). Applied fix: also set `_rigidbody.simulated = false` in OnPhotonInstantiate. **User reports the bug still persists** ("white glare is still in the sky"). User has not yet validated whether `simulated = false` is actually being applied at runtime or whether `OnPhotonInstantiate` is even firing on the host's view of remote White.
- **Joiner's local WhitePlayer reads Y=2.09** (slightly above spawn 1) at one diagnostic check. Body is Dynamic with gravity scale 4 — should fall to floor (~-1.5). Possibly mid-jump during test, possibly something else. Not investigated.
- No clean two-peer validation yet — the in-the-sky bug blocks it.

**State left behind:**
- Branch `Multiplayer` on user's main worktree at `/Users/nadav/Documents/GitHub/super-inverters-game/`. Working tree dirty there (NOT committed):
  - New: `Assets/scripts/Multiplayer/PhotonInputView.cs` (+ `.meta`)
  - New: `Assets/scripts/Controllers/NetworkController.cs` (+ `.meta`, `executionOrder: -100`)
  - Modified: `Assets/Resources/BlackPlayer.prefab` — 3 new components added (PhotonInputView, NetworkController, PhotonTransformView); root Rigidbody2D body type back to Dynamic via YAML revert
  - Modified: `Assets/Resources/WhitePlayer.prefab` — same 3 components added
  - Modified: this `AGENT_CONTEXT.md` (this entry)
- Side worktree at `.claude/worktrees/zealous-panini-57f4e8/` on local branch `claude/slice-4-network-input` — has a duplicate copy of the .cs/.meta files, no commits yet on the branch. Effectively redundant with the main worktree's dirty state; can be deleted once Slice 4 commits land on Multiplayer.
- Lingering uncommitted-across-sessions files (same as prior entries): `UserSettings/EditorUserSettings.asset`, older `level_2.unity` / `start_scene_2.unity` / lighting outputs, SceneTemplate&Timeline settings. User's call.

**What's blocked or unclear:**
- Why does `simulated = false` on the host's remote-White Rigidbody2D not stop the Y drift? Two main hypotheses for next session: (a) `OnPhotonInstantiate` doesn't fire / `_rigidbody` is null at that point so the line is a no-op; (b) PhotonTransformView is doing something unexpected with position writes. Next session should add a Debug.Log inside OnPhotonInstantiate showing IsMine + bodyType + simulated to settle (a), and runtime-inspect PhotonTransformView's `m_NetworkPosition` to settle (b).
- Whether the same bug exists on the remote Black on the joiner side. Not tested.
- Whether `PlayerManager.shoot` NullRefs (Slice 3 carry-over from missing GameManager in Multiplayer.unity) interact with anything Slice 4 added. Not investigated.

**Next agent should:**
1. **Acknowledge context-warning convention in first message** (per Working agreement #2).
2. **Confirm with user whether the in-sky bug still reproduces** (or if they tried more in their head overnight).
3. **Add a Debug.Log inside `PhotonInputView.OnPhotonInstantiate`** printing `[PhotonInputView] IsMine={photonView.IsMine} rb={_rigidbody!=null} bodyType={_rigidbody?.bodyType} simulated={_rigidbody?.simulated}`. Have user run two-peer; check what gets printed for the remote White instance on the host. This will narrow down whether the callback fires and what state the Rigidbody2D ends up in.
4. If the callback isn't firing or `_rigidbody` is null at that point — try moving the rigidbody fetch + body-type change into a deferred coroutine started from OnPhotonInstantiate, or use `info.Sender` / `photonView.transform` to fetch fresh.
5. If the callback IS firing and simulated=false IS applied but Y still drifts — investigate PhotonTransformView's behavior (is it actually receiving updates? what's `m_NetworkPosition`?). Could also be that the `Auto Find All` runtime population isn't ordering observables consistently between host and joiner, mis-aligning the stream so PhotonTransformView reads bool fields as a Vector3 — would explain garbage Y values. Quickest test: switch to Manual on the PhotonView with explicit `[PhotonTransformView, PhotonInputView]` ordering.
6. After Slice 4 lands cleanly, commit a single bundled commit on Multiplayer including: the two new scripts (.cs + .meta), prefab edits, this AGENT_CONTEXT entry. Then push (user runs the push). Compress the now-2-of-3 same-day 2026-05-02 entries if convenient — the doc is at ~490 lines, well past the 300-line compression threshold in Working Agreement #8.
7. Push the meta-improvement commits (`master`, `claude/add-claude-md-pointer:Multiplayer`) — independent of Slice 4, push order doesn't matter.
8. Delete `claude/slice-4-network-input` once Slice 4 is committed elsewhere.
9. **WebGL still parked.** Don't burn another session on it.

---

### 2026-05-02 — Slice 3 (link-share UI) implemented and validated two-peer

**Agent session goal:** Build the host-side share-link UI: a small panel with the room URL and a Copy button that puts it on the OS clipboard.

**What got built:**
- New `Assets/scripts/Multiplayer/LinkShareUI.cs` — `MonoBehaviourPunCallbacks` that builds its UI programmatically in `Awake` (no scene Canvas/wiring needed). Creates a screen-space-overlay Canvas with a `SharePanel` (white background, ~900x80 anchored top-center), a "Share:" label, a URL `Text` showing `BuildShareUrl(roomName)`, and a blue Copy button. On `OnJoinedRoom`, if `IsMasterClient`, populates URL and shows the panel; on `OnPlayerEnteredRoom` it hides the panel once the room is full (no point advertising further). Joiner peers create the canvas but never show the panel. Copy button writes to `GUIUtility.systemCopyBuffer`, swaps button label to "Copied!" for 1.5s.
- Iterations during validation:
  - Initial dark transparent panel was invisible against the dark game scene → flipped to opaque white panel with dark text.
  - All `Text` components had no font: `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` returned null on this Unity 2020.3.48 + macOS setup. Added a `GetUIFont()` helper that tries `LegacyRuntime.ttf` → `Arial.ttf` → `Font.CreateDynamicFontFromOSFont("Arial", 16)` → logs warning if all fail. Fallback chain resolved the missing-text problem.
- `MultiplayerBootstrap.cs` extension: `editorRoomOverride` now passes its trimmed value through `ReadRoomFromUrl` first, falling back to raw input. So pasting `(editor) ?room=ABCDEF` (or any future real WebGL share URL) into the clone's override field extracts `ABCDEF` correctly. Smaller failure mode if the user pastes a string that looks like a URL but isn't quite one — they get the trimmed string used as the room code, which then fails on `JoinRoom` with a clear "join failed" log.
- Scene change: `Multiplayer.unity` Bootstrap GameObject now also has the `LinkShareUI` component.

**Slice 3 validation outcome:**
- Single-editor host: share bar appears at top of Game view as expected; clicking Copy logs `Copied to clipboard: (editor) ?room=XXXXXX` and clipboard receives the same string.
- Two-peer (ParrelSync): host pastes share string into clone's `editorRoomOverride`; clone's override-parser logs `Editor override: will join room 'XXXXXX'`; clone connects, both players spawn. Host's share bar auto-hides once clone joins. ✓
- One transient Photon `wss://` disconnect during retesting (not config-related, cleared on the next Play). Worth noting: PUN in Editor uses WebSocket transport on this setup, not the older UDP path.

**Out-of-scope problems the user surfaced (good to flag for Slice 4):**
- **Position not replicated.** PhotonView on the prefab makes PUN aware of the spawned object, but doesn't sync its transform. Slice 4 needs a `PhotonTransformView` (or `IPunObservable` on a custom controller) to stream position/rotation.
- **Local input drives both players.** Both prefabs have `KeyboardController` attached; each peer's local hardware moves both Players locally. Slice 4 fixes this by gating input by `photonView.IsMine` — the existing `Controller` polymorphism is the seam (per AGENT_CONTEXT's "Architecture seams" section).
- **`PlayerManager.shoot` NullRefs in `Multiplayer.unity`** because no `GameManager` is in this scene. Confirmed harmless if you don't shoot; Slice 4 or 5 will need either a GameManager in the scene or a guard in PlayerManager.

**State left behind:**
- Branch `Multiplayer`. Slice 2 commit `d882fd95` is on `origin/Multiplayer` (pushed). Slice 3 not yet committed at time of this log write — about to be.
- Working tree dirty for the Slice 3 commit:
  - New: `Assets/scripts/Multiplayer/LinkShareUI.cs` (+ `.meta`)
  - Modified: `Assets/scripts/Multiplayer/MultiplayerBootstrap.cs` (tolerant override parsing)
  - Modified: `Assets/Scenes/Multiplayer.unity` (LinkShareUI on Bootstrap)
  - Modified: `AGENT_CONTEXT.md` (this entry)
- Lingering working-tree carryover from prior sessions still not committed: `UserSettings/EditorUserSettings.asset` and the older `level_2.unity` / `start_scene_2.unity` / lighting outputs / SceneTemplate&Timeline settings. User has been deliberately leaving these uncommitted across sessions; do not include in this commit.

**Next agent should:**
1. **Acknowledge context-warning convention in first message** (per Working agreement #2).
2. Confirm with user that the Slice 3 commit landed and was pushed.
3. Start **Slice 4 (networked input)** per the revised plan: add `Assets/scripts/Controllers/NetworkController : Controller` that returns inputs received over the network. On the local Player (where `photonView.IsMine == true`), the existing `KeyboardController` and `PS4Controller` keep working AND their inputs are streamed via `PhotonView.RPC` or `IPunObservable` on a small companion script. On the remote Player (`photonView.IsMine == false`), disable the local controllers (or make them no-op) and let `NetworkController` provide inputs from the replicated stream. Use the `Controller` polymorphism — per "Architecture seams," do NOT modify `PlayerManager`. Probably the smallest path is: add `PhotonTransformView` to the prefabs for transform sync (PUN built-in component, drag in Editor), and a `PhotonInputView : MonoBehaviourPun, IPunObservable` that owns input replication. Then `NetworkController.Update` reads from `PhotonInputView`'s most-recent received inputs.
4. **WebGL still parked.** Don't burn another session on it without searching Photon forums for the `b163`/`nullFunc_vi` signature first.

---

### 2026-05-01 — Slice 2 validated host-side and committed; pivoted to BlackPlayer/WhitePlayer prefabs; LMDB scare from concurrent Editors

**Agent session goal:** Push the 3 local commits, commit the Multiplayer scene rename, then implement and validate Slice 2 (color assignment).

**What got done:**
- **Committed scene rename** `Multiplayer .unity` → `Multiplayer.unity` (commit `f7d86a6a`).
- **User pushed all 4 local commits** to `origin/Multiplayer`. Origin and local are in sync as of session start of Slice 2 work.
- **Slice 2 first attempt (reverted):** moved `Player.prefab` → `Resources/`, wrote `NetworkPlayerInit.cs` (an `IPunInstantiateMagicCallback` to set framework via PUN instantiation data), wrote `MultiplayerSpawner.cs` to instantiate `Player.prefab` with framework as data, extended `MultiplayerBootstrap.cs` with `[SerializeField] Framework hostColor` field that's stored as a custom room property on `CreateRoom`.
- **Discovered `Player.prefab` is dead.** Grepped all scenes by GUID — `Player.prefab` (guid `c808ace7...`) has **0 references anywhere**. The actual gameplay prefabs are `Assets/Prefabs/players/BlackPlayer.prefab` (guid `1c1fbaec...`) and `WhitePlayer.prefab` (guid `66aec441...`), used 10+ times each across `level_1..5`, `start_scene_2`, `level_test`, `level_skeleton`, `level_ori`. The prior agent's plan to use `Player.prefab` as a generic template was based on an unverified assumption.
- **LMDB asset DB scare:** during the prefab move, Unity console flooded with `Assertion failed on expression: 'errors == MDB_SUCCESS || errors == MDB_NOTFOUND'`, `MDB_MAP_RESIZED`, and `Asset database transaction committed twice!` — caused by ParrelSync clone Editor still being open from prior session, sharing `Library/` via symlink. **Recovery: close clone, fully quit Unity, reopen.** Errors cleared on restart. Only remaining error was a harmless stale "Recent Scenes" menu entry pointing at the old `Multiplayer .unity` filename.
- **Slice 2 pivot (current state):** reverted `Player.prefab` back to `Assets/Prefabs/`. Moved `BlackPlayer.prefab` + `WhitePlayer.prefab` (and `.meta` files) to `Assets/Resources/`. Deleted `NetworkPlayerInit.cs` (no longer needed — frameworks are baked into the separate prefabs). Rewrote `MultiplayerSpawner.cs` to call `PhotonNetwork.Instantiate("BlackPlayer" or "WhitePlayer", ...)` based on derived color (master takes `hostColor`, joiner takes opposite). `MultiplayerBootstrap.cs` extension is unchanged.
- **Plan revision per user:** the original Slice 5 (link-share UI) is being promoted earlier. New agreed sequence: Slice 2 (colors) → Slice 3 (link-share UI) → Slice 4 (networked input) → Slice 5 (spawn/kill events). Reason: link-share UI is the highest-leverage user-facing feature; currently the room URL is only logged to console.
- **Memory + AGENT_CONTEXT additions:** added a feedback memory at `~/.claude/projects/.../memory/feedback_context_limit_warning.md` recording the user's preference to be warned 3-5 messages before context exhaustion, and added items #2 (context warning) and #4 (one-Editor-at-a-time during shell file moves) to the Working Agreement above.

**State left behind:**
- Branch `Multiplayer`, **synced with `origin/Multiplayer`** at last push (commit `f7d86a6a`).
- Working tree dirty (Slice 2 NOT yet committed):
  - Renamed (staged): `Assets/Prefabs/players/{BlackPlayer,WhitePlayer}.prefab` + `.meta` → `Assets/Resources/`
  - Modified (unstaged): `Assets/scripts/Multiplayer/MultiplayerBootstrap.cs` (added `hostColor` field + custom room prop)
  - New (untracked): `Assets/scripts/Multiplayer/MultiplayerSpawner.cs` + `.meta`
  - Also lingering from older sessions and not committed: `Assets/Scenes/level_2.unity` and `start_scene_2.unity` modifications, `*Settings.lighting` outputs, `UserSettings/EditorUserSettings.asset`, `ProjectSettings/{ProjectSettings.asset,SceneTemplateSettings.json,TimelineSettings.asset}`. Same status as prior session — left for user to decide.
- **Editor work completed this session:**
  1. PhotonView added to `Assets/Resources/BlackPlayer.prefab` and `WhitePlayer.prefab`.
  2. `Multiplayer.unity` scene now has: `Bootstrap` GameObject (with `MultiplayerBootstrap` + `MultiplayerSpawner`), `Main Camera` prefab, `floor` prefab at position (0, -2, 0). User chose to keep the floor's existing Kinematic Rigidbody2D (default for the prefab — Static would have been a needless override).
- **Slice 2 validation outcome:**
  - Single-editor Play (host): logs `Connecting → Connected to master (eu) → Hosting room 'XXXXXX' as BLACK → Spawned local 'BlackPlayer' as BLACK at (-3, 1) → Joined as actor #1 (1/2)`. Black player spawned and landed on floor. ✓
  - Two-peer (ParrelSync clone) — host editor saw both players with correct colors after clone joined. **The clone-side Game view appeared empty initially** — root cause was the clone's Editor having loaded `Multiplayer.unity` in memory before the host added Camera + floor; ParrelSync symlinks `Assets/` so disk state was current, but the in-memory scene was stale. User chose to delete the existing clone and create a fresh one rather than reload the scene file (safer-over-faster preference for Unity 2020.3 — see memory `feedback_unity_2020_safer_path.md`). Pending verification of the fresh clone showing both players.
- WebGL still parked/broken (per prior sessions). Not touched.

**What's blocked or unclear:**
- Whether `BlackPlayer.prefab` and `WhitePlayer.prefab` are structurally identical except for `player_framework` and color settings — didn't verify exhaustively. If they differ in scripts/colliders, Slice 3+ networked input may need different handling per prefab.
- Whether `PlayerManager.Awake`'s `GetComponentInParent<GameManager>()` returning null in `Multiplayer.unity` (no GameManager in scene) will cause issues beyond shooting/pause/death paths. Movement-only validation should be safe.
- Whether to delete the now-orphaned `Assets/Prefabs/Player.prefab` (it's unused everywhere). Recommend leaving it for now — it's outside the slice's scope.

**Next agent should:**
1. **Acknowledge context-warning convention in first message** (per Working agreement #2).
2. Confirm with user that the fresh-clone two-peer test now shows both players correctly in the clone's Game view too (host-side already validated). If something off, fix and amend; otherwise nothing more to do for Slice 2.
3. Then start **Slice 3 (link-share UI)** per the revised plan: a UI button on the host that copies the share URL (currently only logged to console) to the OS clipboard, and a visible text field showing the URL. Probably extend `Multiplayer.unity` with a Canvas + Button + Text. Cross-platform clipboard: `GUIUtility.systemCopyBuffer = url;` works on WebGL and desktop in Unity 2020.
4. **WebGL still parked.** Don't burn another session on it without searching Photon forums for the `b163`/`nullFunc_vi` signature first.

---

### 2026-05-01 — Slice 1 fully validated two-peer; ParrelSync embedded + patched

**Agent session goal:** Per yesterday's exit plan: install ParrelSync, prove two-peer connect Editor-to-Editor, then start Slice 2. Got through the validation; Slice 2 not started this session.

**What got done:**
- **Installed ParrelSync** (free Unity package). UPM git-URL install needed `?path=/ParrelSync` because the package manifest lives in a subdirectory of the repo, not at root.
- **Patched ParrelSync's `Editor/Preferences.cs` line 91** — the upstream master uses `string.Split(string)`, which is .NET Standard 2.1 / .NET Core 2.1+ only. Unity 2020.3 ships with neither (.NET 4.x's Mono on this version doesn't have it either, so the API-level switch this session didn't help — confirmed empirically). Replaced with `Split(new[] { token }, StringSplitOptions.None)`, which works on .NET Standard 2.0+. Added `using System;` for `StringSplitOptions`.
- **Embedded the package**: copied from `Library/PackageCache/com.veriorpies.parrelsync@610157ad76/` to `Packages/com.veriorpies.parrelsync/`, and changed the `Packages/manifest.json` entry from the git URL to `file:com.veriorpies.parrelsync`. This makes the patch survive package reimports and gives anyone cloning the repo a working ParrelSync without re-doing the patch.
- **Created clone via ParrelSync menu → Clones Manager → Add new clone**. Clone landed at `../super-inverters-game-clone-0/` (sibling dir, symlinked Library/).
- **Added `editorRoomOverride` `[SerializeField]` to `MultiplayerBootstrap`**, gated by `#if UNITY_EDITOR`. In the Editor, `Application.absoluteURL` is empty so URL-based join doesn't work — this field lets the joiner instance be told the room code via Inspector. Ignored in WebGL builds (URL still wins there).
- **Tightened the join log message**: now says `(from URL)` or `(from editor override)` instead of the misleading `from URL.` it always said.
- **Validated Slice 1 end-to-end**: host (original Editor) created room `D8432F` and logged the share URL. Joiner clone with `editorRoomOverride = "D8432F"` connected, joined, became actor #2. Host received `OnPlayerEnteredRoom` callback; clone received `OnJoinedRoom`. Both consoles logged `2/2 players`. **Slice 1 is now genuinely complete and team-testable.**

**State left behind:**
- Branch `Multiplayer`. Yesterday's Slice 1 commit (`9b1ed6db`) is still **1 commit ahead of `origin/Multiplayer`, not pushed**. Today's commit makes that **2 ahead**.
- This commit bundles: `MultiplayerBootstrap.cs` (editor override + log fix), `Packages/manifest.json` + `Packages/packages-lock.json` (file: path), `Packages/com.veriorpies.parrelsync/` (33 files, ~176 KB — patched ParrelSync), `Assets/Plugins/ParrelSync/` (12 KB ParrelSync project-settings asset), and this AGENT_CONTEXT.md entry.
- **Skipped from commit (left in working tree, user's call):** `Assets/Scenes/level_2.unity` and `start_scene_2.unity` modifications and their `*Settings.lighting` bake outputs, `UserSettings/EditorUserSettings.asset`, `ProjectSettings/{ProjectSettings.asset,SceneTemplateSettings.json,TimelineSettings.asset}`. ProjectSettings.asset specifically may contain today's API Compatibility Level toggle to .NET 4.x (which we tried for ParrelSync and abandoned in favor of the patch); not committing leaves the user free to decide.
- WebGL still broken — same dead-ends from yesterday. Not touched this session.
- The trailing-space scene filename `Multiplayer .unity` still not renamed. Easy in-Editor whenever the user gets to it.

**What's blocked or unclear:**
- Whether to push the 2 local commits to `origin/Multiplayer` now or wait until Slice 2 lands.
- Whether to commit ProjectSettings.asset (the .NET 4.x toggle) separately. Affects WebGL targets only; doesn't matter for Editor work.

**Next agent should:**
1. Confirm with user: push or wait?
2. Start **Slice 2 (color assignment)** — the next item in "Planned multiplayer integration":
   - Add a "Host as Black" / "Host as White" button (or flag) so the host picks their color before creating the room.
   - On `CreateRoom`, set a `hostColor` custom room property.
   - On the joiner side in `OnJoinedRoom`, read `hostColor` and take the opposite — assign that color to the local player.
   - Each peer instantiates exactly one Player from `Assets/Prefabs/Player.prefab` via `PhotonNetwork.Instantiate`, with the right `Framework` set on its `PlayerState`.
   - Verify with two ParrelSync Editors: both Players appear on both screens, with the right colors. **Don't touch `PlayerManager` or `GameManager` yet.**
3. Slice 2 is mostly Photon Custom Properties + a small UI tweak to the multiplayer scene. The existing `MultiplayerBootstrap` is a good place to extend, or create a sibling `MultiplayerSpawner` if it gets crowded.



**Agent session goal:** User implemented Slice 1 (`MultiplayerBootstrap` + scene + Build Settings flip), verified it works in the Unity Editor, and tried to ship a WebGL build. Agent's role: post-hoc documentation, then attempted WebGL fixes (none of which worked). Net outcome: Slice 1 is real and Editor-runnable; WebGL is blocked.

**What got built this session:**
- `Assets/scripts/Multiplayer/MultiplayerBootstrap.cs` (133 lines, namespace `Multiplayer`). Implements Slice 1 verbatim: `MonoBehaviourPunCallbacks` subclass that on `Start` reads `?room=XYZ` from `Application.absoluteURL`, calls `PhotonNetwork.ConnectUsingSettings()`, and on `OnConnectedToMaster` either joins the URL room or creates a new one with a 6-char hex code (max 2 players, invisible). Logs share URL in `OnJoinedRoom` for the master client. Has callbacks for create/join failure and disconnect. **Verified working in Editor 2026-04-30:** all five expected log lines appear (`Connecting to Photon... → Connected to master (eu) → Hosting new room 'BACB87' → Joined room as actor #1 (1/2 players) → Share this URL ...`).
- New scene `Assets/Scenes/Multiplayer .unity`. **⚠ Filename has a trailing space before `.unity`** — fine for Unity but cursed in shell (needs quoting). Should be renamed to `Multiplayer.unity` from inside the Editor next session (Editor handles ref updates automatically).
- `Assets/link.xml` (agent-authored) preserves `PhotonUnityNetworking`, `PhotonUnityNetworking.Utilities`, `PhotonRealtime`, `PhotonChat`, `PhotonWebSocket`, `Photon3Unity3d` from managed code stripping. The PUN-shipped `Assets/Photon/PhotonUnityNetworking/link.xml` only preserves a few `mscorlib`/`System` namespaces and `ExitGames.Client.Photon` — it does **not** cover the PUN/Realtime assemblies themselves, so a project-level `link.xml` is necessary.
- Build Settings: only `Scenes/Multiplayer` checked at index 0; all other scenes unchecked. Target: WebGL.
- `PlayerSettings.apiCompatibilityLevelPerPlatform` — switched to `.NET 4.x` for WebGL mid-session, then reverted to `.NET Standard 2.0` (which is Photon's recommendation for WebGL anyway). Net diff in commit may be near-zero.
- `.gitignore` (this session): added `Web build*/` and `.claude/` patterns to keep build outputs and the agent-worktree directory out of commits.

**WebGL debug dead-ends — DO NOT re-run these in a future session:**

WebGL build succeeds (`Build completed with a result of 'Succeeded'`, ~120–140s) but the wasm aborts at runtime the moment Photon opens its WebSocket. Browser console shows `[Multiplayer] Connecting to Photon...` followed immediately by:

```
Invalid function pointer called with signature 'vi'.
Build with ASSERTIONS=2 for more info.
163
abort(163) at Error
    at jsStackTrace (.../framework.js:739:12)
    at stackTrace (.../framework.js:753:11)
    at abort (.../framework.js:19:44)
    at nullFunc_vi (.../framework.js:15660:2)
    at b163 (.../build2.wasm)
    at dynCall_vi (.../build2.wasm)
    at WebSocket.<anonymous> (.../framework.js:3908:3)
```

JS WebSocket callback dispatches into wasm via `dynCall_vi` → function table index 163 is null/`nullFunc_vi` → abort. Looks like managed code stripping or a related IL2CPP issue, but is not — none of the following moved the needle:

- **Managed Stripping Level**: `Low` is the floor in Unity 2020.3 (no `Disabled`/`Minimal` option in this version's WebGL dropdown — confirmed by user: only `Low/Medium/High`).
- **Project-level `link.xml`** preserving PUN, Realtime, Chat, WebSocket, Photon3Unity3d. Build still aborts at the same function index. (Adding broader preserves like `mscorlib` `System.Reflection` to link.xml caused a different failure: `build.bc is not valid LLVM bitcode` — IL2CPP-generated C++ that Emscripten can't compile. Reverted.)
- **`.NET 4.x` ↔ `.NET Standard 2.0`** for WebGL — identical crash both ways.
- **"Strip Engine Code" off** — caused build to fail entirely with `build.bc is not valid LLVM bitcode`. Re-enabled.
- **Development Build off** (release build) — same `b163` abort.
- **Lightmap Encoding → Normal Quality** — same abort.
- **IL2CPP cache wipe**: `rm -rf Library/Bee Library/IL2CPPBuildCache Library/PlayerDataCache` followed by reopen + rebuild — same abort.

So the runtime crash is **deterministically baked into IL2CPP's wasm output for this project + PUN + Unity 2020.3.48f1 + macOS** combination. Not config-fixable from the user side. Working theory: Unity 2020.3.48 has a known bug in IL2CPP function-pointer table generation that hits when PUN's WebGL WebSocket layer registers JS-callable callbacks via `Marshal.GetFunctionPointerForDelegate`. Photon forums + Unity issue tracker likely have hits — neither was searched this session.

**State left behind:**
- Branch `Multiplayer`, local == `origin/Multiplayer` (0/0) at the start of this session. After this session's commit there will be **1 commit ahead, not pushed.**
- Slice 1 commit (this session) bundles: `Assets/scripts/Multiplayer/`, `Assets/Scenes/Multiplayer .unity` (+ `.meta`), `Assets/link.xml` (+ `.meta`), `ProjectSettings/{ProjectSettings,EditorBuildSettings}.asset`, `.gitignore`, `AGENT_CONTEXT.md`. **Skipped from the commit:** `Assets/Scenes/level_2.unity`, `start_scene_2.unity` modifications, the `*Settings.lighting` bake outputs, `UserSettings/EditorUserSettings.asset`, `ProjectSettings/{SceneTemplateSettings.json,TimelineSettings.asset}`, `Web build 2/`, `.claude/`. Those remain in the working tree — user can decide what to do with them later.
- `Web build 2/` (the failing WebGL build output) lives in the working tree but is now gitignored — left in place so the user can keep poking at it without rebuilding from scratch.
- Photon dashboard / App ID untouched. `PhotonServerSettings.asset` still gitignored.

**What's blocked or unclear:**
- WebGL build is blocked end-to-end (see dead-ends above). Genuinely unclear whether this is fixable without bumping Unity version or filing a Photon ticket.
- Two-peer end-to-end connect has been proven in *one* Editor instance only — has not been tested with two clients in the same room (one Editor + one build, or two Editors via ParrelSync). Slice 1 isn't fully validated until that's done.
- The trailing-space scene filename should still be renamed (`Multiplayer .unity` → `Multiplayer.unity`) — easy in the Editor, fiddly afterward.

**Next agent should:**
1. Install **ParrelSync** (free Unity package, `https://github.com/VeriorPies/ParrelSync.git` via Package Manager → "Add package from git URL"). Lets you spin up a second Editor instance pointing at the same project. ~5 min of setup.
2. With two Editor instances open, validate two-peer connect: one acts as host (creates room), the other as joiner (uses `?room=XXXXXX` via the room-code log line — but since `Application.absoluteURL` is empty in the Editor, the joiner needs to call `PhotonNetwork.JoinRoom("XXXXXX")` directly or `MultiplayerBootstrap` needs an Editor-only override for the room code). Add the override if needed; it's a few lines.
3. Rename `Multiplayer .unity` → `Multiplayer.unity` from inside the Editor (Project pane right-click → Rename).
4. Once two-peer connect is proven in-Editor, start **Slice 2 (color assignment)** — see "Planned multiplayer integration." Add a `hostColor` custom room property; joiner reads it on `OnJoinedRoom` and instantiates the opposite-color player via `PhotonNetwork.Instantiate`. Don't touch `PlayerManager` or `GameManager` yet.
5. **WebGL is a separate, parked problem.** Don't burn another full session on it without:
   - Searching the Photon forums for the exact stack signature (`abort(163)` + `nullFunc_vi` + `WebSocket.<anonymous>` on Unity 2020.3 macOS).
   - Considering a Unity version bump (2020.3.x final, or 2021.3 LTS) — but per STOP section, this is a deliberate user decision; don't unilaterally upgrade.
   - Trying the build on a non-macOS machine (Windows/Linux) to confirm it's the macOS toolchain.



**Agent session goal:** Verify the PUN install works against Photon Cloud and commit the import as a checkpoint.

**What I did:**
- User pasted App ID into the PUN Setup Wizard, hit Setup Project. Console: clean.
- User opened `Assets/Photon/PhotonUnityNetworking/Demos/PunBasics-Tutorial/Scenes/PunBasics-Launcher.unity`, pressed Play, entered name. Game view + console showed: `OnConnectedToMaster` → `OnJoinRandomFailed` (expected, no rooms yet) → `OnJoinedRoom with 1 Player(s)`. Networking confirmed working.
- The remaining red console line `Scene 'PunBasics-Room for 1' couldn't be loaded` is a demo-only issue (the demo's next scene isn't in Build Settings). Harmless. Ignore.
- Cleaned up `.gitignore`: added `[Ll]ogs/` and `[Uu]ser[Ss]ettings/`. Untracked 21 stale shader-compiler logs that had been polluting commits since project creation. (commit `c46e9570`)
- Committed all of `Assets/Photon/` as one checkpoint, 978 files, 39 MB. `PhotonServerSettings.asset` confirmed excluded. (commit `54fe2c06`)
- Updated this doc: locked in secret-handling decision, locked in tracking scope, added a 5-slice integration plan to the "Planned multiplayer integration" section.

**State left behind:**
- Branch `Multiplayer`, working tree clean. **7 commits ahead of `origin/Multiplayer`**, not pushed:
  - `54fe2c06` Add Photon PUN 2
  - `c46e9570` Untrack Logs/UserSettings
  - `afe9603a` Document AppKit crash
  - `d185b398` Gitignore PhotonServerSettings
  - `186dee2f` Confirm PUN 2; flag App-ID handling
  - `0514b424` Record Photon as networking choice
  - `314c2946` Add AGENT_CONTEXT.md
- Photon configured: app type PUN, region `eu`, dev mode. App ID is local-only in `PhotonServerSettings.asset`.

**What's blocked or unclear:**
- Whether to push the 7 local commits to `origin/Multiplayer` now (a checkpoint) or wait until Slice 1 lands.
- Whether to start writing Slice 1 in this same session or hand off here. The user designed this AGENT_CONTEXT.md flow specifically to handle context exhaustion mid-implementation; this is a natural handoff point if the next slice is best fresh.

**Next agent should:**
1. Confirm with user: "Push current 7 commits, or wait?" and "Start Slice 1 (two-peer connect with URL room param) now, or start fresh in a new session?"
2. If proceeding with Slice 1: create `Assets/scripts/Multiplayer/MultiplayerBootstrap.cs`, attach to a GameObject in a new test scene `Assets/Scenes/multiplayer_test.unity`, verify with two Editor instances (use Unity's "Multiplayer Play Mode" preview package or just build + open the build alongside the editor). Don't touch `PlayerManager`, `GameManager`, or any existing gameplay scripts in Slice 1.
3. Read the "Planned multiplayer integration" section above for the full slice breakdown — don't expand scope mid-slice.

### 2026-04-30 — PUN 2 imported successfully despite a Unity AppKit crash on dialog dismissal

**Agent session goal:** Get PUN 2 imported and the App ID pasted into PhotonServerSettings.

**What I did:**
- Added `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset` and its `.meta` to `.gitignore` BEFORE running the import (commit `d185b398`). Done so the App ID can't accidentally land in a public commit.
- User clicked Import in the PUN 2 Unity Package dialog. Unity then popped a Bug Reporter window — crash.
- **Diagnosed: import actually succeeded.** `Editor.log` shows every PUN assembly compiled cleanly (`PhotonUnityNetworking.dll`, `PhotonRealtime.dll`, demos, project's `Assembly-CSharp.dll` — all green), then a macOS AppKit bug fired during progress-dialog cleanup:
  - `-[NSApplication runModalSession:]: Use of freed session detected.`
  - `objc_removeExceptionHandler() ... probably a bug in multithreaded AppKit use.`
  - Stack: `[ProgressbarController dealloc] → endModalSession: → _objc_terminate → abort`
- This is a known Unity 2020.3-on-macOS issue, not caused by PUN or by anything we did. **Future agents: if this crash recurs after a long import, do not panic — check `~/Library/Logs/Unity/Editor.log` for the same `runModalSession`/`ProgressbarController dealloc` signature and confirm the import finished compiling before dismissing it as a real failure.**
- Verified PUN files are on disk: `Assets/Photon/{PhotonChat, PhotonUnityNetworking, PhotonLibs, PhotonRealtime}` total 39 MB.

**State left behind:**
- Branch `Multiplayer`, working tree dirty (the entire `Assets/Photon/` tree of 39MB is untracked). **Do NOT `git add Assets/Photon` blindly** — first decide whether the demos and PhotonChat should be tracked, since they bloat the repo and we don't actually need them. Recommend tracking only `PhotonUnityNetworking/{Code, Plugins, Resources}`, `PhotonRealtime`, `PhotonLibs` and gitignoring `PhotonChat` + `**/Demos/`. Confirm with user before committing.
- 4 commits ahead of `origin/Multiplayer`, not pushed.
- App ID has NOT been pasted yet — user needs to reopen Unity (the crashed session never reached the wizard's submit step, even though the file got created). When they reopen: PUN Setup Wizard might auto-open; if not, open `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset` in the Inspector and paste there. The settings asset is gitignored, so the App ID stays local.

**What's blocked or unclear:**
- Whether to track all of `Assets/Photon/` or only the parts we use (see "State left behind").
- Whether the PUN Setup Wizard reopens cleanly after the crash, or if we need to invoke it from the menu.

**Next agent should:**
1. Have the user reopen the project in Unity Hub. Confirm in the Editor that `Assets/Photon/` is visible in the Project panel and the console shows no compile errors.
2. Have the user paste the App ID into `PhotonServerSettings.asset` (via the wizard or the inspector). Agent must not handle the App ID.
3. Decide tracking scope for `Assets/Photon/` (recommend slimming as above) and update `.gitignore` accordingly before any commit that touches the directory.
4. Verify connectivity: open `Assets/Photon/PhotonUnityNetworking/Demos/PunBasics-Tutorial/Scenes/PunBasics-Launcher.unity` (or any demo scene), press Play, look for `OnConnectedToMaster` or "Connected to master" in the console.

### 2026-04-30 — Confirmed PUN 2; flagged App-ID secret-handling before any PUN commit

**Agent session goal:** Pin down the Photon product and App ID, surface the credential-leak risk before installing PUN in a public repo, and identify exactly where the user got stuck.

**What I did:**
- User shared two Photon dashboard screenshots. Confirmed: app **"Super Inverters"**, type **PUN** (= PUN 2), free tier 20 CCU, App ID prefix `159a8424-...`. Recorded prefix only — full ID is a credential and was deliberately not stored in this doc.
- Verified there are zero Photon files in the project tree and no `photon` entries in `.gitignore`. So the prior agent never started the Unity install.
- Added a security note in STOP: this repo is on public GitHub, so the App ID must not be committed. Logged two acceptable handling strategies (gitignore the settings asset, or stub-asset + untracked override).
- Replaced the "which Photon product" open question (now answered) with the "secret-handling decision" open question (must answer before first PUN commit).

**State left behind:** Branch `Multiplayer`, working tree clean after this commit. 3 commits ahead of `origin/Multiplayer`, not pushed. No Unity packages installed. No `.gitignore` changes yet.

**What's blocked or unclear:**
- User's App-ID handling preference (gitignore vs stub+override).
- Whether the user wants me to *talk them through* the PUN install in Unity Editor (I cannot drive the Editor; I can only give precise click-by-click steps and read logs they paste back).

**Next agent should:**
1. Decide App-ID handling with the user, then add the corresponding `.gitignore` line **before** running PUN's setup wizard (so an accidental save can't leak the ID). If "gitignore the asset," append `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset` and the matching `.meta` to `.gitignore`.
2. Install PUN 2 via Unity's Package Manager → My Assets (after the user clicks "Add to My Assets" on https://assetstore.unity.com/packages/tools/network/pun-2-free-119922 if it's not already in their library). Import everything; the PUN Setup Wizard auto-opens.
3. Have the **user** paste the App ID into the wizard — agents must not handle the secret.
4. Verify connectivity: open `Assets/Photon/PhotonUnityNetworking/Demos/PunBasics-Tutorial/` (a demo scene), press Play, confirm console logs "OnConnectedToMaster". Stop there. Don't write gameplay sync code in the same session.

### 2026-04-30 — Photon chosen as networking stack (account + project already exist)

**Agent session goal:** Capture that the user, in the prior (interrupted) agent session, was directed to Photon, signed up, verified email, and created a Photon dashboard project. Then they got stuck.

**What I did:**
- Updated STOP section to lock in Photon as the networking stack.
- Replaced the "pick a stack" open question with two narrower asks: confirm *which Photon SDK* (PUN 2 / Fusion / Realtime / Quantum) the dashboard project was created for, and get the App ID.
- Committed the update.

**State left behind:** Branch `Multiplayer`, working tree clean, this commit + previous AGENT_CONTEXT.md commit are ahead of origin and not pushed. No Unity package installed yet — DO NOT install a Photon SDK before confirming which product the dashboard project is for.

**What's blocked or unclear:**
- Which Photon product the user's project is for (visible on their dashboard as "App Type" / "Type"). PUN 2 is the recommended fit; if the prior agent walked them into Fusion, that's workable but more complex.
- The literal App ID string.
- What specifically the user got "stuck" on. Likely candidates: didn't know what to paste where in Unity; couldn't find the App ID; was about to install the SDK when the agent crashed. Ask them.

**Next agent should:**
1. Ask the user: (a) "On your Photon dashboard, what is the App Type of the project you created — PUN, Fusion, Realtime, or Quantum?" (b) "Can you share the App ID? It's a long hex string visible on the dashboard." (c) "Where exactly did you get stuck — pre-install, mid-install, or trying to wire up a script?"
2. Once both answers are in: install the matching SDK from the Unity Asset Store / Package Manager into the project, paste the App ID into the auto-generated `PhotonServerSettings` asset, and verify the Unity Editor connects to Photon's name server (visible in the console as a "Connected to Master" log line). Stop there in that session — don't start writing gameplay sync code yet.

### 2026-04-30 — User confirmed WebGL, no-lobby flow, and host-picks-color UX

**Agent session goal:** Lock down the user's answers to the open questions and commit the doc to the branch.

**What I did:**
- Confirmed in the STOP section: **WebGL** is the build target, **no lobby UI**.
- Rewrote the "Multiplayer goal" section with the explicit 5-step user flow: host picks color → generates link → manually shares → friend joins → friend gets opposite color automatically.
- Added explicit non-goals (no lobby, no reconnect, no >2 players, no anti-cheat) so future agents don't scope-creep.
- Narrowed open questions to networking stack + hosting model (the two real implementation choices left).
- Committed `AGENT_CONTEXT.md` to branch `Multiplayer`. Did **not** push — push is a separate decision.

**State left behind:** `Multiplayer` branch has one new commit ahead of `origin/Multiplayer` containing only `AGENT_CONTEXT.md`. Working tree clean.

**What's blocked or unclear:**
- Networking stack still unpicked (Photon vs Mirror vs NGO+Relay). Recommend Photon Fusion/PUN if the user values "simplest" over "no vendor"; recommend Mirror+SimpleWebTransport otherwise.
- The link-payload format is undecided: just `?session=ABCD` (host color is server state) vs `?session=ABCD&join=white` (encoded in URL). Either works; pick when implementing.

**Next agent should:**
1. Ask the user to pick a networking stack from the three candidates. Frame it as a tradeoff: Photon = fastest path to "send a link, friend joins"; Mirror = no SaaS dependency, more setup; NGO+Relay = official Unity path. Don't install anything before they pick.
2. Once picked, scope a vertical slice: a new `MultiplayerMenu` scene with "Host as Black" / "Host as White" / "Join via link" buttons, a `NetworkController : Controller` for the remote player (use the `Controller` polymorphism — see "Architecture seams"), and replicate the spawn/kill events from `GameManager`. Get two browser tabs to play `level_1` against each other before touching the link-share UX.
3. Keep `level_1..5` and the existing local-play flow working — multiplayer should be additive, not a replacement, until proven.

### 2026-04-30 — Added itch.io link + Unity Hub note (same session, follow-up)

**Agent session goal:** Capture two pieces of context the user shared after the doc was first written.
**What I did:**
- Added the live WebGL build URL ([https://nmeidan.itch.io/superinverters](https://nmeidan.itch.io/superinverters)) to the "STOP" section as the canonical "latest web version."
- Noted that `super-inverters-game` is registered in Unity Hub at the repo path with editor `2020.3.48f1`. A red warning icon appears next to that Unity version in Hub — almost certainly the standard "this version is no longer maintained by Unity" notice, not a project error. Future agents should not panic-upgrade.
- Promoted the WebGL question from "open" to "strong default — confirm" since a live WebGL build already exists and matches the link-share UX the user wants.
- Refined the recommendation list to WebGL-friendly stacks (Mirror+SimpleWebTransport, NGO+Relay WebSocket, Photon).

**State left behind:** Same as previous entry. `AGENT_CONTEXT.md` is the only untracked change on branch `Multiplayer`. Not committed.

**What's blocked or unclear:** Same as prior entry — networking stack and hosting choice still need user input. WebGL target now near-certain but not officially confirmed.

**Next agent should:** Same as prior entry. When asking the user about platform, lead with "Confirm WebGL is the target?" rather than open-ended.

### 2026-04-30 — Context handoff doc created

**Agent session goal:** Bootstrap an `AGENT_CONTEXT.md` so future agents can resume work without burning context re-discovering the project. User reported hitting image-size-limit errors mid-session and couldn't even ask the previous agent to summarize.

**What I did:**
- Surveyed repo at [/Users/nadav/Documents/GitHub/super-inverters-game/](.) (the launching cwd `/Users/nadav/Documents/Claude/Super Inverters Reloaded/` is empty — flagged at top).
- Confirmed branch `Multiplayer` exists locally and on origin, working tree clean.
- Confirmed Unity version `2020.3.48f1` from [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt).
- Confirmed **no networking packages** are installed in [Packages/manifest.json](Packages/manifest.json).
- Read [Assets/scripts/Game/GameManager.cs](Assets/scripts/Game/GameManager.cs), [Assets/scripts/Player/PlayerManager.cs](Assets/scripts/Player/PlayerManager.cs), and [Assets/scripts/Controllers/KeyboardController.cs](Assets/scripts/Controllers/KeyboardController.cs) to identify the input-controller abstraction as the natural multiplayer seam.
- Wrote this file. **No code changes made.**

**State left behind:**
- Branch `Multiplayer`, clean tree before this file. After saving this file, `AGENT_CONTEXT.md` is the only new untracked file. Not committed.
- No running processes, no Unity Editor session opened by me.

**What's blocked or unclear:**
- Target platform (WebGL vs desktop) for the multiplayer build is undecided — biggest open question.
- Networking stack choice (Mirror / NGO+Relay / Photon / WebRTC) depends on the platform answer.
- Whether to commit this file to the `Multiplayer` branch — user hasn't said.

**Next agent should:**
1. Ask the user the two open questions above (platform + networking-stack constraints / hosting budget).
2. Once answered, propose a minimal vertical slice: install the chosen package, add a `NetworkController : Controller`, get two clients running on localhost with one moving Black and one moving White on `level_1`. Don't build the link/lobby UI until that works.
3. Ask the user before committing this `AGENT_CONTEXT.md` to the branch.
