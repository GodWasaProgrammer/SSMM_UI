using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using SSMM_UI.RTMP;
using SSMM_UI.Settings;
using SSMM_UI.ViewModel;
using System;
using System.Threading.Tasks;
using SSMM_UI.Views;
using SSMM_UI.Interfaces;

namespace SSMM_UI.Services;

public class DialogService : IDialogService
{
    public DialogService(ILogService logService, StateService stateservice)
    {
        _logService = logService;
        _stateservice = stateservice;
    }
    ILogService _logService;
    StateService _stateservice;

    public async Task OpenSecretsAndKeys()
    {
        var secretsAndKeysVM = new SecretsAndKeysViewModel();
        var secretsandKeysView = new SecretsAndKeysView()
        {
            DataContext = secretsAndKeysVM,
        };
        await secretsandKeysView.ShowDialog(GetMainWindow()!);
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
            StreamFeedPolling = currentSettings.PollStream
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
                PollStream = viewModel.StreamFeedPolling
            };
        }
        return currentSettings;
    }

    private static Window? GetMainWindow()
    {
        if(Application.Current != null)
        {
        return Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : throw new InvalidOperationException("Application is not desktop");
        }
        return null;
    }
}