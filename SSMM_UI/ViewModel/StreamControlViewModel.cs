using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.MetaData;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class StreamControlViewModel : ObservableObject
{
    public StreamControlViewModel(LogViewModel logVM, ILogService logger, LeftSideBarViewModel leftSideBarViewModel, StreamService streamservice, MetaDataService mdService, StateService stateservice, BroadCastService broadCastService, PollService pollService)
    {
        // set Viewmodels
        LogVM = logVM;
        LeftSideBarViewModel = leftSideBarViewModel;


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
        _ = Initialize();
    }

    public void fetchCurrentMetaData()
    {
        if (_stateService != null)
        {

        }
    }

    // == child models ==
    readonly LogViewModel LogVM;
    public LeftSideBarViewModel LeftSideBarViewModel { get; }

    // ==== RTMP Server and internal RTMP feed from OBS Status ====
    [ObservableProperty] private string serverStatusText = "Stream status: ❌ Not Receiving";
    [ObservableProperty] private string _serverStatus = "RTMP-server: ❌ Not Running";
    [ObservableProperty] private string streamStatusText = "Stream status: ❌ Not Receiving";

    // ==== Services ====
    ILogService _logService;
    StreamService _streamService;
    MetaDataService _mdService;
    StateService _stateService;
    BroadCastService _broadCastService;
    PollService _pollService;


    // Settings
    UserSettings _settings;

    // == bool toggler for stopping your output streams ==
    [ObservableProperty] private bool canStopStream = false;
    [ObservableProperty] private bool canStartStream = true;

    // === MetaData === 
    [ObservableProperty] StreamMetadata currentMetaData;

    // == Output Controls ==
    public ICommand StartStreamCommand { get; }
    public ICommand StopStreamsCommand { get; }


    private async Task Initialize()
    {
        if (_settings.PollStream && _settings.PollServer)
        {
            SubscribeToEvents();
            if (_pollService != null)
                _pollService.StartPolling();
        }
        else
        {
            ServerStatusText = "Polling is turned off for RTMP Server";
            StreamStatusText = "Polling is turned off for incoming Stream";
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
                if (LeftSideBarViewModel.YTService != null)
                {
                    _broadCastService.CreateYTService(LeftSideBarViewModel.YTService);
                    _mdService.CreateYouTubeService(LeftSideBarViewModel.YTService);
                }
                CurrentMetaData = _stateService.GetCurrentMetaData();
                await _streamService.StartStream(CurrentMetaData, LeftSideBarViewModel.SelectedServicesToStream);
                _logService.Log("Started streaming...");
            }
            catch (Exception ex)
            {
                _logService.Log(ex.ToString());
            }
            var bla = _streamService.ProcessInfos;
            LogVM.StreamOutputVM.Outputs.Clear();
            foreach (var info in bla)
            {
                var outputview = new OutputViewModel(info.Header, info.Process);
                LogVM.StreamOutputVM.Outputs.Add(outputview);
            }
        }
    }

    private void OnStopStreams()
    {
        try
        {
            if (_streamService != null)
                _streamService.StopStreams();
            _logService.Log("Stopped all streams.");
            CanStartStream = true;
            CanStopStream = false;
        }
        catch (Exception ex)
        {
            _logService.Log(ex.ToString());
        }
    }
}