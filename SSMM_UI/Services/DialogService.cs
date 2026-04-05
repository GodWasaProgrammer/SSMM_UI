using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using SSMM_UI.RTMP;
using SSMM_UI.Settings;
using SSMM_UI.ViewModel;
using System;
using System.Threading.Tasks;
using SSMM_UI.Views;
using SSMM_UI.Interfaces;
using SSMM_UI.Enums;
using SSMM_UI.Dialogs;
using System.ComponentModel;

namespace SSMM_UI.Services;

public class DialogService : IDialogService
{
    public DialogService(ILogService logService, StateService stateservice)
    {
        _logService = logService;
        _stateservice = stateservice;
    }
    readonly ILogService _logService;
    readonly StateService _stateservice;
    private Window? _chatOverlayWindow;

    public async Task WebhooksView()
    {
        var WebhooksVM = new WebhooksViewModel(_stateservice);
        var WebhooksView = new WebhooksView()
        {
            DataContext = WebhooksVM,
        };
        await WebhooksView.ShowDialog(GetMainWindow()!);
    }

    public async Task<string?> EditSocialPostMessageAsync(string currentMessage)
    {
        var vm = new SocialPostMessageViewModel(currentMessage);
        var view = new SocialPostMessageWindow
        {
            DataContext = vm
        };
        vm.SetHostWindow(view);
        return await view.ShowDialog<string?>(GetMainWindow()!);
    }

    public async Task InspectSelectedService(SelectedService selection)
    {
        if (selection == null)
        {
            return;
        }

        var selectionVM = new SelectedServiceViewModel(selection, _logService, _stateservice);
        var selectionView = new SelectedServiceView
        {
            DataContext = selectionVM
        };
        await selectionView.ShowDialog(GetMainWindow()!);
    }

    public async Task About()
    {
        try
        {
            var aboutViewModel = new AboutViewModel();
            var aboutView = new AboutView(aboutViewModel);

            await aboutView.ShowDialog(GetMainWindow()!);
        }
        catch (Exception ex)
        {
            _logService.Log($"Error showing about dialog: {ex.Message}");
        }
    }

    public async Task<bool> ShowServerDetailsAsync(RtmpServiceGroup group)
    {
        var tcs = new TaskCompletionSource<bool>();
        SelectedService? selectedService = null;

        var detailsWindow = new ServerDetailsWindow(group, (success, streamKey, server, serviceGroup) =>
        {
            if (success && !string.IsNullOrEmpty(streamKey) && server != null)
            {
                // Spara den valda servicen
                selectedService = new SelectedService
                {
                    ServiceGroup = serviceGroup,
                    SelectedServer = server,
                    StreamKey = streamKey
                };
            }
            tcs.SetResult(success);
        });

        await detailsWindow.ShowDialog(GetMainWindow()!);
        var result = await tcs.Task;

        if (result && selectedService != null)
        {
            var mainVM = GetMainWindow();
            if (mainVM != null)
            {
                if (mainVM.DataContext is MainWindowViewModel mainVm)
                {
                    mainVm.LeftSideBarVM.SelectedServicesToStream.Add(selectedService);
                }
            }
        }

        return result;
    }

