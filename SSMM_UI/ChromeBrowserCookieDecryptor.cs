using Avalonia.Logging;
using Microsoft.Data.Sqlite;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SSMM_UI;

public class ChromeBrowserCookiesDecryptor
{
    private const string _userDataDir = "C:/users/Björn/AppData/Local/Google/Chrome/User Data/";
    private const string _profileName = "Default";
    private readonly string _origin = "https://studio.youtube.com";
    public Dictionary<string, string> _cookies = [];

    public ChromeBrowserCookiesDecryptor(string userDataDir = _userDataDir, string profileName = _profileName)
    {
        ArgumentNullException.ThrowIfNull(userDataDir);
        ArgumentNullException.ThrowIfNull(profileName);
        _cookies = GetCookiesFromProfile();
    }


    public Dictionary<string, string> BuildFullAuthHeaders()
    {
        if (!_cookies.TryGetValue("SAPISID", out var sapisid) || string.IsNullOrEmpty(sapisid))
            throw new Exception("SAPISID cookie saknas");

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string hashInput = $"{timestamp} {sapisid} https://studio.youtube.com";
        string hash = ComputeSha1Hash(hashInput);

        // Samma hash används för alla tre, men med olika nyckelnamn
        return new Dictionary<string, string>
        {
            ["main"] = $"{timestamp}_{hash}",
            ["secondary"] = $"{timestamp}_{hash}",
            ["tertiary"] = $"{timestamp}_{hash}"
        };
    }

    private static string ComputeSha1Hash(string input)
    {
        byte[] bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }

    public string BuildSapisdHashHeader()
    {
        if (_cookies == null || _cookies.Count == 0) throw new Exception("No cookies found.");

        var sapisid = _cookies.TryGetValue("SAPISID", out var s) ? s : null;
        if (string.IsNullOrEmpty(sapisid))
        {
            throw new Exception("SAPISID cookie not found. Make sure you selected correct profile and are logged in.");
        }

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var input = $"{timestamp} {sapisid} {_origin}";
        var sha1 = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        var hex = BitConverter.ToString(sha1).Replace("-", "").ToLowerInvariant();

        return $"{timestamp}_{hex}_u"; // Lägg till _u
    }

