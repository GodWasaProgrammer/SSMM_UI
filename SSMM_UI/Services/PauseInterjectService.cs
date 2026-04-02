using SSMM_UI.RTMP;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class PauseInterjectService
{
    private readonly ILogService _logger;
    private string? _defaultPauseMediaPath;

    public PauseInterjectService(ILogService logger)
    {
        _logger = logger;
    }

    public void SetDefaultPauseMedia(string mediaPath)
    {
        if (!File.Exists(mediaPath))
        {
            throw new FileNotFoundException($"Pause media file not found: {mediaPath}");
        }
        _defaultPauseMediaPath = mediaPath;
        _logger.Log($"Default pause media set to: {mediaPath}");
    }

    public string? GetDefaultPauseMedia() => _defaultPauseMediaPath;

    public bool ValidateMediaFile(string mediaPath)
    {
        if (!File.Exists(mediaPath))
        {
            _logger.Log($"Media file not found: {mediaPath}");
            return false;
        }

        var extension = Path.GetExtension(mediaPath).ToLowerInvariant();
        var validExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".flv", ".png", ".jpg", ".jpeg" };

        if (!validExtensions.Contains(extension))
        {
            _logger.Log($"Invalid media file extension: {extension}. Supported formats: {string.Join(", ", validExtensions)}");
            return false;
        }

        return true;
    }

    public async Task<Process?> StartPauseStream(
        string inputMediaPath,
        string outputUrl,
        string serviceName,
        int? maxVideoBitRate = null,
        int? maxAudioBitRate = null,
        int? keyInt = null)
    {
        if (!ValidateMediaFile(inputMediaPath))
        {
            return null;
        }

        var path = "Dependencies/ffmpeg";
        var args = new StringBuilder();

        // Use -stream_loop -1 for videos to loop indefinitely, or -loop 1 for images
        var extension = Path.GetExtension(inputMediaPath).ToLowerInvariant();
        var isImage = new[] { ".png", ".jpg", ".jpeg" }.Contains(extension);

        if (isImage)
        {
            args.Append($"-loop 1 -i \"{inputMediaPath}\" ");
            // For images, we need to set a frame rate
            args.Append("-r 30 ");
        }
        else
        {
            args.Append($"-stream_loop -1 -re -i \"{inputMediaPath}\" ");
        }

        // Video codec - copy if possible, otherwise encode
        args.Append("-c:v libx264 ");

        // Video bitrate
        if (maxVideoBitRate.HasValue)
        {
            args.Append($"-b:v {maxVideoBitRate.Value}k ");
        }
        else
        {
            args.Append("-b:v 2500k "); // Default bitrate
        }

        // Keyframe interval
        if (keyInt.HasValue)
        {
            args.Append($"-g {keyInt.Value} ");
        }
        else
        {
            args.Append("-g 60 "); // Default keyframe interval
        }

        // Audio handling
        if (isImage)
        {
            // For images, generate silent audio
            args.Append("-f lavfi -i anullsrc=channel_layout=stereo:sample_rate=44100 ");
            args.Append("-c:a aac ");
            if (maxAudioBitRate.HasValue)
            {
                args.Append($"-b:a {maxAudioBitRate.Value}k ");
            }
            else
            {
                args.Append("-b:a 128k ");
            }
        }
        else
        {
            // For videos, copy or encode audio
            args.Append("-c:a aac ");
            if (maxAudioBitRate.HasValue)
            {
                args.Append($"-b:a {maxAudioBitRate.Value}k ");
            }
            else
            {
                args.Append("-b:a 128k ");
            }
        }

        // Output format
        args.Append($"-f flv \"{outputUrl}\"");

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = args.ToString(),
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            var process = new Process { StartInfo = startInfo };
            process.Start();
            _logger.Log($"Started pause stream for {serviceName} with media: {inputMediaPath}");
            return process;
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to start pause stream: {ex.Message}");
            return null;
        }
    }

    public void StopPauseStream(Process? process, string serviceName)
    {
        if (process != null && !process.HasExited)
        {
            try
            {
                // Send 'q' to FFmpeg to quit gracefully
                process.StandardInput.WriteLine("q");
                process.StandardInput.Flush();

                // Wait a bit for graceful shutdown
                if (!process.WaitForExit(2000))
                {
                    process.Kill();
                }

                _logger.Log($"Stopped pause stream for {serviceName}");
            }
            catch (Exception ex)
            {
                _logger.Log($"Error stopping pause stream for {serviceName}: {ex.Message}");
            }
        }
    }
}
