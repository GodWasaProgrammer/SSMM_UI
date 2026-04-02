using Avalonia.Media.Imaging;
using SSMM_UI.MetaData;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI.Interfaces;

public interface ITwitchCategoryCacheService
{
    Task<(IReadOnlyList<TwitchCategory> Results, bool FromCache)> SearchAsync(
        string query,
        string accessToken,
        string clientId,
        CancellationToken cancellationToken = default);

    Task<Bitmap?> GetOrFetchBoxArtAsync(
        string? categoryId,
        string? boxArtUrl,
        CancellationToken cancellationToken = default);
}
