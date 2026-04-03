# Getting Started with Multistream Manager

## Prerequisites

1. Windows desktop environment.
2. .NET SDK 9.x installed.
3. ffmpeg binaries present under `SSMM_UI\Dependencies` (`ffmpeg.exe`, `ffprobe.exe`, required DLLs).
4. OBS Studio (or another RTMP encoder).
5. OAuth/API credentials configured for providers you want to use.

## First-run stream setup

1. Open **Settings** and confirm save/polling options.
2. Configure OBS:
   - Service: **Custom**
   - Server: `rtmp://localhost:1935/live/`
   - Stream key: `demo`
3. In app menu, open **Setup** and run provider setup actions as needed.
4. Log in to stream providers (YouTube/Twitch/Kick) and optional social providers (X/Facebook).
5. In left sidebar:
   - Select a service from **STREAM SERVICES**
   - Choose endpoint and stream key
   - Add all destinations you need
6. Open **Stream Metadata**:
   - Set stream title
   - Select YouTube category
   - Choose Twitch category
   - Upload thumbnail
   - Click **Update Metadata**
7. In **Social Media AutoPoster**, configure auto-post destinations and optional custom message.
8. In **Inspection**, click **Start Receiving** to verify local ingest preview.
9. In **Stream** tab, click **Start Stream**.

## Daily operation checklist

1. Confirm provider login status cards are healthy.
2. Confirm selected destinations are enabled.
3. Confirm metadata and social post settings.
4. Start receiving preview.
5. Start stream.
6. Monitor logs and per-output stream cards.
7. Stop stream when done.

## Troubleshooting quick reference

- **No incoming stream detected**
  - Verify encoder output target is `rtmp://localhost:1935/live/demo`.
  - Check no process is already bound to port `1935`.

- **Provider stream does not start**
  - Verify stream key and server URL for that destination.
  - Check provider login status card.
  - Check logs for ffmpeg process errors.

- **Metadata did not apply as expected**
  - YouTube/Twitch/Kick use dedicated platform integrations.
  - Other catalog services use embedded ffmpeg metadata fallback.
  - Check logs for per-service metadata result lines before stream start.

- **Social post not sent**
  - Confirm destination toggle enabled.
  - Confirm required token/webhook exists.
  - Review log output for provider-specific failures.

## Data and settings location

App data is saved under:

`%APPDATA%\GWAP Technologies\Multistream Manager\...`

Includes settings, selected services, metadata state, webhooks, window layout, and encrypted tokens.
