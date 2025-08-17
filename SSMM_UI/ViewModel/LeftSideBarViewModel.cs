using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Apis.YouTube.v3;
using SSMM_UI.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel
{
    public partial class LeftSideBarViewModel : ObservableObject
    {
        public LeftSideBarViewModel(IDialogService dialogService, VideoPlayerService vidPlayer, CentralAuthService authservice, ILogService logService, StateService stateService)
        {

            // ==== Stream Inspection window ====
            ToggleReceivingStreamCommand = new RelayCommand(ToggleReceivingStream);

            // ==== Selected Services controls ====
            AddServiceCommand = new AsyncRelayCommand<RtmpServiceGroup>(OnRTMPServiceSelected);
            RemoveSelectedServiceCommand = new RelayCommand(RemoveSelectedService);

            // ==== Login ====
            LoginWithGoogleCommand = new AsyncRelayCommand(OnLoginWithGoogleClicked);
            LoginWithKickCommand = new AsyncRelayCommand(LoginWithKick);
            LoginWithTwitchCommand = new AsyncRelayCommand(LoginWithTwitch);

            // ==== Service assignment ====
            _dialogService = dialogService;
            _videoPlayerService = vidPlayer;
            _centralAuthService = authservice;
            _logService = logService;
            _stateService = stateService;
            RtmpServiceGroups = _stateService.RtmpServiceGroups;
            SelectedServicesToStream = _stateService.SelectedServicesToStream;

            _ = Initialize();
        }


        public async Task Initialize()
        {
            await AutoLoginIfTokenized();
        }

        public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; } = [];
        public ObservableCollection<SelectedService> SelectedServicesToStream { get; } = [];



        // == Login ===
        public ICommand LoginWithGoogleCommand { get; }
        public ICommand LoginWithKickCommand { get; }
        public ICommand LoginWithTwitchCommand { get; }

        // == Internal stream inspection toggle ==
        public ICommand ToggleReceivingStreamCommand { get; }
        // == Service Selections ==
        public IAsyncRelayCommand<RtmpServiceGroup> AddServiceCommand { get; }
        public ICommand RemoveSelectedServiceCommand { get; }


        // ==== Services ====
        private readonly CentralAuthService _centralAuthService;
        private readonly VideoPlayerService _videoPlayerService;
        private readonly IDialogService _dialogService;
        private readonly ILogService _logService;
        private YouTubeService? YTService;
        private readonly StateService _stateService;

        // ==== Login Status ====
        [ObservableProperty] private string youtubeLoginStatus = "";
        [ObservableProperty] private string kickLoginStatus = "";
        [ObservableProperty] private string twitchLoginStatus = "";

        // == bool toggler for the preview window for stream ==
        [ObservableProperty] private bool isReceivingStream;
        [ObservableProperty] private RtmpServiceGroup selectedRtmpService;

        // ==== Service Selections ====
        [ObservableProperty]
        private SelectedService? _selectedService;

        private async Task OnRTMPServiceSelected(RtmpServiceGroup group)
        {
            var result = await _dialogService.ShowServerDetailsAsync(group);

            if (!result)
                _logService.Log($"Cancelled adding service: {group.ServiceName}\n");
        }

        [ObservableProperty] private string streamButtonText = "Start Receiving";
        private void ToggleReceivingStream()
        {
            IsReceivingStream = !IsReceivingStream;
            _videoPlayerService.ToggleVisibility(IsReceivingStream);
            StreamButtonText = IsReceivingStream ? "Stop Receiving" : "Start Receiving";
        }

        private void RemoveSelectedService()
        {
            if (SelectedService == null)
            {
                _logService.Log("Ingen tjänst vald att ta bort");
                return;
            }

            if (!SelectedServicesToStream.Contains(SelectedService))
            {
                _logService.Log("Den valda tjänsten finns inte i listan");
                return;
            }

            var serviceName = SelectedService.DisplayName;
            SelectedServicesToStream.Remove(SelectedService);
            _logService.Log($"Tog bort tjänst: {serviceName}");
            SelectedService = null;
        }

        private async Task OnLoginWithGoogleClicked()
        {

            YoutubeLoginStatus = "Loggar in...";

            var (userName, ytservice) = await _centralAuthService.LoginWithYoutube();
            if (ytservice != null)
            {
                YTService = ytservice;
            }
            if (userName != null)
            {
                YoutubeLoginStatus = $"✅ Inloggad som {userName}";
            }
            else
            {
                YoutubeLoginStatus = "Inloggning misslyckades";
            }
        }

        private async Task LoginWithTwitch()
        {
            TwitchLoginStatus = "Logging in...";
            TwitchLoginStatus = await _centralAuthService.LoginWithTwitch();
        }

        private async Task LoginWithKick()
        {
            KickLoginStatus = "Logging in...";
            KickLoginStatus = await _centralAuthService.LoginWithKick();
        }

        private async Task AutoLoginIfTokenized()
        {
            var (results, ytService) = await _centralAuthService.TryAutoLoginAllAsync();

            if (ytService != null)
            {
                YTService = ytService;
            }
            foreach (var result in results)
            {
                var message = result.Success
                    ? $"✅ Logged in as: {result.Username}"
                    : $"❌ {result.ErrorMessage}";

                switch (result.Provider)
                {
                    case AuthProvider.Twitch:
                        TwitchLoginStatus = message;
                        break;
                    case AuthProvider.YouTube:
                        YoutubeLoginStatus = message;
                        break;
                    case AuthProvider.Kick:
                        KickLoginStatus = message;
                        break;
                }
            }
        }
    }
}