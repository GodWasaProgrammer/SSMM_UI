using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace SSMM_UI.Services;

public class ChatProviderRegistryService
{
    private readonly IReadOnlyDictionary<AuthProvider, IChatProvider> _providers;

    public ChatProviderRegistryService(IEnumerable<IChatProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Provider, p => p);
    }

    public bool TryGetProvider(AuthProvider provider, out IChatProvider? chatProvider)
    {
        return _providers.TryGetValue(provider, out chatProvider);
    }

    public IReadOnlyCollection<AuthProvider> AvailableProviders => _providers.Keys.ToArray();
}
