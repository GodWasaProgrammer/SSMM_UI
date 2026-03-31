using SSMM_UI.RTMP;
using SSMM_UI.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace SSMM_UI.Examples;

/// <summary>
/// Example demonstrating how to use the Pause/Interject functionality
/// </summary>
public class PauseInterjectExample
{
    private readonly StreamService _streamService;
    private readonly PauseInterjectService _pauseInterjectService;
    private readonly ILogService _logger;

    public PauseInterjectExample(
        StreamService streamService,
        PauseInterjectService pauseInterjectService,
        ILogService logger)
    {
        _streamService = streamService;
        _pauseInterjectService = pauseInterjectService;
        _logger = logger;
    }

    /// <summary>
    /// Example 1: Basic pause/resume workflow
    /// </summary>
    public async Task BasicPauseResumeExample(ObservableCollection<SelectedService> activeServices)
    {
        // Set up the default pause media
        var pauseVideoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "pause_screen.mp4");
        _pauseInterjectService.SetDefaultPauseMedia(pauseVideoPath);

        _logger.Log("Streaming live...");
        await Task.Delay(TimeSpan.FromMinutes(5)); // Stream for 5 minutes

        // Take a break - pause all streams
        _logger.Log("Taking a 2-minute break...");
        await _streamService.PauseStream();

        await Task.Delay(TimeSpan.FromMinutes(2)); // 2-minute break

        // Resume streaming
        _logger.Log("Resuming live stream...");
        await _streamService.ResumeStream(activeServices);
    }

    /// <summary>
    /// Example 2: Pause specific services with custom media
    /// </summary>
    public async Task CustomPauseMediaExample(ObservableCollection<SelectedService> activeServices)
    {
        // Pause YouTube with a custom video
        var youtubePauseVideo = "Assets/youtube_pause.mp4";
        if (_pauseInterjectService.ValidateMediaFile(youtubePauseVideo))
        {
            await _streamService.PauseStreamToService("Youtube", activeServices, youtubePauseVideo);
            _logger.Log("YouTube paused with custom video");
        }

        // Pause Twitch with a different custom video
        var twitchPauseVideo = "Assets/twitch_pause.mp4";
        if (_pauseInterjectService.ValidateMediaFile(twitchPauseVideo))
        {
            await _streamService.PauseStreamToService("Twitch", activeServices, twitchPauseVideo);
            _logger.Log("Twitch paused with custom video");
        }

        // Wait, then resume all
        await Task.Delay(TimeSpan.FromSeconds(30));
        await _streamService.ResumeStream(activeServices);
    }

    /// <summary>
    /// Example 3: Using static images for pause screens
    /// </summary>
    public async Task StaticImagePauseExample(ObservableCollection<SelectedService> activeServices)
    {
        // Set a static image as pause screen
        var pauseImagePath = "Assets/brb_screen.png";
        _pauseInterjectService.SetDefaultPauseMedia(pauseImagePath);

        // Pause with the image
        await _streamService.PauseStream();
        _logger.Log("All streams showing 'Be Right Back' image");

        // Image will loop with silent audio automatically
        await Task.Delay(TimeSpan.FromMinutes(1));

        // Resume
        await _streamService.ResumeStream(activeServices);
    }

    /// <summary>
    /// Example 4: Checking pause state
    /// </summary>
    public void CheckPauseStateExample()
    {
        foreach (var processInfo in _streamService.ProcessInfos)
        {
            if (processInfo.IsPaused)
            {
                var pauseDuration = DateTime.UtcNow - processInfo.PauseStartTime;
                _logger.Log($"{processInfo.Header} has been paused for {pauseDuration?.TotalMinutes:F1} minutes");
                _logger.Log($"  Using media: {processInfo.InterjectMediaPath}");
            }
            else
            {
                _logger.Log($"{processInfo.Header} is streaming live");
            }
        }
    }

    /// <summary>
    /// Example 5: Scheduled pause (e.g., for ads or announcements)
    /// </summary>
    public async Task ScheduledPauseExample(ObservableCollection<SelectedService> activeServices)
    {
        // Stream live for 30 minutes
        _logger.Log("Starting stream with scheduled pause...");
        await Task.Delay(TimeSpan.FromMinutes(30));

        // Pause for a 1-minute ad/announcement
        var adVideoPath = "Assets/announcement.mp4";
        _pauseInterjectService.SetDefaultPauseMedia(adVideoPath);

        _logger.Log("Showing scheduled announcement...");
        await _streamService.PauseStream();

        await Task.Delay(TimeSpan.FromMinutes(1));

        // Resume live content
        _logger.Log("Returning to live content...");
        await _streamService.ResumeStream(activeServices);
    }

    /// <summary>
    /// Example 6: Error handling for pause operations
    /// </summary>
    public async Task<bool> SafePauseResumeExample(ObservableCollection<SelectedService> activeServices)
    {
        try
        {
            // Validate pause media exists
            var pauseMedia = _pauseInterjectService.GetDefaultPauseMedia();
            if (string.IsNullOrEmpty(pauseMedia))
            {
                _logger.Log("ERROR: No default pause media configured!");
                return false;
            }

            if (!_pauseInterjectService.ValidateMediaFile(pauseMedia))
            {
                _logger.Log($"ERROR: Invalid pause media file: {pauseMedia}");
                return false;
            }

            // Attempt pause
            var pauseSuccess = await _streamService.PauseStream();
            if (!pauseSuccess)
            {
                _logger.Log("ERROR: Failed to pause streams");
                return false;
            }

            _logger.Log("Successfully paused all streams");

            // Wait
            await Task.Delay(TimeSpan.FromSeconds(30));

            // Attempt resume
            var resumeSuccess = await _streamService.ResumeStream(activeServices);
            if (!resumeSuccess)
            {
                _logger.Log("ERROR: Failed to resume streams");
                return false;
            }

            _logger.Log("Successfully resumed all streams");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log($"EXCEPTION during pause/resume: {ex.Message}");
            return false;
        }
    }
}
