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
2. **Verify before trusting:** facts here can go stale. Before acting on a claim (file path, branch state, package presence), spot-check it.
3. **Confirm scope with the user before installing a networking package** or making sweeping changes — this branch is exploratory, not production.
4. **Keep edits minimal and on-branch.** Don't merge to master. Don't force-push.
5. **Last action of every session:** append an entry to "Update log" below (newest at the top of the log). Even if you accomplished nothing, log what you tried and what blocked you. Future-you needs the negative results too.
6. **If this file gets long (>300 lines):** compress old log entries into a single "Older history" summary block at the bottom, but keep the last ~10 entries verbatim.

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

### 2026-04-30 — PUN setup verified end-to-end; ready for multiplayer integration

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
