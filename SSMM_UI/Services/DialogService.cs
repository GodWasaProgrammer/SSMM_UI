using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using SSMM_UI.RTMP;
using SSMM_UI.Settings;
using SSMM_UI.ViewModel;
using System;
using System.Threading.Tasks;
using SSMM_UI.Views;

namespace SSMM_UI.Services;

public class DialogService : IDialogService
{
    public DialogService()
    {

    }
    public async Task<bool> ShowServerDetailsAsync(RtmpServiceGroup group)
    {
        var tcs = new TaskCompletionSource<bool>();
        SelectedService selectedService = null;

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

        await detailsWindow.ShowDialog(GetMainWindow());
        var result = await tcs.Task;

        // EFTER att dialogen stängts - lägg till i huvud-viewmodel
        if (result && selectedService != null)
        {
            var mainVM = GetMainWindow();
            if (mainVM.DataContext is MainWindowViewModel mainVm)
            {
                mainVm.LeftSideBarVM.SelectedServicesToStream.Add(selectedService);
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

        var result = await dialog.ShowDialog<bool?>(GetMainWindow());

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

    private Window GetMainWindow()
    {
        return Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : throw new InvalidOperationException("Application is not desktop");
    }
}
public interface IDialogService
{
    Task<bool> ShowServerDetailsAsync(RtmpServiceGroup group);
    Task<UserSettings> ShowSettingsDialogAsync(UserSettings currentSettings);
}