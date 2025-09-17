using SSMM_UI.API_Key_Secrets_Loader;
using System.Collections.ObjectModel;

namespace SSMM_UI.ViewModel;

public class SecretsAndKeysViewModel
{
    public SecretsAndKeysViewModel()
    {
        _api_Keys = (ObservableCollection<ReadOnlyDictionary<string, string>>)KeyLoader.Instance.API_Keys;
        _consumer_Keys = (ObservableCollection<ReadOnlyDictionary<string, string>>)KeyLoader.Instance.CONSUMER_Keys;
        _consumer_Secrets = (ObservableCollection<ReadOnlyDictionary<string, string>>)KeyLoader.Instance.CONSUMER_Secrets;
        _access_Tokens = (ObservableCollection<ReadOnlyDictionary<string, string>>)KeyLoader.Instance.ACCESS_Tokens;
        _access_Secrets = (ObservableCollection<ReadOnlyDictionary<string, string>>)KeyLoader.Instance.ACCESS_Secrets;
        _client_Ids = (ObservableCollection<ReadOnlyDictionary<string, string>>)KeyLoader.Instance.CLIENT_Ids;
        _account_Names = (ObservableCollection<ReadOnlyDictionary<string, string>>)KeyLoader.Instance.ACCOUNT_Names;
        _webhooks = (ObservableCollection<ReadOnlyDictionary<string, string>>)KeyLoader.Instance.Webhooks;
    }
    public ObservableCollection<ReadOnlyDictionary<string, string>> _api_Keys { get; } = new();
    public ObservableCollection<ReadOnlyDictionary<string, string>> _consumer_Keys { get; } = new();
    public ObservableCollection<ReadOnlyDictionary<string, string>> _consumer_Secrets { get; } = new();
    public ObservableCollection<ReadOnlyDictionary<string, string>> _access_Tokens { get; } = new();
    public ObservableCollection<ReadOnlyDictionary<string, string>> _access_Secrets { get; } = new();
    public ObservableCollection<ReadOnlyDictionary<string, string>> _client_Ids { get; } = new();
    public ObservableCollection<ReadOnlyDictionary<string, string>> _account_Names { get; } = new();
    public ObservableCollection<ReadOnlyDictionary<string, string>> _webhooks { get; } = new();
}
