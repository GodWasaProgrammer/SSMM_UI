using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using SSMM_UI;
using SSMM_UI.Settings;
using SSMM_UI.ViewModel;
using System;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class DialogService : IDialogService
{
    public DialogService()
    {

    }
    public async Task<bool> ShowServerDetailsAsync(RtmpServiceGroup group)
    {
        var detailsWindow = new ServerDetailsWindow(group);
        return await detailsWindow.ShowDialog<bool>(Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
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