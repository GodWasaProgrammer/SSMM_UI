# Pause/Interject Functionality

## Overview

The Multistream Manager now supports pausing live streams and injecting custom video or static image content into all running FFmpeg streams. This allows you to temporarily interrupt your broadcast with a "pause screen" without ending the stream session.

## Features

- **Video Injection**: Loop a video file (MP4, MOV, AVI, MKV, FLV) during pauses
- **Image Injection**: Display a static image (PNG, JPG, JPEG) with silent audio
- **Seamless Switching**: Streams transition smoothly between live and pause states
- **Per-Service Control**: Pause individual streaming services or all at once
- **State Tracking**: Each stream tracks its pause state, media path, and timing

## Architecture

### Components

1. **PauseInterjectService**: Manages pause media configuration and FFmpeg process creation
2. **StreamService**: Orchestrates pause/resume operations across all streams
3. **StreamProcessInfo**: Tracks pause state for each stream (IsPaused, InterjectMediaPath, PauseStartTime)

### How It Works

When you pause a stream:
1. The current live FFmpeg process is terminated
2. A new FFmpeg process starts, streaming the pause media (video/image) on loop
3. The stream continues to broadcast to platforms with the pause content
4. Stream metadata is updated to track the paused state

When you resume:
1. The pause media FFmpeg process is gracefully stopped
2. A new live FFmpeg process restarts with the original RTMP input
3. The stream seamlessly returns to live broadcasting

## Usage

### Setting Default Pause Media

Before using pause functionality, configure a default pause media file:

```csharp
// Get the service from DI container
var pauseService = App.Services.GetRequiredService<PauseInterjectService>();

// Set default pause video
pauseService.SetDefaultPauseMedia("/path/to/pause_screen.mp4");

// Or set a static image
pauseService.SetDefaultPauseMedia("/path/to/pause_image.png");
```

### Pausing All Streams

```csharp
var streamService = App.Services.GetRequiredService<StreamService>();
var activeServices = GetActiveServices(); // Your ObservableCollection<SelectedService>

// Pause all active streams with default media
await streamService.PauseStream();

// Or use custom media for this pause
await streamService.PauseStream("/path/to/custom_pause.mp4");
```

### Pausing a Specific Service

```csharp
// Pause only YouTube stream
await streamService.PauseStreamToService("Youtube", activeServices);

// Pause with custom media
await streamService.PauseStreamToService("Twitch", activeServices, "/path/to/custom.mp4");
```

### Resuming Streams

```csharp
// Resume all paused streams back to live
await streamService.ResumeStream(activeServices);
```

### UI Integration

Commands are available in `StreamControlViewModel`:

```csharp
// In your UI button bindings
Command="{Binding PauseStreamsCommand}"  // Pause all streams
Command="{Binding ResumeStreamsCommand}"  // Resume all streams
Command="{Binding SetPauseMediaCommand}"  // Configure pause media
```

## Supported Media Formats

### Video Files
- MP4 (recommended)
- MOV
- AVI
- MKV
- FLV

### Image Files
- PNG
- JPG/JPEG

**Note**: Images automatically generate silent audio to maintain stream compatibility.

## FFmpeg Arguments

### For Videos
```bash
-stream_loop -1        # Loop video indefinitely
-re                    # Read input at native frame rate
-i "video.mp4"         # Input video
-c:v libx264          # Encode with H.264
-b:v 2500k            # Video bitrate (service settings applied)
-g 60                 # Keyframe interval
-c:a aac              # AAC audio codec
-b:a 128k             # Audio bitrate (service settings applied)
-f flv                # FLV output format
"rtmp://destination"  # Output URL
```

### For Images
```bash
-loop 1               # Loop the image
-i "image.png"        # Input image
-r 30                 # Frame rate (30fps)
-f lavfi              # Generate audio filter
-i anullsrc=...      # Silent audio source
-c:v libx264          # Encode with H.264
-b:v 2500k            # Video bitrate
-c:a aac              # AAC audio codec
-b:a 128k             # Audio bitrate
-f flv                # FLV output format
"rtmp://destination"  # Output URL
```

## Best Practices

1. **Pre-configure Media**: Set your default pause media before starting streams
2. **Test First**: Test pause/resume with a single service before using on all streams
3. **Media Quality**: Use pause media that matches or exceeds your stream quality settings
4. **Video Length**: For looping videos, shorter clips (5-30 seconds) work best
5. **Aspect Ratio**: Match your pause media aspect ratio to your stream resolution (16:9 recommended)

## Example Workflow

```csharp
// 1. Configure pause media on application startup
var pauseService = App.Services.GetRequiredService<PauseInterjectService>();
pauseService.SetDefaultPauseMedia("Assets/pause_screen.mp4");

// 2. Start streaming normally
await streamService.StartStream(metadata, activeServices);

// 3. When you need a break, pause all streams
await streamService.PauseStream();
// Viewers now see your pause screen

// 4. When ready, resume live streaming
await streamService.ResumeStream(activeServices);
// Viewers are back to live content
```

## Troubleshooting

### Pause media not playing
- Verify file path is correct and file exists
- Check file format is supported
- Review logs for FFmpeg errors

### Stream disconnects during pause
- Ensure pause media duration is sufficient
- Check bitrate settings match platform requirements
- Verify FFmpeg is installed in Dependencies/ffmpeg

### Audio issues with images
- Silent audio is automatically generated for images
- If platforms reject the stream, try a short video loop instead

## Technical Details

### Process Management

The service maintains two process lists:
- `ffmpegProcess`: Active live streaming processes
- `pauseProcesses`: Active pause media streaming processes

Only one type runs per service at a time, ensuring clean state transitions.

### State Tracking

Each `StreamProcessInfo` tracks:
- `IsPaused`: Boolean indicating current state
- `InterjectMediaPath`: Path to the pause media being used
- `PauseStartTime`: When the pause began (for analytics/logging)

### Graceful Shutdown

Pause processes are stopped gracefully when possible:
1. Send 'q' command to FFmpeg stdin
2. Wait 2 seconds for graceful exit
3. Force kill if process doesn't exit

This ensures clean stream transitions and prevents corruption.

## Future Enhancements

Potential improvements for this feature:
- Schedule automatic pauses
- Playlist support for multiple pause screens
- Fade transitions between live and pause
- Real-time pause media switching without stream interruption
- Analytics on pause duration and frequency
