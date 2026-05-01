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
