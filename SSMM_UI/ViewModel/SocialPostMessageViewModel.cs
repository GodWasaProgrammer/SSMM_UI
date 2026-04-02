using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SSMM_UI.ViewModel;

public partial class SocialPostMessageViewModel : ObservableObject
{
    private Window? _hostWindow;

    public SocialPostMessageViewModel(string initialMessage)
    {
        messageText = initialMessage ?? string.Empty;
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        UseDefaultCommand = new RelayCommand(UseDefault);
    }

    [ObservableProperty] private string messageText = string.Empty;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand UseDefaultCommand { get; }

    public void SetHostWindow(Window host) => _hostWindow = host;

    private void Save() => _hostWindow?.Close(MessageText.Trim());

    private void Cancel() => _hostWindow?.Close(null);

    private void UseDefault() => _hostWindow?.Close(string.Empty);
}
