using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using SSMM_UI;
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
}
public interface IDialogService
{
    Task<bool> ShowServerDetailsAsync(RtmpServiceGroup group);
}