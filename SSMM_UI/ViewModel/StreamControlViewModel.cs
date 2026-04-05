using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.MetaData;
using SSMM_UI.RTMP;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class StreamControlViewModel : ObservableObject
{
    public StreamControlViewModel(LogViewModel logVM,
                                  ILogService logger,
                                  LeftSideBarViewModel leftSideBarViewModel,
                                  StreamService streamservice,
                                  MetaDataService mdService,
                                  StateService stateservice,
                                  BroadCastService broadCastService,
                                  PollService pollService,
                                  SocialPosterService socialposterservice,
                                  LoginViewModel loginVM,
                                  PauseInterjectService pauseInterjectService)
    {
        // set Viewmodels
        LogVM = logVM;
        LeftSideBarViewModel = leftSideBarViewModel;
        LoginVM = loginVM;


        // Services
        _logService = logger;
        _streamService = streamservice;
        _mdService = mdService;
        _stateService = stateservice;
        _broadCastService = broadCastService;
        _socialPosterService = socialposterservice;
        _pauseInterjectService = pauseInterjectService;
        // init our settings
        _settings = _stateService.UserSettingsObj;
        _pollService = pollService;

        // ==== OutPut Streams ====
        StartStreamCommand = new AsyncRelayCommand(StartStream);
        StopStreamsCommand = new RelayCommand(OnStopStreams);
        PauseStreamsCommand = new AsyncRelayCommand(PauseStreams);
        ResumeStreamsCommand = new AsyncRelayCommand(ResumeStreams);
        SetPauseMediaCommand = new AsyncRelayCommand(SetPauseMedia);

        // Fire and forget
        Initialize();
    }

    // == child models ==
    readonly LogViewModel LogVM;
    public LeftSideBarViewModel LeftSideBarViewModel { get; }
    public LoginViewModel LoginVM { get; }

    // ==== RTMP Server and internal RTMP feed from OBS Status ====
    [ObservableProperty] private string serverStatusText = "Stream status: ❌ Not Receiving";
    [ObservableProperty] private string _serverStatus = "RTMP-server: ❌ Not Running";
    [ObservableProperty] private string streamStatusText = "Stream status: ❌ Not Receiving";

    // ==== Services ====
    readonly ILogService _logService;
    readonly StreamService _streamService;
    readonly MetaDataService _mdService;
    readonly StateService _stateService;
    readonly BroadCastService _broadCastService;
    readonly PollService _pollService;
    readonly SocialPosterService _socialPosterService;
    readonly PauseInterjectService _pauseInterjectService;

    // Settings
    readonly UserSettings _settings;

    // == bool toggler for stopping your output streams ==
    [ObservableProperty] private bool canStopStream = false;
    [ObservableProperty] private bool canStartStream = true;

    // === MetaData === 
    [ObservableProperty] StreamMetadata? currentMetaData;

    // == Output Controls ==
    public ICommand StartStreamCommand { get; }
    public ICommand StopStreamsCommand { get; }
    public ICommand PauseStreamsCommand { get; }
    public ICommand ResumeStreamsCommand { get; }
    public ICommand SetPauseMediaCommand { get; }


    private void Initialize()
    {
        SubscribeToEvents();
        if (_settings.PollStream)
        {
            _pollService?.StartStreamPolling();
        }
        else
        {
            StreamStatusText = "Polling is turned off for incoming Stream";
        }
        if (_settings.PollServer)
        {
            _pollService?.StartServerPolling();
        }
        else
        {
            ServerStatusText = "Polling is turned off for RTMP Server";
        }
    }

    private void SubscribeToEvents()
    {
        try
        {
            if (_pollService != null)
            {
                _pollService.ServerStatusChanged += isAlive =>
                    ServerStatusText = isAlive ? "RTMP-server: ✅ Running" : "RTMP-server: ❌ Not Running";

                _pollService.StreamStatusChanged += isAlive =>
                    StreamStatusText = isAlive ? "Stream status: ✅ Live" : "Stream status: ❌ Not Receiving";
            }
            else
            {
                throw new Exception("streamservice was null");
            }
        }
        catch (Exception ex)
        {
            _logService.Log(ex.Message);
        }
    }

    //private async Task<bool> TriggerSocialPosterAsync(bool success)
    //{
    //    if(success)
    //    {
    //        await _socialPosterService.RunPoster(_settings.PostToDiscord, _settings.PostToFB, _settings.PostToX);
    //    }
    //}

    private async Task StartStream()
    {
        CanStartStream = false;
        CanStopStream = true;
        if (_streamService != null)
        {
            try
            {
                if (LoginVM.YTService != null)
                {
                    _broadCastService.CreateYTService(LoginVM.YTService);
                    _mdService.CreateYouTubeService(LoginVM.YTService);
                }
                CurrentMetaData = _stateService.GetCurrentMetaData();

                // deduce which we should start

                var ActiveServices = new ObservableCollection<SelectedService>(LeftSideBarViewModel.SelectedServicesToStream.Where(x => x.IsActive).ToList());
                var hasYouTubeOutput = ActiveServices.Any(IsYouTubeService);
                TaskCompletionSource<bool>? youtubeLiveSignal = null;
                if (_settings.AutoPost && hasYouTubeOutput)
                {
                    youtubeLiveSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                if (ActiveServices.Count == 0)
                {
                    _logService.Log("No active services selected to stream.");
                    CanStartStream = true;
                    CanStopStream = false;
                    return;
                }

                foreach (var service in ActiveServices)
                {
                    if (CurrentMetaData == null)
                    {
                        break;
                    }

                    var serviceName = service.ServiceGroup?.ServiceName ?? service.DisplayName;
                    var metadataResult = await _broadCastService.ApplyMetadataForServiceAsync(serviceName, CurrentMetaData);
                    _logService.Log(metadataResult);
                }

                await _streamService.StartStream(
                    CurrentMetaData,
                    ActiveServices,
                    youtubeLiveSignal is null ? null : isLive => youtubeLiveSignal.TrySetResult(isLive));
                
                _logService.Log("Started streaming...");

                if (_settings.AutoPost)
                {
                    await TryAutoPostAsync(ActiveServices, youtubeLiveSignal?.Task);
                }
            }
            catch (Exception ex)
            {
                _logService.Log(ex.ToString());
            }
            var bla = _streamService.ProcessInfos;
            if (LogVM != null)
            {
                if (LogVM.StreamOutputVM != null)
                {
                    if (LogVM.StreamOutputVM.Outputs != null)
                    {
                        //LogVM.StreamOutputVM.Outputs.Clear();
                        foreach (var info in bla)
                        {
                            if (info != null)
                            {
                                if (info.Header != null)
                                {
                                    if (info.Process != null)
                                    {
                                        var outputview = new OutputViewModel(info.Header, info.Process);
                                        LogVM.StreamOutputVM.Outputs.Add(outputview);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
       // await _socialPosterService.RunPoster(_settings.PostToDiscord, _settings.PostToFB, _settings.PostToX);
    }

    private void OnStopStreams()
    {
        try
        {
            _streamService?.StopStreams();
            _logService.Log("Stopped all streams.");
            CanStartStream = true;
            CanStopStream = false;
            if (LogVM.StreamOutputVM != null)
            {
                if (LogVM.StreamOutputVM.Outputs != null)
                {
                    foreach (var output in LogVM.StreamOutputVM.Outputs)
                    {
                        output.Dispose();
                    }
                    LogVM.StreamOutputVM.Outputs.Clear();
                    _streamService?.ProcessInfos.Clear();
                }
            }
       }
       catch (Exception ex)
       {
            _logService.Log(ex.ToString());
        }
    }

    private static bool IsYouTubeService(SelectedService service)
    {
        var name = service.ServiceGroup?.ServiceName ?? service.DisplayName;
        return name.Contains("youtube", StringComparison.OrdinalIgnoreCase);
    }

    private async Task TryAutoPostAsync(ObservableCollection<SelectedService> activeServices, Task<bool>? youtubeLiveSignalTask)
    {
        if (!_settings.PostToDiscord && !_settings.PostToFB && !_settings.PostToX)
        {
            _logService.Log("Auto-posting skipped: no destinations selected.");
            return;
        }

        if (youtubeLiveSignalTask != null)
        {
            _logService.Log("Auto-post waiting for YouTube live transition...");
            bool youtubeLive = false;
            try
            {
                youtubeLive = await youtubeLiveSignalTask.WaitAsync(TimeSpan.FromMinutes(9));
            }
            catch (TimeoutException)
            {
                _logService.Log("Auto-post timed out waiting for YouTube transition signal.");
            }

            if (youtubeLive)
            {
                // Give APIs a short propagation window after lifecycle reaches live.
                await Task.Delay(TimeSpan.FromSeconds(15));
            }
            else
            {
                _logService.Log("YouTube did not confirm live state before auto-post checks; continuing with live-detection retries.");
            }
        }

        const int maxAttempts = 6;
        var attemptDelay = TimeSpan.FromSeconds(15);
        try
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var result = await _socialPosterService.RunPoster(_settings.PostToDiscord, _settings.PostToFB, _settings.PostToX, _settings.CustomSocialMessage);
                if (result.PostedAny && result.PostedTo.Count > 0)
                {
                    _logService.Log($"Auto-posted to: {string.Join(", ", result.PostedTo)}.");
                    return;
                }

                var reason = result.SkippedReasons.Count > 0 ? string.Join("; ", result.SkippedReasons) : "No destinations accepted the post.";
                var noLiveDetected = result.SkippedReasons.Any(r => r.Contains("No live platforms detected.", StringComparison.OrdinalIgnoreCase));
                if (!noLiveDetected || attempt == maxAttempts)
                {
                    _logService.Log($"Auto-post triggered but nothing was sent. {reason}");
                    return;
                }

                _logService.Log($"Auto-post delayed (attempt {attempt}/{maxAttempts}): waiting for platform live detection.");
                await Task.Delay(attemptDelay);
            }
        }
        catch (Exception ex)
        {
            _logService.Log($"Social poster failed: {ex}");
        }
    }

    private async Task PauseStreams()
    {
        try
        {
            var activeServices = new ObservableCollection<SelectedService>(
                LeftSideBarViewModel.SelectedServicesToStream.Where(x => x.IsActive).ToList());

            foreach (var processInfo in _streamService.ProcessInfos)
            {
                var serviceName = processInfo.Header;
                if (!string.IsNullOrEmpty(serviceName))
                {
                    await _streamService.PauseStreamToService(serviceName, activeServices);
                }
            }

            _logService.Log("All active streams have been paused");
        }
        catch (Exception ex)
        {
            _logService.Log($"Error pausing streams: {ex.Message}");
        }
    }

    private async Task ResumeStreams()
    {
        try
        {
            var activeServices = new ObservableCollection<SelectedService>(
                LeftSideBarViewModel.SelectedServicesToStream.Where(x => x.IsActive).ToList());

            await _streamService.ResumeStream(activeServices);

            _logService.Log("All paused streams have been resumed");
        }
        catch (Exception ex)
        {
            _logService.Log($"Error resuming streams: {ex.Message}");
        }
    }

    private async Task SetPauseMedia()
    {
        try
        {
            // This would typically open a file picker dialog
            // For now, we'll just log that this functionality is available
            _logService.Log("Use PauseInterjectService.SetDefaultPauseMedia(path) to configure pause media");
            _logService.Log("Supported formats: .mp4, .mov, .avi, .mkv, .flv, .png, .jpg, .jpeg");
        }
        catch (Exception ex)
        {
            _logService.Log($"Error setting pause media: {ex.Message}");
        }
    }
}