    public static Dictionary<string, string> GetCookiesFromProfile(string userDataDir = _userDataDir, string profileName = _profileName)
    {
        LogService.Log("[1] Startar GetCookiesFromProfile...");
        SQLitePCL.Batteries.Init();
        LogService.Log("[2] SQLitePCL initierad.");

        // Hitta cookies-filen
        string cookiesPath = Path.Combine(userDataDir, profileName, "Network", "Cookies");
        if (!File.Exists(cookiesPath))
        {
            LogService.Log("[3] Hittade inte 'Network/Cookies', testar 'Cookies'...");
            cookiesPath = Path.Combine(userDataDir, profileName, "Cookies");
            if (!File.Exists(cookiesPath))
            {
                LogService.Log("[4] ERROR: Cookies DB finns inte!");
                throw new FileNotFoundException("Cookies DB not found", cookiesPath);
            }
        }
        LogService.Log($"[5] Hittade cookies-fil: {cookiesPath}");

        // Kopiera till temporär fil
        var tmpFile = Path.Combine(Path.GetTempPath(), $"Cookies_{Guid.NewGuid():N}.db");
        LogService.Log($"[6] Kopierar till tmp-fil: {tmpFile}");
        File.Copy(cookiesPath, tmpFile, true);

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            LogService.Log("[7] Skapar SQLite-anslutning...");
            var connString = new SqliteConnectionStringBuilder
            {
                DataSource = tmpFile,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();
            LogService.Log($"[8] Anslutningssträng: {connString}");

            using var conn = new SqliteConnection(connString);
            LogService.Log("[9] Försöker öppna anslutning...");
            conn.Open();
            LogService.Log("[10] Anslutning öppnad!");

            using var cmd = conn.CreateCommand();
            // Ändrad SQL-fråga: Endast YouTube/Google-cookies
            cmd.CommandText = @"
            SELECT host_key, name, encrypted_value 
            FROM cookies 
            WHERE host_key LIKE '%youtube.com' 
               OR host_key LIKE '%google.com'
               OR host_key = '.youtube.com'
               OR host_key = '.google.com';";
            LogService.Log("[11] Kör SQL-fråga...");

            using var reader = cmd.ExecuteReader();
            LogService.Log("[12] Läser resultat...");
            int cookieCount = 0;

            while (reader.Read())
            {
                var host = reader.GetString(0);
                var name = reader.GetString(1);
                var encryptedValue = (byte[])reader["encrypted_value"];
                var value = DecryptChromeCookie(encryptedValue, userDataDir);

                if (!string.IsNullOrEmpty(value) && !dict.ContainsKey(name))
                {
                    dict[name] = value;
                    cookieCount++;
                    LogService.Log($"[13] Cookie #{cookieCount}: {name}={value} (från {host})");
                }
            }
            LogService.Log($"[14] Klart! Totalt {cookieCount} YouTube/Google-cookies.");
        }
        catch (Exception ex)
        {
            LogService.Log($"[ERROR] Fel vid DB-åtkomst: {ex.GetType().Name}");
            LogService.Log($"Meddelande: {ex.Message}");
            LogService.Log($"Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            try
            {
                //File.Delete(tmpFile);
                //Console.WriteLine("[15] Tmp-fil borttagen.");
            }
            catch (Exception ex)
            {
                LogService.Log($"[WARN] Kunde inte ta bort tmp-fil: {ex.Message}");
            }
        }

        return dict;
    }

    // -------------------------
    // Master key extraction (v20 + fallback)
    // -------------------------
    private static byte[]? GetChromeMasterKey(string userDataDir)
    {
        var localStatePath = Path.Combine(userDataDir, "Local State");
        if (!File.Exists(localStatePath)) return null;

        var json = File.ReadAllText(localStatePath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("os_crypt", out var osCrypt))
            return null;

        // Try app-bound key first (v20 path)
        if (osCrypt.TryGetProperty("app_bound_encrypted_key", out var appBoundKeyProp))
        {
            var encryptedKeyB64 = appBoundKeyProp.GetString();
            if (!string.IsNullOrEmpty(encryptedKeyB64))
            {
                try
                {
                    var raw = Convert.FromBase64String(encryptedKeyB64);
                    // strip "APPB" prefix if present
                    if (raw.Length > 4 && raw[0] == (byte)'A' && raw[1] == (byte)'P' && raw[2] == (byte)'P' && raw[3] == (byte)'B')
                    {
                        var actualEncrypted = new byte[raw.Length - 4];
                        Array.Copy(raw, 4, actualEncrypted, 0, actualEncrypted.Length);
                        var key = DecryptAppBoundKey(actualEncrypted);
                        if (key != null) return key;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"App-bound key path failed: {ex.Message}");
                }
            }
        }

        // Fallback to legacy encrypted_key (v10 style)
        if (osCrypt.TryGetProperty("encrypted_key", out var encKeyProp))
        {
            var encryptedKeyB64 = encKeyProp.GetString();
            if (!string.IsNullOrEmpty(encryptedKeyB64))
            {
                try
                {
                    return DecryptOldChromeKey(Convert.FromBase64String(encryptedKeyB64));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Old key decryption failed: {ex.Message}");
                }
            }
        }

        return null;
    }

    private static byte[] DecryptOldChromeKey(byte[] encryptedKey)
    {
        const string dpapiPrefix = "DPAPI";
        var key = encryptedKey;

        if (encryptedKey.Length > dpapiPrefix.Length &&
            Encoding.ASCII.GetString(encryptedKey, 0, dpapiPrefix.Length) == dpapiPrefix)
        {
            var actual = new byte[encryptedKey.Length - dpapiPrefix.Length];
            Array.Copy(encryptedKey, dpapiPrefix.Length, actual, 0, actual.Length);
            key = actual;
        }

        try
        {
            return ProtectedData.Unprotect(key, null, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    // -------------------------
    // IMPORTANT: app-bound v20 flow (system DPAPI -> user DPAPI -> parse -> NCrypt)
    // -------------------------
    private static byte[]? DecryptAppBoundKey(byte[] appBoundEncrypted)
    {
        // Step 1: system DPAPI unprotect (IMPERS0NATE LSASS)
        byte[] systemDecrypted;
        try
        {
            using (new LsassImpersonation())
            {
                // under SYSTEM context try machine-scoped unprotect
                systemDecrypted = ProtectedData.Unprotect(appBoundEncrypted, null, DataProtectionScope.LocalMachine);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"System DPAPI unprotect failed: {ex.Message}");
            return null;
        }

        // Step 2: user DPAPI unprotect (current user)
        byte[] userDecrypted;
        try
        {
            userDecrypted = ProtectedData.Unprotect(systemDecrypted, null, DataProtectionScope.CurrentUser);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"User DPAPI unprotect failed: {ex.Message}");
            return null;
        }

        // Parse the key blob and derive v20 master key
        try
        {
            var parsed = KeyBlob.Parse(userDecrypted);
            return DeriveV20MasterKey(parsed);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Parsing/deriving v20 key failed: {ex.Message}");
            return null;
        }
    }

    private static byte[] DeriveV20MasterKey(KeyBlob parsed)
    {
        if (parsed.Flag == 1)
        {
            var aesKey = HexToBytes("B31C6E241AC846728DA9C1FAC4936651CFFB944D143AB816276BCC6DA0284787");
            using var aes = new AesGcm(aesKey);
            var outPlain = new byte[parsed.Ciphertext.Length];
            aes.Decrypt(parsed.Iv, parsed.Ciphertext, parsed.Tag, outPlain);
            return outPlain;
        }

#if NET6_0_OR_GREATER || NET7_0_OR_GREATER || NET8_0_OR_GREATER
        else if (parsed.Flag == 2)
        {
            byte[] chachaKey = Convert.FromHexString("E98F37D7F4E1FA433D19304DC2258042090E2D1D7EEA7670D41F738D08729660");
            byte[] outPlain = new byte[parsed.Ciphertext.Length];
            using (var chacha = new ChaCha20Poly1305(chachaKey))
            {
                chacha.Decrypt(parsed.Iv, parsed.Ciphertext, parsed.Tag, outPlain);
            }
            return outPlain; // eller fortsätt bearbeta som i din Python-logik
        }
#else
            throw new PlatformNotSupportedException("ChaCha20-Poly1305 derivation (flag 2) requires .NET 6+/runtime support.");
#endif

        else if (parsed.Flag == 3)
        {
            byte[] decryptedAesKey;
            try
            {
                using (new LsassImpersonation())
                {
                    decryptedAesKey = DecryptWithCng(parsed.EncryptedAesKey);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"NCrypt decrypt failed: {ex.Message}");
            }

            var xorKey = HexToBytes("CCF8A1CEC56605B8517552BA1A2D061C03A29E90274FB2FCF59BA4B75C392390");
            if (decryptedAesKey.Length != xorKey.Length)
                throw new Exception("Unexpected decrypted key length.");

            var finalAesKey = XorBytes(decryptedAesKey, xorKey);

            using var aes = new AesGcm(finalAesKey);
            var outPlain = new byte[parsed.Ciphertext.Length];
            aes.Decrypt(parsed.Iv, parsed.Ciphertext, parsed.Tag, outPlain);

            return outPlain;
        }
        else
        {
            throw new Exception($"Unsupported flag value: {parsed.Flag}");
        }
    }

    private static byte[] DecryptWithCng(byte[] input)
    {
        nint hProvider = nint.Zero;
        nint hKey = nint.Zero;
        try
        {
            int status = NativeMethods.NCryptOpenStorageProvider(out hProvider, "Microsoft Software Key Storage Provider", 0);
            if (status != 0) throw new Exception($"NCryptOpenStorageProvider failed: 0x{status:X}");

            status = NativeMethods.NCryptOpenKey(hProvider, out hKey, "Google Chromekey1", 0, 0);
            if (status != 0) throw new Exception($"NCryptOpenKey failed: 0x{status:X}");

            status = NativeMethods.NCryptDecrypt(hKey, input, input.Length, nint.Zero, null, 0, out int requiredSize, NativeMethods.NCRYPT_SILENT_FLAG);
            if (status != 0) throw new Exception($"NCryptDecrypt (size) failed: 0x{status:X}");

            var outBuf = new byte[requiredSize];
            status = NativeMethods.NCryptDecrypt(hKey, input, input.Length, nint.Zero, outBuf, outBuf.Length, out requiredSize, NativeMethods.NCRYPT_SILENT_FLAG);
            if (status != 0) throw new Exception($"NCryptDecrypt (decrypt) failed: 0x{status:X}");

            var result = new byte[requiredSize];
            Array.Copy(outBuf, result, requiredSize);
            return result;
        }
        finally
        {
            if (hKey != nint.Zero) NativeMethods.NCryptFreeObject(hKey);
            if (hProvider != nint.Zero) NativeMethods.NCryptFreeObject(hProvider);
        }
    }

    // -------------------------
    // Cookie decryption helpers
    // -------------------------
    private static string DecryptChromeCookie(byte[] encryptedValue, string userDataDir)
    {
        if (encryptedValue == null || encryptedValue.Length == 0)
            return "";

        var v10 = Encoding.ASCII.GetBytes("v10");
        var v20 = Encoding.ASCII.GetBytes("v20");

        if (encryptedValue.Length > 3 && encryptedValue.Take(3).SequenceEqual(v10))
        {
            try
            {
                var toDecrypt = encryptedValue.Skip(3).ToArray();
                var plain = ProtectedData.Unprotect(toDecrypt, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return "";
            }
        }
        else if (encryptedValue.Length > 3 && encryptedValue.Take(3).SequenceEqual(v20))
        {
            try
            {
                var masterKey = GetChromeMasterKey(userDataDir);
                if (masterKey == null) return "";

                return DecryptV20Cookie(encryptedValue, masterKey);
            }
            catch
            {
                return "";
            }
        }
        else
        {
            try { return Encoding.UTF8.GetString(encryptedValue); } catch { return ""; }
        }
    }

    private static string DecryptV20Cookie(byte[] encryptedValue, byte[] masterKey)
    {
        const int prefix = 3;
        const int ivLen = 12;
        const int tagLen = 16;

        if (encryptedValue.Length < prefix + ivLen + tagLen) return "";

        var iv = encryptedValue.Skip(prefix).Take(ivLen).ToArray();
        var cipherLen = encryptedValue.Length - prefix - ivLen - tagLen;
        var cipher = encryptedValue.Skip(prefix + ivLen).Take(cipherLen).ToArray();
        var tag = encryptedValue.Skip(prefix + ivLen + cipherLen).Take(tagLen).ToArray();

        var plaintext = new byte[cipherLen];
        try
        {
            using var aes = new AesGcm(masterKey);
            aes.Decrypt(iv, cipher, tag, plaintext);
        }
        catch
        {
            return "";
        }

        if (plaintext.Length <= 32) return "";
        return Encoding.UTF8.GetString(plaintext, 32, plaintext.Length - 32);
    }

    // -------------------------
    // Util
    // -------------------------
    private static byte[] HexToBytes(string hex)
    {
        var outb = new byte[hex.Length / 2];
        for (int i = 0; i < outb.Length; i++)
            outb[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return outb;
    }

    private static byte[] XorBytes(byte[] a, byte[] b)
    {
        var r = new byte[a.Length];
        for (int i = 0; i < a.Length; i++) r[i] = (byte)(a[i] ^ b[i]);
        return r;
    }

    // -------------------------
    // Key blob parser object
    // -------------------------

    private class KeyBlob
    {
        public int Flag;
        public byte[]? Header;
        public byte[]? EncryptedAesKey; // when flag == 3
        public byte[]? Iv;
        public byte[]? Ciphertext;
        public byte[]? Tag;

        public static KeyBlob Parse(byte[] blob)
        {
            using var ms = new MemoryStream(blob);
            using var br = new BinaryReader(ms);

            int headerLen = br.ReadInt32(); // little-endian
            var header = br.ReadBytes(headerLen);
            int contentLen = br.ReadInt32();

            var flag = br.ReadByte();
            var k = new KeyBlob { Flag = flag, Header = header };

            if (flag == 1 || flag == 2)
            {
                k.Iv = br.ReadBytes(12);
                k.Ciphertext = br.ReadBytes(32);
                k.Tag = br.ReadBytes(16);
            }
            else if (flag == 3)
            {
                k.EncryptedAesKey = br.ReadBytes(32);
                k.Iv = br.ReadBytes(12);
                k.Ciphertext = br.ReadBytes(32);
                k.Tag = br.ReadBytes(16);
            }
            else
            {
                throw new Exception($"Unsupported flag: {flag}");
            }

            return k;
        }
    }

    // -------------------------
    // Native + impersonation helpers
    // -------------------------
    private static class NativeMethods
    {
        public const int NCRYPT_SILENT_FLAG = 0x00000040;

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        public static extern int NCryptOpenStorageProvider(out nint phProvider, string pszProviderName, int dwFlags);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        public static extern int NCryptOpenKey(nint hProvider, out nint phKey, string pszKeyName, int dwLegacyKeySpec, int dwFlags);

        [DllImport("ncrypt.dll")]
        public static extern int NCryptDecrypt(nint hKey,
            [In] byte[] pbInput,
            int cbInput,
            nint pPaddingInfo,
            [Out] byte[]? pbOutput,
            int cbOutput,
            out int pcbResult,
            int dwFlags);

        [DllImport("ncrypt.dll")]
        public static extern int NCryptFreeObject(nint hObject);

        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const uint PROCESS_QUERY_INFORMATION = 0x0400;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern nint OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(nint ProcessHandle, uint DesiredAccess, out nint TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool DuplicateTokenEx(
            nint hExistingToken,
            uint dwDesiredAccess,
            nint lpTokenAttributes,
            int ImpersonationLevel,
            int TokenType,
            out nint phNewToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool AdjustTokenPrivileges(nint TokenHandle, bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState, int BufferLength, nint PreviousState, nint ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(nint hObject);

        // SetThreadToken - set token for current thread when first param is IntPtr.Zero
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool SetThreadToken(nint thread, nint token);

        public const uint TOKEN_DUPLICATE = 0x0002;
        public const uint TOKEN_QUERY = 0x0008;
        public const uint TOKEN_IMPERSONATE = 0x0004;
        public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;

        public const int SecurityImpersonation = 2;
        public const int TokenImpersonation = 2;
        public const string SE_DEBUG_NAME = "SeDebugPrivilege";

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privileges;
        }
    }

    private sealed class LsassImpersonation : IDisposable
    {
        private nint _dupToken = nint.Zero;
        private nint _procHandle = nint.Zero;
        private bool _applied = false;

        public LsassImpersonation()
        {
            ImpersonateLsass();
        }

        private void ImpersonateLsass()
        {
            EnableDebugPrivilege();

            var procs = Process.GetProcessesByName("lsass");
            if (procs == null || procs.Length == 0) throw new Exception("lsass.exe process not found.");

            var lsass = procs[0];
            _procHandle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, lsass.Id);
            if (_procHandle == nint.Zero)
            {
                _procHandle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_INFORMATION, false, lsass.Id);
                if (_procHandle == nint.Zero)
                    throw new Exception($"OpenProcess failed: {Marshal.GetLastWin32Error()}");
            }

            if (!NativeMethods.OpenProcessToken(_procHandle, NativeMethods.TOKEN_DUPLICATE | NativeMethods.TOKEN_QUERY | NativeMethods.TOKEN_IMPERSONATE, out var lsassToken))
                throw new Exception($"OpenProcessToken failed: {Marshal.GetLastWin32Error()}");

            // Duplicate as impersonation token
            if (!NativeMethods.DuplicateTokenEx(lsassToken,
                NativeMethods.TOKEN_DUPLICATE | NativeMethods.TOKEN_QUERY | NativeMethods.TOKEN_IMPERSONATE,
                nint.Zero,
                NativeMethods.SecurityImpersonation,
                NativeMethods.TokenImpersonation,
                out _dupToken))
            {
                NativeMethods.CloseHandle(lsassToken);
                throw new Exception($"DuplicateTokenEx failed: {Marshal.GetLastWin32Error()}");
            }

            NativeMethods.CloseHandle(lsassToken);

            // Set token on current thread
            if (!NativeMethods.SetThreadToken(nint.Zero, _dupToken))
            {
                var err = Marshal.GetLastWin32Error();
                throw new Exception($"SetThreadToken failed: {err}");
            }

            _applied = true;
        }

        private static void EnableDebugPrivilege()
        {
            if (!NativeMethods.OpenProcessToken(Process.GetCurrentProcess().Handle,
                NativeMethods.TOKEN_ADJUST_PRIVILEGES | NativeMethods.TOKEN_QUERY,
                out nint token))
            {
                throw new Exception($"OpenProcessToken(current) failed: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                if (!NativeMethods.LookupPrivilegeValue(null, NativeMethods.SE_DEBUG_NAME, out var luid))
                    throw new Exception("LookupPrivilegeValue failed.");

                var tp = new NativeMethods.TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Privileges = new NativeMethods.LUID_AND_ATTRIBUTES
                    {
                        Luid = luid,
                        Attributes = 0x00000002u // SE_PRIVILEGE_ENABLED
                    }
                };

                if (!NativeMethods.AdjustTokenPrivileges(token, false, ref tp, Marshal.SizeOf(tp), nint.Zero, nint.Zero))
                {
                    throw new Exception($"AdjustTokenPrivileges failed: {Marshal.GetLastWin32Error()}");
                }
            }
            finally
            {
                NativeMethods.CloseHandle(token);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_applied)
                {
                    NativeMethods.SetThreadToken(nint.Zero, nint.Zero); // revert
                    _applied = false;
                }
            }
            catch { }

            if (_dupToken != nint.Zero)
            {
                NativeMethods.CloseHandle(_dupToken);
                _dupToken = nint.Zero;
            }

            if (_procHandle != nint.Zero)
            {
                NativeMethods.CloseHandle(_procHandle);
                _procHandle = nint.Zero;
            }
        }
    }

}
