# WebGL deploy guide

## Prerequisites

1. **Unity Hub → Installs → Unity 6000.3.16f1 → Add modules → WebGL Build Support**  
   (Required; batch builds fail with "build target was unsupported" without it.)

2. **Photon App ID** in `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset` (gitignored).

## Build

```bash
./Scripts/build-webgl.sh
```

Output: `Web build/` (gitignored).

If the build fails with *script class layout is incompatible*, close Unity Editor, delete `Library/Bee` and `Library/PlayerDataCache`, then rebuild **using the same Unity version as the project** (6000.3.16f1).

## Local two-tab test

```bash
./Scripts/serve-webgl.sh
```

Open `http://127.0.0.1:8080/` in two browser tabs:

1. Tab A: Multiplayer → Create room → Copy link  
2. Tab B: Paste link (or open copied URL) → Join  
3. Play a full round (kill, reload, game-over)

## Deploy to itch.io

Install [butler](https://itch.io/docs/butler/), log in (`butler login`), then:

```bash
./Scripts/deploy-itch.sh
```

Default target: `nmeidan/superinverters:web` → https://nmeidan.itch.io/superinverters

Override with env vars: `ITCH_USER`, `ITCH_GAME`, `ITCH_CHANNEL`.

## Remote friend test

After deploy, host sends the in-game **Copy Room Address** link; guest opens it in a second browser/device on the same itch page.
