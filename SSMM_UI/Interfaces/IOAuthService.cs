using System.Threading.Tasks;

namespace SSMM_UI.Interfaces;

public interface IOAuthService<TToken>
{
    /// <summary>
    /// Attempts to retrieve and use an existing token if one is available.
    /// </summary>
    /// <returns>The existing token of Type <typeparamref name="TToken"/> if available; otherwise, <c>null</c>.</returns>
    Task<TToken?> TryUseExistingTokenAsync();
    /// <summary>
    /// Attempts to log in and obtain a new Token.
    /// </summary>
    /// <returns>Token of Type <typeparamref name="TToken"/></returns>
    Task<TToken?> LoginAsync();

    /// <summary>
    /// Should return null if refresh failed.
    /// </summary>
    /// <param name="token">The RefreshToken as a plain string</param>
    /// <returns>Token Of Type <typeparamref name="TToken"/> If Success, Otherwise <c>null</c> </returns>
    Task<TToken?> RefreshTokenAsync(string token);
}
