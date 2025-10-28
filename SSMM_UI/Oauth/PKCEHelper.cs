using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace SSMM_UI.Oauth;

public class PKCEHelper
{
    private const int VerifierLength = 64;

    public static string GenerateCodeVerifier()
    {
        var bytes = new byte[VerifierLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        return Base64UrlEncode(bytes);
    }

    public static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = Encoding.ASCII.GetBytes(codeVerifier);
        var hash = SHA256.HashData(bytes);
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Genererar URL för användarautentisering (PKCE)
    /// </summary>
    public static string GetAuthorizationUrl(string codeChallenge, string _clientId,string _redirectUri, string[] _scopes, string AuthEndpoint)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = _clientId;
        query["redirect_uri"] = _redirectUri;
        query["response_type"] = "code";
        query["scope"] = string.Join(",", _scopes);
        query["code_challenge"] = codeChallenge;
        query["code_challenge_method"] = "S256";

        return $"{AuthEndpoint}?{query}";
    }

    public static string RandomString(int length)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[length];
        for (int i = 0; i < length; i++) chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }
}
