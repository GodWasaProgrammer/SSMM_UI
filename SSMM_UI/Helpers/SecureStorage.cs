using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SSMM_UI.Helpers;

public static class SecureStorage
{
    /// <summary>
    /// Krypterar och sparar ett objekt som JSON, skyddat för den aktuella användaren.
    /// </summary>
    public static void SaveEncrypted<T>(string path, T data, JsonSerializerOptions? options = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, options);
            var bytes = Encoding.UTF8.GetBytes(json);

            // Kryptera med DPAPI (bundna till den inloggade användaren)
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);

            // Se till att mappen finns
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            File.WriteAllBytes(path, protectedBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save encrypted data: {ex.Message}");
        }
    }

    /// <summary>
    /// Läser och dekrypterar en JSON-fil.
    /// </summary>
    public static T? LoadEncrypted<T>(string path, JsonSerializerOptions? options = null)
    {
        try
        {
            if (!File.Exists(path))
                return default;

            var protectedBytes = File.ReadAllBytes(path);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load encrypted data: {ex.Message}");
            return default;
        }
    }
}