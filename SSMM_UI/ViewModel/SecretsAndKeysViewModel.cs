using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.API_Key_Secrets_Loader;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class SecretsAndKeysViewModel : ObservableObject
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

        // init commands
        ToggleApiKeysEdit = new RelayCommand(ToggleApiKeysEditDisplay);
        ToggleConsumerKeys = new RelayCommand(ToggleConsumerKeysEditDisplay);
        ToggleConsumerSecrets = new RelayCommand(ToggleConsumerSecretsEditDisplay);
        ToggleAccessTokens = new RelayCommand(ToggleAccessTokensEditDisplay);
        ToggleAccessSecrets = new RelayCommand(ToggleAccessSecretsEditDisplay);
        ToggleClientIds = new RelayCommand(ToggleClientIdsEditDisplay);
        ToggleAccountNames = new RelayCommand(ToggleAccountNamesEditDisplay);
        ToggleWebHooks = new RelayCommand(ToggleWebHooksEditDisplay);

    }
    public ObservableCollection<KeyValueItem> ApiKeys { get; } = new();
    public ObservableCollection<KeyValueItem> ConsumerKeys { get; } = new();
    public ObservableCollection<KeyValueItem> ConsumerSecrets { get; } = new();
    public ObservableCollection<KeyValueItem> AccessTokens { get; } = new();
    public ObservableCollection<KeyValueItem> AccessSecrets { get; } = new();
    public ObservableCollection<KeyValueItem> ClientIds { get; } = new();
    public ObservableCollection<KeyValueItem> AccountNames { get; } = new();
    public ObservableCollection<KeyValueItem> Webhooks { get; } = new();

    public ICommand ToggleApiKeysEdit { get; }
    [ObservableProperty] bool displayApiKeys;
    public void ToggleApiKeysEditDisplay()
    {
        DisplayApiKeys = !DisplayApiKeys;
    }

    public ICommand ToggleConsumerKeys { get; }
    [ObservableProperty] bool displayConsumerKeys;
    public void ToggleConsumerKeysEditDisplay()
    {
        DisplayConsumerKeys = !DisplayConsumerKeys;
    }

    public ICommand ToggleConsumerSecrets { get; }
    [ObservableProperty] bool displayConsumerSecrets;
    public void ToggleConsumerSecretsEditDisplay()
    {
        DisplayConsumerSecrets = !DisplayConsumerSecrets;
    }

    public ICommand ToggleAccessTokens { get; }
    [ObservableProperty] bool displayAccessTokens;
    public void ToggleAccessTokensEditDisplay()
    {
        DisplayAccessTokens = !DisplayAccessTokens;
    }

    public ICommand ToggleAccessSecrets { get; }
    [ObservableProperty] bool displayAccessSecrets;
    public void ToggleAccessSecretsEditDisplay()
    {
       DisplayAccessSecrets = !DisplayAccessSecrets;
    }

    public ICommand ToggleClientIds { get; }
    [ObservableProperty] bool displayClientIds;
    public void ToggleClientIdsEditDisplay()
    {
        DisplayClientIds = !DisplayClientIds;
    }

    public ICommand ToggleAccountNames { get; }
    [ObservableProperty] bool displayAccountNames;
    public void ToggleAccountNamesEditDisplay()
    {
        DisplayAccountNames = !DisplayAccountNames;
    }

    public ICommand ToggleWebHooks { get; }
    [ObservableProperty] bool displayWebhooks;
    public void ToggleWebHooksEditDisplay()
    {
        DisplayWebhooks = !DisplayWebhooks;
    }
}

public class KeyValueItem
{
    public string? Key { get; set; }
    public string? Value { get; set; }
}

