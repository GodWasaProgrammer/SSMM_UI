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
                                  LoginViewModel loginVM)
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
        // init our settings
        _settings = _stateService.UserSettingsObj;
        _pollService = pollService;

        // ==== OutPut Streams ====
        StartStreamCommand = new AsyncRelayCommand(StartStream);
        StopStreamsCommand = new RelayCommand(OnStopStreams);

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

                await _streamService.StartStream(CurrentMetaData, ActiveServices);
                _logService.Log("Started streaming...");
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
}