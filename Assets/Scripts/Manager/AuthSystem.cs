using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class AuthSessionData
{
    public string Username;
    public string AccessToken;
    public bool IsAdmin;
    public string ExpiresUtc;
}

public static class AuthSystem
{
    private static string s_CurrentUser = string.Empty;
    private static string s_AccessToken = string.Empty;
    private static bool s_IsAdmin = false;
    private static string s_ExpiresUtc = string.Empty;

    public static event Action AuthStateChanged;

    public static string CurrentUser => s_CurrentUser;
    public static bool IsLoggedIn => !string.IsNullOrEmpty(s_CurrentUser) && !string.IsNullOrEmpty(s_AccessToken) && !IsExpiryReached(s_ExpiresUtc);
    public static bool IsAdmin => IsLoggedIn && s_IsAdmin;
    public static string AccessToken => s_AccessToken;
    public static string AccessTokenExpiryUtc => s_ExpiresUtc;
    public static string UserScopeKey => IsLoggedIn ? SanitizeKey(s_CurrentUser) : "guest";

    private static string SessionPath => Path.Combine(Application.persistentDataPath, "auth_session.json");

    public static void Initialize()
    {
        LoadSession();
        CloudSyncService.EnsureExists();
        if (HasValidAccessToken())
            CloudSyncService.PullCurrentProfile();
    }

    public static void SetAuthenticatedSession(string username, string accessToken, bool isAdmin, string expiresUtc)
    {
        s_CurrentUser = Normalize(username);
        s_AccessToken = accessToken ?? string.Empty;
        s_IsAdmin = isAdmin;
        s_ExpiresUtc = expiresUtc ?? string.Empty;

        SaveSession();
        AuthStateChanged?.Invoke();

        if (HasValidAccessToken())
            CloudSyncService.PullCurrentProfile();
    }

    public static void Logout()
    {
        if (HasValidAccessToken())
            CloudSyncService.PushCurrentProfile();

        ClearSessionInternal(removeSessionFile: true);
        AuthStateChanged?.Invoke();
    }

    public static void InvalidateSession()
    {
        ClearSessionInternal(removeSessionFile: true);
        AuthStateChanged?.Invoke();
    }

    public static bool HasValidAccessToken()
    {
        return IsLoggedIn;
    }

    private static void LoadSession()
    {
        try
        {
            if (!File.Exists(SessionPath))
                return;

            string json = File.ReadAllText(SessionPath);
            AuthSessionData saved = JsonUtility.FromJson<AuthSessionData>(json);
            if (saved == null)
                return;

            s_CurrentUser = Normalize(saved.Username);
            s_AccessToken = saved.AccessToken ?? string.Empty;
            s_IsAdmin = saved.IsAdmin;
            s_ExpiresUtc = saved.ExpiresUtc ?? string.Empty;

            if (!HasValidAccessToken())
                ClearSessionInternal(removeSessionFile: true);
        }
        catch
        {
            ClearSessionInternal(removeSessionFile: true);
        }
    }

    private static void SaveSession()
    {
        try
        {
            AuthSessionData data = new AuthSessionData
            {
                Username = s_CurrentUser,
                AccessToken = s_AccessToken,
                IsAdmin = s_IsAdmin,
                ExpiresUtc = s_ExpiresUtc
            };
            File.WriteAllText(SessionPath, JsonUtility.ToJson(data, true));
        }
        catch { }
    }

    private static void ClearSessionInternal(bool removeSessionFile)
    {
        s_CurrentUser = string.Empty;
        s_AccessToken = string.Empty;
        s_IsAdmin = false;
        s_ExpiresUtc = string.Empty;

        if (!removeSessionFile)
            return;

        try
        {
            if (File.Exists(SessionPath))
                File.Delete(SessionPath);
        }
        catch { }
    }

    private static bool IsExpiryReached(string expiresUtc)
    {
        if (string.IsNullOrWhiteSpace(expiresUtc))
            return true;

        if (!DateTime.TryParse(
                expiresUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal,
                out DateTime parsed))
        {
            return true;
        }

        return parsed <= DateTime.UtcNow.AddSeconds(5);
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static string SanitizeKey(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "guest";

        StringBuilder sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else
                sb.Append('_');
        }
        return sb.ToString();
    }
}
