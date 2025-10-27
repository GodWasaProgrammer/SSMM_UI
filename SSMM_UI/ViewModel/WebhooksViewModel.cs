using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.API_Key_Secrets_Loader;
using SSMM_UI.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class WebhooksViewModel : ObservableObject
{
    public WebhooksViewModel(StateService _state)
    {
        ToggleWebHooks = new RelayCommand(ToggleWebHooksEditDisplay);
        _stateService = _state;
        Webhooks = new ObservableCollection<KeyValueItem>(_stateService.Webhooks
        .SelectMany(dict => dict.Select(kvp => new KeyValueItem
        {
            Key = kvp.Key,
            Value = kvp.Value
        })));
    }
    public ObservableCollection<KeyValueItem> Webhooks { get; } = new();
    StateService _stateService;

    public ICommand ToggleWebHooks { get; }
    [ObservableProperty] bool displayWebhooks;
    public void ToggleWebHooksEditDisplay()
    {
        DisplayWebhooks = !DisplayWebhooks;
    }
    [ObservableProperty] bool isAddingNewWebHook;
    public ICommand AddWebhook => new RelayCommand(AddNewWebHook);
    private Dictionary<string, string>? AnotherWebHook;
    public void AddNewWebHook()
    {
        IsAddingNewWebHook = true;
        AnotherWebHook = [];
    }
    public ICommand SaveNewWebHook => new RelayCommand(SaveNewWebHookExecute);
    [ObservableProperty] string nameofWebHook = string.Empty;
    [ObservableProperty] string linkOfWebhook = string.Empty;
    private void SaveNewWebHookExecute()
    {
        IsAddingNewWebHook = false;
        AnotherWebHook![NameofWebHook] = LinkOfWebhook;
        Webhooks.Add(new KeyValueItem
        {
            Key = AnotherWebHook!.Keys.First(),
            Value = AnotherWebHook.Values.First()
        });
        _stateService.SaveWebHook(AnotherWebHook);
        NameofWebHook = string.Empty;
        LinkOfWebhook = string.Empty;
    }
}

public class KeyValueItem
{
    public string? Key { get; set; }
    public string? Value { get; set; }
}