    public async Task<UserSettings> ShowSettingsDialogAsync(UserSettings currentSettings)
    {
        var viewModel = new SettingsViewModel
        {
            SaveTokens = currentSettings.SaveTokens,
            SaveServices = currentSettings.SaveServices,
            SaveMetaData = currentSettings.SaveMetaData,
            ServerPolling = currentSettings.PollServer,
            StreamFeedPolling = currentSettings.PollStream,
            ChatOverlayEnabled = currentSettings.ChatOverlay.Enabled,
            ChatAlwaysOnTop = currentSettings.ChatOverlay.IsAlwaysOnTop,
            ChatClickThrough = currentSettings.ChatOverlay.IsClickThrough,
            ChatEnableConcatenation = currentSettings.ChatOverlay.EnableConcatenation,
            ChatConcatenationWindowSeconds = currentSettings.ChatOverlay.ConcatenationWindowSeconds,
            ChatMaxMessages = currentSettings.ChatOverlay.MaxMessages,
            ChatMaxConcatenatedLines = currentSettings.ChatOverlay.MaxConcatenatedLines,
            ChatOverlayOpacity = currentSettings.ChatOverlay.Opacity,
            ChatFontScale = currentSettings.ChatOverlay.FontScale
        };

        var dialog = new SettingsDialogView
        {
            DataContext = viewModel
        };

        // Sätt host window-referensen i ViewModel
        viewModel.SetHostWindow(dialog);

        var result = await dialog.ShowDialog<bool?>(GetMainWindow()!);

        if (result == true)
        {
            return new UserSettings
            {
                SaveTokens = viewModel.SaveTokens,
                SaveServices = viewModel.SaveServices,
                SaveMetaData = viewModel.SaveMetaData,
                PollServer = viewModel.ServerPolling,
                PollStream = viewModel.StreamFeedPolling,
                ChatOverlay = new ChatOverlaySettings
                {
                    Enabled = viewModel.ChatOverlayEnabled,
                    IsAlwaysOnTop = viewModel.ChatAlwaysOnTop,
                    IsClickThrough = viewModel.ChatClickThrough,
                    EnableConcatenation = viewModel.ChatEnableConcatenation,
                    ConcatenationWindowSeconds = viewModel.ChatConcatenationWindowSeconds,
                    MaxMessages = viewModel.ChatMaxMessages,
                    MaxConcatenatedLines = viewModel.ChatMaxConcatenatedLines,
                    Opacity = viewModel.ChatOverlayOpacity,
                    FontScale = viewModel.ChatFontScale
                }
            };
        }
        return currentSettings;
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current != null)
        {
            return Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : throw new InvalidOperationException("Application is not desktop");
        }
        return null;
    }

    public async Task DeleteToken(AuthProvider provider, bool result)
    {
        if (result)
        {
            await MessageBox.Show(GetMainWindow()!, $"The token for {provider} has been deleted.");
        }
        else
        {
            await MessageBox.Show(GetMainWindow()!, $"The token for {provider} could not be deleted.");
        }
    }

    public async Task DeleteAllTokens(bool result)
    {
        if (result)
        {
            await MessageBox.Show(GetMainWindow()!, "All tokens have been deleted.");
        }
        else
        {
            await MessageBox.Show(GetMainWindow()!, "Failed to delete all tokens.");
        }
    }

    public async Task PurgeSpecificToken()
    {

        var mw = GetMainWindow()!;
        var vm = new PurgeTokenViewModel(_stateservice.AvailableAuthProviders, _stateservice, this);

        var dialog = new PurgeTokenView
        {
            DataContext = vm
        };
        await dialog.ShowDialog(mw);
    }

    public async Task ShowGettingStartedAsync()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "docs", "GETTING_STARTED.md");
        if (!System.IO.File.Exists(path))
        {
            await MessageBox.Show(GetMainWindow()!, "GETTING_STARTED.md not found in output folder.", "Getting Started");
            return;
        }

        var content = await System.IO.File.ReadAllTextAsync(path);
        var titleBlock = new TextBlock
        {
            Text = "Getting Started Guide",
            FontSize = 18,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var scroll = new ScrollViewer
        {
            Content = new TextBlock
            {
                Text = content,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontFamily = "Consolas, Courier New"
            }
        };

        var contentGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Thickness(12)
        };
        Grid.SetRow(titleBlock, 0);
        Grid.SetRow(scroll, 1);
        contentGrid.Children.Add(titleBlock);
        contentGrid.Children.Add(scroll);

        var window = new Window
        {
            Title = "Getting Started",
            Width = 920,
            Height = 720,
            MinWidth = 720,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = contentGrid
        };

        var owner = GetMainWindow()!;
        if (owner.Icon is not null)
        {
            window.Icon = owner.Icon;
        }

        await window.ShowDialog(owner);
    }

    public async Task ShowChatOverlayAsync()
    {
        var owner = GetMainWindow();
        if (owner is null || owner.DataContext is not MainWindowViewModel mainVm)
        {
            return;
        }

        if (_chatOverlayWindow is not null)
        {
            _chatOverlayWindow.Activate();
            return;
        }

        var overlayWindow = new ChatOverlayWindow
        {
            DataContext = mainVm.ChatOverlayVM,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        if (owner.Icon is not null)
        {
            overlayWindow.Icon = owner.Icon;
        }

        void OverlayClosed(object? sender, EventArgs args)
        {
            if (_chatOverlayWindow is ChatOverlayWindow activeWindow)
            {
                if (activeWindow.DataContext is ChatOverlayViewModel activeVm)
                {
                    activeVm.CloseOverlayRequested -= OnCloseOverlayRequested;
                }

                activeWindow.Closed -= OverlayClosed;
                activeWindow.Closing -= OverlayClosing;
            }

            _chatOverlayWindow = null;
        }

        void OverlayClosing(object? sender, CancelEventArgs args)
        {
            // No-op: ensures window can always close and prevents focus-lock behavior.
        }

        void OnCloseOverlayRequested(object? sender, EventArgs args)
        {
            if (_chatOverlayWindow is Window window)
            {
                window.Close();
            }
        }

        overlayWindow.Closing += OverlayClosing;
        overlayWindow.Closed += OverlayClosed;
        mainVm.ChatOverlayVM.CloseOverlayRequested += OnCloseOverlayRequested;

        _chatOverlayWindow = overlayWindow;
        await mainVm.ChatOverlayVM.RefreshConnectionsAsync();
        overlayWindow.Show();
        overlayWindow.Activate();
    }
}
