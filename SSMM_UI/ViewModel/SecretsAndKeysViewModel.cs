using SSMM_UI.API_Key_Secrets_Loader;
using System.Collections.ObjectModel;
using System.Linq;

namespace SSMM_UI.ViewModel;

public class SecretsAndKeysViewModel
{
    public SecretsAndKeysViewModel()
    {
        ApiKeys = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.API_Keys.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        ConsumerKeys = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.CONSUMER_Keys.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        ConsumerSecrets = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.CONSUMER_Secrets.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        AccessTokens = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.ACCESS_Tokens.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        AccessSecrets = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.ACCESS_Secrets.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        ClientIds = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.CLIENT_Ids.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        AccountNames = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.ACCOUNT_Names.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));

        Webhooks = new ObservableCollection<KeyValueItem>(
            KeyLoader.Instance.Webhooks.Select(kvp => new KeyValueItem { Key = kvp.Key, Value = kvp.Value }));
    }
    public ObservableCollection<KeyValueItem> ApiKeys { get; } = new();
    public ObservableCollection<KeyValueItem> ConsumerKeys { get; } = new();
    public ObservableCollection<KeyValueItem> ConsumerSecrets { get; } = new();
    public ObservableCollection<KeyValueItem> AccessTokens { get; } = new();
    public ObservableCollection<KeyValueItem> AccessSecrets { get; } = new();
    public ObservableCollection<KeyValueItem> ClientIds { get; } = new();
    public ObservableCollection<KeyValueItem> AccountNames { get; } = new();
    public ObservableCollection<KeyValueItem> Webhooks { get; } = new();
}

// just a simple key-value pair class for binding purposes
public class KeyValueItem
{
    public string? Key { get; set; }
    public string? Value { get; set; }
}

