# SSMM_UI Design and Architecture

## System Overview

SSMM_UI is a Windows desktop control plane for multistream operations. It receives one local RTMP ingest stream (`rtmp://localhost:1935/live/demo`) and forwards it to multiple configured destinations via one ffmpeg process per output. It also coordinates provider authentication, metadata updates, social announcements, and persisted user/session state.

Core runtime model:

1. **UI layer** (Avalonia XAML Views) renders sections for service selection, stream control, metadata, login, logs, and preview.
2. **ViewModel layer** holds bindable state and command orchestration logic.
3. **Service layer** performs all side-effect operations (stream process lifecycle, OAuth, persistence, category cache, social posting, theme switching).
4. **StateService** is the central source of truth for mutable app data and persistent storage boundaries.

## Composition Root and Dependency Injection

Composition root is `SSMM_UI\App.axaml.cs`.

- All key services and ViewModels are registered as singletons.
- `mainWindow.DataContext` is bound to `MainWindowViewModel`.
- `Program.cs` sets `ffmpeg.RootPath = "Dependencies"` before Avalonia startup.

Implications:

- Shared singleton state is intentional across all tabs/panels.
- New features should be introduced through DI registration and ViewModel injection rather than static access.

## Layering and Responsibilities

### Views (`SSMM_UI\Views\*.axaml`)

- Visual structure and bindings only.
- Uses compiled bindings (`AvaloniaUseCompiledBindingsByDefault=true`) and `x:DataType`.
- Uses shared card style (`Border.card`) and dynamic theme resources.

### ViewModels (`SSMM_UI\ViewModel\*.cs`)

- Own command execution and user-facing status text.
- Coordinate services; do not duplicate service-level domain logic.
- Example: `StreamControlViewModel` decides start/stop flow and delegates process-level work to `StreamService`.

### Services (`SSMM_UI\Services\*.cs`)

- Host all external integration logic and persistent operations.
- Major services:
  - `StateService`: persisted app state, auth object map, auth provider tracking, selected services, metadata/webhooks/settings serialization.
  - `StreamService`: local RTMP server startup, ffmpeg process start/stop, pause/interject flow, process tracking.
  - `BroadCastService`: provider-specific pre-stream setup (YouTube/Twitch/Kick).
  - `CentralAuthService`: OAuth orchestration and auto-login flows.
  - `ThemeService`: runtime theme dictionary switching and persistence.
  - `PollService`: local ingest and server health polling.

## Streaming Architecture

### Ingest and forwarding

- Local ingest source: `rtmp://localhost:1935/live/demo`.
- `RTMPServer.StartSrv()` is invoked in `StreamService` constructor.
- For each active selected destination, `StreamService.StartStream(...)`:
  1. Resolves provider-specific setup through `BroadCastService` (if required).
  2. Builds output URL and ffmpeg args based on selected service and recommended constraints.
  3. Starts one ffmpeg process per destination.

### Process tracking model

`StreamService` maintains:

- `ffmpegProcess`: live forwarding processes.
- `pauseProcesses`: pause/interject replacement processes.
- `ProcessInfos` (`List<StreamProcessInfo>`): per-output metadata (`Header`, `Process`, `IsPaused`, interject metadata).

Pause/resume behavior depends on all three collections being updated coherently.

## Authentication and Token Lifecycle

Auth providers include stream and social providers.

Flow:

1. Login command in VM calls `CentralAuthService`.
2. Provider service returns typed token payload.
3. `StateService.SerializeToken(...)` persists encrypted token (DPAPI via `SecureStorage`) and updates in-memory auth map.
4. `StateService` emits auth events and updates `AvailableAuthProviders` (`ReadOnlyObservableCollection<AuthProvider>`).
5. UI reacts through bindings/event-driven updates.

Token purge:

- `DeleteToken(AuthProvider)` removes one token and emits update events.
- `DeleteAllTokens()` clears all tokens, clears provider collection, and emits update events.

## Persistence Architecture

Persistence boundary is `StateService` + `StorageHelper`.

Roaming persisted files include:

- `Settings\UserSettings.json`
- `Settings\WindowSettings.json`
- `Services\Serialized_Services.json`
- `Metadata\MetaData_State.json`
- `WebHooks\Webhooks.json`
- `Tokens\*Token.json` (encrypted)

On main window close (`MainWindow.axaml.cs`), services/settings/webhooks/window position are serialized.

## UI/Theme Architecture

### Shell layout

`MainWindow.axaml` hosts:

- top command menu,
- center tabbed workflow region (stream/metadata/social),
- left service management rail,
- right auth/inspection rail,
- splitters for runtime column resize control.

### Theming

- Theme resources live in `SSMM_UI\Themes\*.axaml`.
- `ThemeService` switches merged dictionaries in `App.Resources.MergedDictionaries`.
- Selected theme key persisted in `UserSettings.ThemeKey`.

## Current Design System

Common reusable design primitives:

- `Border.card` class for panel surfaces.
- dynamic brushes (`CardBackgroundBrush`, `CardBorderBrush`, `PanelOverlayBrush`, etc.).
- shared button variants (`primary`, `success`, `danger`, `auth-action`).
- high-contrast text and input brushes from active theme dictionary.

## Testing Architecture

Test project: `SSMM_UI.Tests` (`xunit.v3` + `Moq`).

Current test coverage focus:

- RTMP server registration behavior.
- converter/serialization behavior.

Execution entry points:

- `dotnet test .\SSMM_UI.Tests\SSMM_UI.Tests.csproj -nologo`
- single test via `--filter "FullyQualifiedName~<name>"`

## Build and Packaging

Application build:

- `dotnet build .\SSMM_UI\SSMM_UI.csproj -nologo`

Installer pipeline:

- GitHub workflow `.github\workflows\release-Installer.yml` builds WiX bundle on Windows runner.
- ffmpeg dependency binaries are downloaded/extracted in CI before bundle build.
- WiX bundle artifacts are attached to GitHub release on tag pushes (`v*.*.*`).

## Architectural Constraints and Invariants

1. `StateService` remains the shared state authority.
2. Compiled bindings must remain valid (`x:DataType` aligned with DataContext type).
3. ffmpeg runtime path remains `SSMM_UI\Dependencies`.
4. Window/dialog icon convention remains `avares://MultistreamManager/Assets/MainIcon.png`.
5. Service-level side effects stay out of Views and minimal in ViewModels.

## Recommended Extension Strategy

For any new feature:

1. Add/extend service APIs first.
2. Integrate state changes in `StateService` if persisted/runtime shared.
3. Inject service into ViewModel and expose command/state for UI.
4. Add compiled bindings in view (`x:DataType` aware).
5. Add/extend unit tests for service and converter logic.
