using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.API_Key_Secrets_Loader;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class WebhooksViewModel : ObservableObject
{
    public WebhooksViewModel()
    {
        Webhooks = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.Webhooks.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));
        ToggleWebHooks = new RelayCommand(ToggleWebHooksEditDisplay);
    }
    public ObservableCollection<KeyValueItem> Webhooks { get; } = new();

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
    private void SaveNewWebHookExecute()
    {
        Webhooks.Add(new KeyValueItem
        {
            Key = AnotherWebHook!.Keys.First(),
            Value = AnotherWebHook.Values.First()
        });
    }
}

public class KeyValueItem
{
    public string? Key { get; set; }
    public string? Value { get; set; }
}