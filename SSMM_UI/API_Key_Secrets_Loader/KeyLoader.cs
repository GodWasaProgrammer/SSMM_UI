using System;
using System.Collections.Generic;
using System.IO;

namespace SSMM_UI.API_Key_Secrets_Loader;

public sealed class KeyLoader
{
    public static readonly KeyLoader Instance = new();
    public readonly IReadOnlyDictionary<string, string> API_Keys;
    public readonly IReadOnlyDictionary<string, string> CONSUMER_Keys;
    public readonly IReadOnlyDictionary<string, string> CONSUMER_Secrets;
    public readonly IReadOnlyDictionary<string, string> ACCESS_Tokens;
    public readonly IReadOnlyDictionary<string, string> ACCESS_Secrets;
    public readonly IReadOnlyDictionary<string, string> CLIENT_Ids;
    public readonly IReadOnlyDictionary<string, string> ACCOUNT_Names;
    public readonly IReadOnlyDictionary<string, string> Webhooks;

    private KeyLoader()
    {
        API_Keys = LoadApiKeys("API_Keys.txt");
        CONSUMER_Keys = LoadApiKeys("Consumer_Keys.txt");
        CONSUMER_Secrets = LoadApiKeys("Consumer_Secrets.txt");
        ACCESS_Tokens = LoadApiKeys("Access_Token.txt");
        ACCESS_Secrets = LoadApiKeys("Access_Secret.txt");
        CLIENT_Ids = LoadApiKeys("Client_Ids.txt");
        ACCOUNT_Names = LoadApiKeys("Accounts.txt");
        Webhooks = LoadApiKeys("Webhooks.txt");
    }

    private static Dictionary<string, string> LoadApiKeys(string filePath)
    {
        var apiKeys = new Dictionary<string, string>();

        var baseDir = AppContext.BaseDirectory;
        var projectRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
        var targetPath = Path.Combine(projectRoot, "API_Key_Secrets_Loader");
        var combinedpath = Path.Combine(targetPath, filePath);
   
        if (!File.Exists(combinedpath))
        {
            throw new FileNotFoundException($"Key file not found: {combinedpath}");
        }

        foreach (var line in File.ReadLines(combinedpath))
        {
            if (string.IsNullOrWhiteSpace(line) || !line.Contains("=")) continue;

            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                string key = parts[0].Trim();
                string value = parts[1].Trim();

                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                {
                    apiKeys[key] = value;
                }
            }
        }

        return apiKeys;
    }
}