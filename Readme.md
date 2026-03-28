# Multistream Manager (SSMM_UI)

Multistream Manager is a Windows desktop app (Avalonia + .NET 9) for local RTMP ingest, multi-destination restreaming, metadata management, OAuth login handling, and optional social announcement posting.

It acts as a control center between:

- Your encoder (`OBS`/other RTMP-capable software)
- A local RTMP server (`rtmp://localhost:1935/live/demo`)
- Destination platforms (YouTube, Twitch, Kick, and other RTMP targets from `services.json`)
- Optional social posting (X, Facebook Pages, Discord webhooks)


## Table of Contents

- [What the App Does](#what-the-app-does)
- [High-Level Architecture](#high-level-architecture)
- [Project Structure](#project-structure)
- [Core Workflows (How It Works)](#core-workflows-how-it-works)
- [Authentication and Token Lifecycle](#authentication-and-token-lifecycle)
- [Streaming Pipeline and ffmpeg](#streaming-pipeline-and-ffmpeg)
- [Metadata and Category Handling](#metadata-and-category-handling)
- [Social AutoPoster](#social-autoposter)
- [Themes and UI Behavior](#themes-and-ui-behavior)
- [Persistence and Storage](#persistence-and-storage)
- [Configuration Files](#configuration-files)
- [Prerequisites](#prerequisites)
- [Setup and First Run](#setup-and-first-run)
- [Build and Run](#build-and-run)
- [Installer (WiX)](#installer-wix)
- [Troubleshooting](#troubleshooting)
- [Known Limitations / Notes](#known-limitations--notes)


## What the App Does

Main capabilities:

- Maintains a local RTMP ingest server and status polling.
- Lets you choose stream destinations and store their stream keys.
- Creates/updates platform broadcast sessions where applicable.
- Starts one ffmpeg forwarding process per active destination.
- Lets you manage stream metadata (title, thumbnail, category).
- Supports OAuth login for YouTube, Twitch, Kick, X, Facebook.
- Persists settings, service selections, metadata, webhooks, and encrypted tokens.
- Can auto-post social announcements when stream starts.
- Provides live logs and per-output ffmpeg stderr output.
- Supports multiple built-in themes with a runtime theme selector.


## High-Level Architecture

The app follows an MVVM-centered structure with service-driven state:

- **Views (`.axaml`)**: UI markup only.
- **ViewModels**: Bindable UI state + commands.
- **Services**: Application logic, persistence, auth, stream orchestration.
- **`StateService`**: central state owner and persistence boundary.

Dependency injection is configured in `SSMM_UI\App.axaml.cs` and services/viewmodels are primarily registered as singletons.

Application startup is in `SSMM_UI\Program.cs`:

- Sets `ffmpeg.RootPath = "Dependencies"`
- Starts Avalonia app lifetime.


## Project Structure

Important folders/files:

- `SSMM_UI\Program.cs` - app entrypoint.
- `SSMM_UI\App.axaml.cs` - DI setup, main window initialization.
- `SSMM_UI\MainWindow.axaml` - shell layout (menu, tabs, side panels, theme selector).
- `SSMM_UI\Services\StateService.cs` - central state, token events, persistence.
- `SSMM_UI\Services\StreamService.cs` - ffmpeg process management + stream start/stop.
- `SSMM_UI\Services\BroadCastService.cs` - YouTube/Twitch/Kick broadcast creation logic.
- `SSMM_UI\Services\CentralAuthService.cs` - OAuth coordination and auto-login logic.
- `SSMM_UI\Services\ThemeService.cs` - theme registration/application/persistence.
- `SSMM_UI\Poster\SocialPoster.cs` - social post dispatch logic.
- `SSMM_UI\RTMP\RTMPServer.cs` - local RTMP/HTTP-FLV/HLS admin server host.
- `WixPackage\` - MSI packaging and shortcut definitions.


## Core Workflows (How It Works)

### 1) Service selection

- `LeftSideBarViewModel` binds available RTMP service groups loaded by `StateService` from `services.json`.
- User selects a service.
- `DialogService.ShowServerDetailsAsync(...)` opens server/key dialog and stores a `SelectedService` in `StateService.SelectedServicesToStream`.

### 2) Login and token state

- Login actions in `LoginViewModel` and `SocialPosterLoginViewModel` call `CentralAuthService`.
- `StateService` stores tokens in encrypted files and updates in-memory auth dictionary.
- UI statuses refresh from `StateService.OnAuthObjectsUpdated`.

### 3) Metadata preparation

- `MetaDataViewModel` edits title/category/thumbnail.
- Metadata is kept in `StateService` and serialized to roaming storage.

### 4) Start stream

- `StreamControlViewModel.StartStream()`:
  - Validates active selected outputs.
  - For YouTube/Twitch/Kick, asks `BroadCastService` to prepare upstream broadcast metadata/session details.
  - Calls `StreamService.StartStream(...)`.

- `StreamService.StartStream(...)`:
  - Builds ffmpeg args for each active output.
  - Starts one ffmpeg process per output.
  - Captures process references and makes them visible in output logs.

### 5) Stop stream

- `StreamControlViewModel.StopStreamsCommand` calls `StreamService.StopStreams()`.
- Existing ffmpeg processes are killed and output viewmodels disposed.


## Authentication and Token Lifecycle

Auth providers in use:

- Stream providers: `YouTube`, `Twitch`, `Kick`
- Social providers: `X`, `Facebook`

Key mechanics:

- Tokens implement a common auth token interface (`IAuthToken`).
- `StateService` stores tokens encrypted via DPAPI (`SecureStorage`).
- `StateService` emits:
  - `OnTokenAdded`
  - `OnTokenRemoved`
  - `OnAuthObjectsCleared`
  - `OnAuthObjectsUpdated`
- `AvailableAuthProviders` is exposed as a `ReadOnlyObservableCollection` for reactive UI binding.

Token purge:

- “Purge all” removes token files + clears in-memory state.
- “Purge specific token” removes one provider token from both storage and in-memory state.
- Login status labels are derived from current state (not stale snapshots).


## Streaming Pipeline and ffmpeg

### Local ingest

- App starts local RTMP server (`RTMPServer.StartSrv()`) on startup through `StreamService` constructor.
- Ingest endpoint is expected at:
  - `rtmp://localhost:1935/live/demo`

### ffmpeg path and binaries

- Runtime expects ffmpeg tools in `SSMM_UI\Dependencies`.
- `Program.cs` sets ffmpeg root path accordingly.
- `ffmpeg`, `ffprobe`, and related DLLs are included as copied dependencies in project file.

### Output process model

- One ffmpeg process per selected active destination.
- ffmpeg args are built based on service recommended settings where available.
- Current default video codec behavior uses stream copy (`-c:v copy`) fallback.
- Process stderr is captured and surfaced in UI through `OutputViewModel`.

### Polling

`PollService` reports:

- RTMP stream alive status (via ffmpeg probe logic against local ingest URL).
- Server status (via HTTPS check to local admin endpoint `https://localhost:7000/ui/`).


## Metadata and Category Handling

Metadata (`MetaDataViewModel` + `MetaDataService`):

- Title editing.
- YouTube category selection from `youtube_categories.json`.
- Thumbnail image selection via file picker.
- Twitch category selection via `SearchView`.

Platform-specific updates:

- YouTube: broadcast title/category + optional thumbnail upload.
- Twitch: title/category update through Twitch API integration.
- Kick: title/category automation via puppeteering workflow.


## Social AutoPoster

`SocialPosterViewModel` controls:

- Enable/disable auto-post on stream start.
- Destination toggles: X, Facebook, Discord.
- Optional custom social message.

`SocialPoster` behavior:

- Resolves currently active stream platforms through `PostMaster`.
- Builds announcement text (`SocialPostTemplate`).
- Posts to enabled destinations when possible:
  - X API
  - Facebook Pages API
  - Discord webhooks from configured list
- Returns aggregate success/failure details (`SocialPostResult`) for logging.


## Themes and UI Behavior

Theme system:

- Implemented in `ThemeService`.
- Persisted in `UserSettings.ThemeKey`.
- Applied by replacing merged resource dictionaries at runtime.

Included themes:

- Midnight Neon
- Cyberpunk Pulse
- Vaporwave Drift
- Crimson Core
- Aurora Glass
- Oceanic Storm
- Solar Flare
- Acid Reactor
- Sunrise Glow

UI theme controls:

- Header `ComboBox` in `MainWindow` for direct theme selection.
- Theme selection is synchronized through `SelectedTheme` + `ThemeOption.IsSelected`.


## Persistence and Storage

Storage helper root:

- `%APPDATA%\GWAP Technologies\Multistream Manager\...`

Persisted data includes:

- `Settings\UserSettings.json`
- `Services\Serialized_Services.json`
- `Metadata\MetaData_State.json`
- `WebHooks\Webhooks.json`
- `Settings\WindowSettings.json`
- `Tokens\*Token.json` (encrypted)

On main window close (`MainWindow.axaml.cs`):

- Services, settings, webhooks, and window position are serialized.


## Configuration Files

- `SSMM_UI\services.json`  
  RTMP service definitions and recommended streaming constraints.

- `SSMM_UI\youtube_categories.json`  
  YouTube categories loaded into metadata UI.

- `SSMM_UI\Dependencies\*`  
  ffmpeg binaries and supporting native DLLs.


## Prerequisites

- Windows (desktop target)
- .NET SDK 9.x
- ffmpeg/ffprobe binaries present in `SSMM_UI\Dependencies`
- Valid API credentials/config for enabled OAuth providers
- OBS Studio (or another RTMP-capable encoder) for local ingest feed


## Setup and First Run

1. Ensure dependencies:

- Restore NuGet packages (`dotnet restore`).
- Verify `Dependencies` folder contains ffmpeg binaries.

2. Configure encoder:

- In OBS: Stream Service = **Custom**
- Server: `rtmp://localhost:1935/live/`
- Stream key: `demo`

3. Start app and authenticate providers as needed.

4. Add desired destinations in left sidebar and set stream keys.

5. Set metadata and optional social posting preferences.

6. Click **Start Receiving** in Inspection panel to preview local ingest.

7. Click **Start Stream** to begin forwarding to active destinations.


## Build and Run

From repository root:

```powershell
dotnet build .\SSMM_UI\SSMM_UI.csproj -nologo
dotnet run --project .\SSMM_UI\SSMM_UI.csproj
```

Solution build:

```powershell
dotnet build .\SSMM_UI.sln -nologo
```


## Installer (WiX)

Installer project:

- `WixPackage\WixPackage.wixproj`

Notes:

- Pre-build target publishes desktop app and harvests files with Heat.
- MSI output name is `MultistreamManager`.
- Desktop and Start Menu shortcuts target `MultistreamManager.exe`.
- Shortcuts explicitly use app icon via `WixPackage\Shortcuts.wxs` (`AppShortcutIcon`).

Build MSI from repo root:

```powershell
dotnet build .\WixPackage\WixPackage.wixproj -nologo -p:SolutionDir="C:\path\to\SSMM_UI\"
```


## Troubleshooting

### App can’t find icon/resource

- Verify URI uses new assembly host:
  - `avares://MultistreamManager/...`
- Ensure `Assets\MainIcon.png` is included as `AvaloniaResource`.

### Stream status shows not receiving

- Confirm encoder is pushing to `rtmp://localhost:1935/live/demo`.
- Confirm local RTMP service is running and no port conflict on `1935`.

### YouTube does not go live automatically

- Verify token is valid and YouTube service object exists.
- Check logs for lifecycle transition retries.
- Confirm ingest stream became active before transition timeout.

### Social post not sent

- Verify destination toggle enabled.
- Confirm corresponding token/webhook exists.
- Check log output for provider-specific error reason.

### Token purge not reflected in UI

- Current implementation uses central token events + read-only provider collection, so login status and purge dialogs should react immediately. If stale status appears, check subscribers to `OnAuthObjectsUpdated`.


## Known Limitations / Notes

- Some provider integrations are partial/stubbed (example: Trovo/Facebook broadcast creation stubs in `BroadCastService`).
- `PollService` currently runs continuous loops without explicit stop/dispose path for cancellation source.
- Some exception handling paths log-and-continue rather than fail-fast (legacy behavior).
- Current project contains an older `ReadMe\ReadMe.txt`; this `README.md` is intended to be the primary, comprehensive documentation moving forward.

