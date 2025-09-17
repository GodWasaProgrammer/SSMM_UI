using SSMM_UI.API_Key_Secrets_Loader;
using System.Collections.ObjectModel;
using System.Linq;

namespace SSMM_UI.ViewModel;

public class SecretsAndKeysViewModel
{
    public SecretsAndKeysViewModel()
    {
        Api_Keys = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.API_Keys.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        Consumer_Keys = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.CONSUMER_Keys.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        Consumer_Secrets = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.CONSUMER_Secrets.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        Access_Tokens = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.ACCESS_Tokens.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        Access_Secrets = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.ACCESS_Secrets.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        Client_Ids = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.CLIENT_Ids.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        Account_Names = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.ACCOUNT_Names.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        Webhooks = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.Webhooks.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));
    }
    public ObservableCollection<KeyValueItem> Api_Keys { get; } = new();
    public ObservableCollection<KeyValueItem> Consumer_Keys { get; } = new();
    public ObservableCollection<KeyValueItem> Consumer_Secrets { get; } = new();
    public ObservableCollection<KeyValueItem> Access_Tokens { get; } = new();
    public ObservableCollection<KeyValueItem> Access_Secrets { get; } = new();
    public ObservableCollection<KeyValueItem> Client_Ids { get; } = new();
    public ObservableCollection<KeyValueItem> Account_Names { get; } = new();
    public ObservableCollection<KeyValueItem> Webhooks { get; } = new();
}

// just a simple key-value pair class for binding purposes
public class KeyValueItem
{
    public string Key { get; set; }
    public string Value { get; set; }
}

