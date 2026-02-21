using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class CloudBackendConfig
{
    public bool Enabled = false;
    public string BaseUrl = "";
    public int TimeoutSec = 8;
}

public class CloudSyncService : MonoBehaviour
{
    private static CloudSyncService s_Instance;
    private CloudBackendConfig m_Config;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (s_Instance != null)
            return;
        GameObject go = new GameObject("CloudSyncService");
        s_Instance = go.AddComponent<CloudSyncService>();
        DontDestroyOnLoad(go);
    }

    public static void PullCurrentProfile()
    {
        if (s_Instance == null)
            EnsureExists();
        if (!AuthSystem.HasValidAccessToken())
            return;
        if (!s_Instance.IsCloudEnabled())
            return;
        s_Instance.StartCoroutine(s_Instance.PullRoutine(AuthSystem.CurrentUser));
    }

    public static void PushCurrentProfile()
    {
        if (s_Instance == null)
            EnsureExists();
        if (!AuthSystem.HasValidAccessToken())
            return;
        if (!s_Instance.IsCloudEnabled())
            return;
        s_Instance.StartCoroutine(s_Instance.PushRoutine(AuthSystem.CurrentUser));
    }

    private void Awake()
    {
        LoadConfig();
    }

    private bool IsCloudEnabled()
    {
        return m_Config != null && m_Config.Enabled && !string.IsNullOrWhiteSpace(m_Config.BaseUrl);
    }

    private void LoadConfig()
    {
        TextAsset cfgAsset = Resources.Load<TextAsset>("backend_config");
        if (cfgAsset == null || string.IsNullOrWhiteSpace(cfgAsset.text))
        {
            m_Config = new CloudBackendConfig();
            return;
        }

        try
        {
            m_Config = JsonUtility.FromJson<CloudBackendConfig>(cfgAsset.text);
            if (m_Config == null)
                m_Config = new CloudBackendConfig();
            if (m_Config.TimeoutSec <= 0)
                m_Config.TimeoutSec = 8;
        }
        catch
        {
            m_Config = new CloudBackendConfig();
        }
    }

    private System.Collections.IEnumerator PullRoutine(string username)
    {
        string url = BuildProfileUrl(username);
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = m_Config.TimeoutSec;
            req.SetRequestHeader("Authorization", $"Bearer {AuthSystem.AccessToken}");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (req.responseCode == 401 || req.responseCode == 403)
                    AuthSystem.InvalidateSession();
                Debug.LogWarning($"Cloud pull failed: {req.error}");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(req.downloadHandler.text))
                yield break;

            ProfileProgressData data = null;
            try
            {
                data = JsonUtility.FromJson<ProfileProgressData>(req.downloadHandler.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Cloud pull parse failed: {ex.Message}");
            }

            if (data != null)
            {
                ProfileProgress.ApplyToCurrentUser(data);
                UpgradeSystem.NotifyDataChanged();
            }
        }
    }

    private System.Collections.IEnumerator PushRoutine(string username)
    {
        string url = BuildProfileUrl(username);
        ProfileProgressData data = ProfileProgress.ExportCurrentUser();
        string json = JsonUtility.ToJson(data);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(url, "PUT"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = m_Config.TimeoutSec;
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {AuthSystem.AccessToken}");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (req.responseCode == 401 || req.responseCode == 403)
                    AuthSystem.InvalidateSession();
                Debug.LogWarning($"Cloud push failed: {req.error}");
            }
        }
    }

    private string BuildProfileUrl(string username)
    {
        string baseUrl = m_Config.BaseUrl.TrimEnd('/');
        string escapedUser = UnityWebRequest.EscapeURL(username);
        return $"{baseUrl}/progress/{escapedUser}";
    }
}
