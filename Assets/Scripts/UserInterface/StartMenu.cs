using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

public class StartMenu : UiMono
{
    [Serializable]
    private class AdminUserDto
    {
        public string username;
        public int metaCoins;
        public int coreHealthLevel;
        public int towerDamageLevel;
        public int towerFireRateLevel;
        public int repairPowerLevel;
        public bool banned;
        public string banReason;
        public string updatedUtc;
    }

    [Serializable]
    private class AdminUsersResponse
    {
        public AdminUserDto[] users;
    }

    [Serializable]
    private class AdminGrantRequest
    {
        public string username;
        public int delta;
    }

    [Serializable]
    private class AdminResetRequest
    {
        public string username;
    }

    [Serializable]
    private class AdminDeleteRequest
    {
        public string username;
    }

    [Serializable]
    private class AdminBanRequest
    {
        public string username;
        public bool banned;
        public string reason;
    }

    [Serializable]
    private class AuthRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    private class AuthResponse
    {
        public string username;
        public string token;
        public bool isAdmin;
        public string expiresUtc;
        public string error;
        public string message;
    }

    [Serializable]
    private class ApiErrorResponse
    {
        public string error;
        public string message;
        public string reason;
    }

    private Button m_PlayBtn;
    private Button m_QuitBtn;
    private Button m_UpgradesOpenBtn;
    private Button m_UpgradesCloseBtn;
    private Button m_AdminOpenBtn;
    private Button m_AdminCloseBtn;
    private Button m_AdminRefreshBtn;
    private Button m_AdminGrantBtn;
    private Button m_AdminResetBtn;
    private Button m_AdminDeleteBtn;
    private Button m_AdminBanBtn;
    private Button m_AdminUnbanBtn;
    private Button m_DifficultyEasyBtn;
    private Button m_DifficultyNormalBtn;
    private Button m_DifficultyHardBtn;
    private Button m_PermCoreBtn;
    private Button m_PermDamageBtn;
    private Button m_PermRateBtn;
    private Button m_PermRepairBtn;
    private Button m_AuthLoginBtn;
    private Button m_AuthRegisterBtn;
    private Button m_SwitchAccountBtn;

    private Label m_MetaCoinsLbl;
    private Label m_PermCoreLbl;
    private Label m_PermDamageLbl;
    private Label m_PermRateLbl;
    private Label m_PermRepairLbl;
    private Label m_AuthStatusLbl;
    private Label m_AuthStatusMenuLbl;
    private Label m_AdminStatusLbl;
    private Label m_AdminUsersLbl;

    private VisualElement m_UpgradesPanel;
    private VisualElement m_AdminPanel;
    private VisualElement m_AuthCard;
    private VisualElement m_MenuCard;

    private TextField m_AuthUsernameInput;
    private TextField m_AuthPasswordInput;
    private TextField m_AdminSearchInput;
    private TextField m_AdminTargetUserInput;
    private TextField m_AdminDeltaInput;

    private string m_AdminBaseUrl = string.Empty;
    private bool m_BackendEnabled = false;
    private int m_BackendTimeoutSec = 8;
    private bool m_AuthRequestInFlight = false;

    void Start()
    {
        AuthSystem.Initialize();
        LoadBackendConfig();

        this.m_PlayBtn = this.m_Doc.rootVisualElement.Q<Button>("play-btn");
        this.m_QuitBtn = this.m_Doc.rootVisualElement.Q<Button>("quit-btn");
        this.m_UpgradesOpenBtn = this.m_Doc.rootVisualElement.Q<Button>("upgrades-open-btn");
        this.m_UpgradesCloseBtn = this.m_Doc.rootVisualElement.Q<Button>("upgrades-close-btn");
        this.m_AdminOpenBtn = this.m_Doc.rootVisualElement.Q<Button>("admin-open-btn");
        this.m_AdminCloseBtn = this.m_Doc.rootVisualElement.Q<Button>("admin-close-btn");
        this.m_AdminRefreshBtn = this.m_Doc.rootVisualElement.Q<Button>("admin-refresh-btn");
        this.m_AdminGrantBtn = this.m_Doc.rootVisualElement.Q<Button>("admin-grant-btn");
        this.m_AdminResetBtn = this.m_Doc.rootVisualElement.Q<Button>("admin-reset-btn");
        this.m_AdminDeleteBtn = this.m_Doc.rootVisualElement.Q<Button>("admin-delete-btn");
        this.m_AdminBanBtn = this.m_Doc.rootVisualElement.Q<Button>("admin-ban-btn");
        this.m_AdminUnbanBtn = this.m_Doc.rootVisualElement.Q<Button>("admin-unban-btn");
        this.m_DifficultyEasyBtn = this.m_Doc.rootVisualElement.Q<Button>("difficulty-easy-btn");
        this.m_DifficultyNormalBtn = this.m_Doc.rootVisualElement.Q<Button>("difficulty-normal-btn");
        this.m_DifficultyHardBtn = this.m_Doc.rootVisualElement.Q<Button>("difficulty-hard-btn");
        this.m_PermCoreBtn = this.m_Doc.rootVisualElement.Q<Button>("perm-core-btn");
        this.m_PermDamageBtn = this.m_Doc.rootVisualElement.Q<Button>("perm-dmg-btn");
        this.m_PermRateBtn = this.m_Doc.rootVisualElement.Q<Button>("perm-rate-btn");
        this.m_PermRepairBtn = this.m_Doc.rootVisualElement.Q<Button>("perm-repair-btn");
        this.m_AuthLoginBtn = this.m_Doc.rootVisualElement.Q<Button>("auth-login-btn");
        this.m_AuthRegisterBtn = this.m_Doc.rootVisualElement.Q<Button>("auth-register-btn");
        this.m_SwitchAccountBtn = this.m_Doc.rootVisualElement.Q<Button>("switch-account-btn");

        this.m_MetaCoinsLbl = this.m_Doc.rootVisualElement.Q<Label>("meta-coins-lbl");
        this.m_PermCoreLbl = this.m_Doc.rootVisualElement.Q<Label>("perm-core-lbl");
        this.m_PermDamageLbl = this.m_Doc.rootVisualElement.Q<Label>("perm-dmg-lbl");
        this.m_PermRateLbl = this.m_Doc.rootVisualElement.Q<Label>("perm-rate-lbl");
        this.m_PermRepairLbl = this.m_Doc.rootVisualElement.Q<Label>("perm-repair-lbl");
        this.m_AuthStatusLbl = this.m_Doc.rootVisualElement.Q<Label>("auth-status-lbl");
        this.m_AuthStatusMenuLbl = this.m_Doc.rootVisualElement.Q<Label>("auth-status-lbl-menu");
        this.m_AdminStatusLbl = this.m_Doc.rootVisualElement.Q<Label>("admin-status-lbl");
        this.m_AdminUsersLbl = this.m_Doc.rootVisualElement.Q<Label>("admin-users-lbl");

        this.m_UpgradesPanel = this.m_Doc.rootVisualElement.Q<VisualElement>("upgrades-panel");
        this.m_AdminPanel = this.m_Doc.rootVisualElement.Q<VisualElement>("admin-panel");
        this.m_AuthCard = this.m_Doc.rootVisualElement.Q<VisualElement>("auth-card");
        this.m_MenuCard = this.m_Doc.rootVisualElement.Q<VisualElement>("menu-card");

        this.m_AuthUsernameInput = this.m_Doc.rootVisualElement.Q<TextField>("auth-username-input");
        this.m_AuthPasswordInput = this.m_Doc.rootVisualElement.Q<TextField>("auth-password-input");
        this.m_AdminSearchInput = this.m_Doc.rootVisualElement.Q<TextField>("admin-search-input");
        this.m_AdminTargetUserInput = this.m_Doc.rootVisualElement.Q<TextField>("admin-target-user-input");
        this.m_AdminDeltaInput = this.m_Doc.rootVisualElement.Q<TextField>("admin-delta-input");

        ApplyInputTheme();
        StartCoroutine(ApplyInputThemeDeferred());

        if (this.m_AuthPasswordInput != null)
            this.m_AuthPasswordInput.isPasswordField = true;

        if (this.m_PlayBtn != null)
            this.m_PlayBtn.clicked += TryStartGame;

        if (this.m_DifficultyEasyBtn != null)
            this.m_DifficultyEasyBtn.clicked += () => SetDifficulty(DifficultyLevel.Easy);
        if (this.m_DifficultyNormalBtn != null)
            this.m_DifficultyNormalBtn.clicked += () => SetDifficulty(DifficultyLevel.Normal);
        if (this.m_DifficultyHardBtn != null)
            this.m_DifficultyHardBtn.clicked += () => SetDifficulty(DifficultyLevel.Hard);

        if (this.m_UpgradesOpenBtn != null)
            this.m_UpgradesOpenBtn.clicked += TryOpenUpgrades;
        if (this.m_UpgradesCloseBtn != null)
            this.m_UpgradesCloseBtn.clicked += () => SetUpgradePanelVisible(false);

        if (this.m_AdminOpenBtn != null)
            this.m_AdminOpenBtn.clicked += TryOpenAdmin;
        if (this.m_AdminCloseBtn != null)
            this.m_AdminCloseBtn.clicked += () => SetAdminPanelVisible(false);
        if (this.m_AdminRefreshBtn != null)
            this.m_AdminRefreshBtn.clicked += () => StartCoroutine(RefreshAdminUsers());
        if (this.m_AdminGrantBtn != null)
            this.m_AdminGrantBtn.clicked += () => StartCoroutine(GrantAdminCoins());
        if (this.m_AdminResetBtn != null)
            this.m_AdminResetBtn.clicked += () => StartCoroutine(ResetAdminUser());
        if (this.m_AdminDeleteBtn != null)
            this.m_AdminDeleteBtn.clicked += () => StartCoroutine(DeleteAdminUser());
        if (this.m_AdminBanBtn != null)
            this.m_AdminBanBtn.clicked += () => StartCoroutine(SetBanState(true));
        if (this.m_AdminUnbanBtn != null)
            this.m_AdminUnbanBtn.clicked += () => StartCoroutine(SetBanState(false));

        if (this.m_AuthLoginBtn != null)
            this.m_AuthLoginBtn.clicked += TryLogin;
        if (this.m_AuthRegisterBtn != null)
            this.m_AuthRegisterBtn.clicked += TryRegister;
        if (this.m_SwitchAccountBtn != null)
            this.m_SwitchAccountBtn.clicked += DoLogout;

        if (this.m_PermCoreBtn != null)
            this.m_PermCoreBtn.clicked += () => BuyPermanentUpgrade(UpgradeType.CoreHealth);
        if (this.m_PermDamageBtn != null)
            this.m_PermDamageBtn.clicked += () => BuyPermanentUpgrade(UpgradeType.TowerDamage);
        if (this.m_PermRateBtn != null)
            this.m_PermRateBtn.clicked += () => BuyPermanentUpgrade(UpgradeType.TowerFireRate);
        if (this.m_PermRepairBtn != null)
            this.m_PermRepairBtn.clicked += () => BuyPermanentUpgrade(UpgradeType.RepairPower);

        UpdateDifficultyButtons();
        SetUpgradePanelVisible(false);
        SetAdminPanelVisible(false);
        RefreshUpgradePanel();
        RefreshAuthState("Войди или зарегистрируйся");

        UpgradeSystem.UpgradeValuesChanged += RefreshUpgradePanel;
        AuthSystem.AuthStateChanged += OnAuthChanged;

        if (this.m_QuitBtn != null)
        {
            this.m_QuitBtn.clicked += () =>
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            };
        }
    }

    public void RefreshMenuStats()
    {
        RefreshAuthState();
    }

    private void OnDestroy()
    {
        UpgradeSystem.UpgradeValuesChanged -= RefreshUpgradePanel;
        AuthSystem.AuthStateChanged -= OnAuthChanged;
    }

    private void SetDifficulty(DifficultyLevel difficulty)
    {
        DifficultySettings.Current = difficulty;
        UpdateDifficultyButtons();
    }

    private void ApplyInputTheme()
    {
        ApplyInputTheme(m_AuthUsernameInput);
        ApplyInputTheme(m_AuthPasswordInput);
        ApplyInputTheme(m_AdminSearchInput);
        ApplyInputTheme(m_AdminTargetUserInput);
        ApplyInputTheme(m_AdminDeltaInput);
    }

    private IEnumerator ApplyInputThemeDeferred()
    {
        yield return null;
        ApplyInputTheme();
        yield return null;
        ApplyInputTheme();
    }

    private void ApplyInputTheme(TextField field)
    {
        if (field == null)
            return;

        Color labelColor = new Color(0.74f, 0.93f, 0.98f, 0.96f);
        Color defaultInputColor = new Color(0.05f, 0.1f, 0.2f, 0.98f);
        Color hoverInputColor = new Color(0.08f, 0.15f, 0.26f, 0.98f);
        Color defaultBorder = new Color(0.47f, 0.86f, 0.91f, 0.52f);
        Color hoverBorder = new Color(0.75f, 0.94f, 0.99f, 0.72f);
        Color focusBorder = new Color(1f, 0.85f, 0.42f, 1f);
        Color textColor = new Color(0.95f, 0.98f, 1f, 1f);

        field.style.marginTop = 8;
        field.style.marginRight = 10;
        field.style.marginBottom = 8;
        field.style.marginLeft = 10;

        if (field.labelElement != null)
        {
            field.labelElement.style.color = labelColor;
        }

        ApplyInputVisualState(field, defaultInputColor, defaultBorder, textColor);

        const string callbackMarkerClass = "menu-input-theme-ready";
        if (field.ClassListContains(callbackMarkerClass))
            return;

        field.AddToClassList(callbackMarkerClass);
        field.RegisterCallback<MouseEnterEvent>(_ => ApplyInputVisualState(field, hoverInputColor, hoverBorder, textColor));
        field.RegisterCallback<MouseLeaveEvent>(_ => ApplyInputVisualState(field, defaultInputColor, defaultBorder, textColor));
        field.RegisterCallback<FocusInEvent>(_ =>
        {
            ApplyInputVisualState(field, hoverInputColor, focusBorder, textColor);
            if (field.labelElement != null)
                field.labelElement.style.color = focusBorder;
        });
        field.RegisterCallback<FocusOutEvent>(_ =>
        {
            ApplyInputVisualState(field, defaultInputColor, defaultBorder, textColor);
            if (field.labelElement != null)
                field.labelElement.style.color = labelColor;
        });
    }

    private void ApplyInputVisualState(TextField field, Color backgroundColor, Color borderColor, Color textColor)
    {
        if (field == null)
            return;

        field.style.color = textColor;
        ApplyInputVisual(GetTextInputElement(field), backgroundColor, borderColor, textColor);
        ApplyInputVisual(field.Q<VisualElement>(className: "unity-base-field__input"), backgroundColor, borderColor, textColor);
        ApplyInputVisual(field.Q<VisualElement>(className: "unity-base-text-field__input"), backgroundColor, borderColor, textColor);
        ApplyInputVisual(field.Q<VisualElement>(className: "unity-text-field__input"), backgroundColor, borderColor, textColor);
        ApplyInputVisual(field.Q<VisualElement>(className: "unity-text-input"), backgroundColor, borderColor, textColor);
    }

    private void ApplyInputVisual(VisualElement input, Color backgroundColor, Color borderColor, Color textColor)
    {
        if (input == null)
            return;

        input.style.minHeight = 48;
        input.style.paddingTop = 8;
        input.style.paddingRight = 12;
        input.style.paddingBottom = 8;
        input.style.paddingLeft = 12;
        input.style.backgroundColor = backgroundColor;
        input.style.color = textColor;
        input.style.borderTopWidth = 1;
        input.style.borderRightWidth = 1;
        input.style.borderBottomWidth = 1;
        input.style.borderLeftWidth = 1;
        input.style.borderTopLeftRadius = 12;
        input.style.borderTopRightRadius = 12;
        input.style.borderBottomRightRadius = 12;
        input.style.borderBottomLeftRadius = 12;
        SetBorderColor(input, borderColor);

        TextElement textElement = input.Q<TextElement>(className: "unity-text-element");
        if (textElement != null)
            textElement.style.color = textColor;
    }

    private VisualElement GetTextInputElement(TextField field)
    {
        VisualElement input = field.Q<VisualElement>(className: "unity-base-field__input");
        if (input != null)
            return input;

        input = field.Q<VisualElement>(className: "unity-base-text-field__input");
        if (input != null)
            return input;

        input = field.Q<VisualElement>(className: "unity-text-field__input");
        if (input != null)
            return input;

        input = field.Q<VisualElement>(className: "unity-text-input");
        return input;
    }

    private void SetBorderColor(VisualElement element, Color color)
    {
        if (element == null)
            return;

        element.style.borderTopColor = color;
        element.style.borderRightColor = color;
        element.style.borderBottomColor = color;
        element.style.borderLeftColor = color;
    }

    private void UpdateDifficultyButtons()
    {
        Color selected = Color.white;
        Color notSelected = new Color(0.35f, 0.35f, 0.35f, 1.0f);

        if (m_DifficultyEasyBtn != null)
            Util.SetBorderColor(m_DifficultyEasyBtn, DifficultySettings.Current == DifficultyLevel.Easy ? selected : notSelected);
        if (m_DifficultyNormalBtn != null)
            Util.SetBorderColor(m_DifficultyNormalBtn, DifficultySettings.Current == DifficultyLevel.Normal ? selected : notSelected);
        if (m_DifficultyHardBtn != null)
            Util.SetBorderColor(m_DifficultyHardBtn, DifficultySettings.Current == DifficultyLevel.Hard ? selected : notSelected);
    }

    private void SetUpgradePanelVisible(bool visible)
    {
        if (m_UpgradesPanel == null)
            return;

        m_UpgradesPanel.visible = visible;
        m_UpgradesPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetAdminPanelVisible(bool visible)
    {
        if (m_AdminPanel == null)
            return;

        m_AdminPanel.visible = visible;
        m_AdminPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void BuyPermanentUpgrade(UpgradeType type)
    {
        if (!AuthSystem.IsLoggedIn)
        {
            SetAuthStatus("Нужен вход для апгрейдов");
            AudioManager.Instance.PlaySfx("BuildFail");
            return;
        }

        bool success = UpgradeSystem.TryBuyPermanent(type);
        if (!success)
        {
            AudioManager.Instance.PlaySfx("BuildFail");
            return;
        }

        AudioManager.Instance.PlaySfx("TowerBuild");
        RefreshUpgradePanel();
    }

    private void RefreshUpgradePanel()
    {
        if (m_MetaCoinsLbl != null)
            m_MetaCoinsLbl.text = AuthSystem.IsLoggedIn ? $"COINS: {UpgradeSystem.MetaCoins}" : "COINS: -";

        SetUpgradeRow(m_PermCoreLbl, m_PermCoreBtn, UpgradeType.CoreHealth, "Core HP");
        SetUpgradeRow(m_PermDamageLbl, m_PermDamageBtn, UpgradeType.TowerDamage, "Tower Damage");
        SetUpgradeRow(m_PermRateLbl, m_PermRateBtn, UpgradeType.TowerFireRate, "Tower Fire Rate");
        SetUpgradeRow(m_PermRepairLbl, m_PermRepairBtn, UpgradeType.RepairPower, "Repair Power");
    }

    private void SetUpgradeRow(Label label, Button button, UpgradeType type, string title)
    {
        int level = UpgradeSystem.GetPermanentLevel(type);
        int cost = UpgradeSystem.GetPermanentCost(type);

        if (label != null)
        {
            string costStr = cost < 0 ? "MAX" : cost.ToString();
            label.text = $"{title}  Lv.{level}/{UpgradeSystem.PermanentMaxLevel}  Cost: {costStr}";
        }

        if (button != null)
        {
            bool canBuy = AuthSystem.IsLoggedIn && cost > 0 && UpgradeSystem.MetaCoins >= cost;
            button.text = cost < 0 ? "MAX" : "Купить";
            button.SetEnabled(cost >= 0 && AuthSystem.IsLoggedIn);
            button.style.opacity = canBuy || cost < 0 ? 1.0f : 0.55f;
        }
    }

    private void TryStartGame()
    {
        if (!AuthSystem.IsLoggedIn)
        {
            SetAuthStatus("Сначала войди в аккаунт");
            AudioManager.Instance.PlaySfx("BuildFail");
            return;
        }

        GameManager.Instance.ToInGame();
    }

    private void TryOpenUpgrades()
    {
        if (!AuthSystem.IsLoggedIn)
        {
            SetAuthStatus("Сначала войди в аккаунт");
            AudioManager.Instance.PlaySfx("BuildFail");
            return;
        }

        SetUpgradePanelVisible(true);
    }

    private void TryOpenAdmin()
    {
        if (!IsCurrentUserAdmin())
        {
            SetAdminStatus("Нет доступа к админ-панели");
            AudioManager.Instance.PlaySfx("BuildFail");
            return;
        }
        if (!m_BackendEnabled || string.IsNullOrEmpty(m_AdminBaseUrl))
        {
            SetAdminStatus("Backend auth не настроен в backend_config.json");
            AudioManager.Instance.PlaySfx("BuildFail");
            return;
        }

        SetAdminPanelVisible(true);
        StartCoroutine(RefreshAdminUsers());
    }

    private void TryRegister()
    {
        if (m_AuthRequestInFlight)
            return;
        StartCoroutine(RegisterRoutine());
    }

    private void TryLogin()
    {
        if (m_AuthRequestInFlight)
            return;
        StartCoroutine(LoginRoutine());
    }

    private IEnumerator RegisterRoutine()
    {
        if (!TryGetAuthInput(out string username, out string password))
            yield break;
        if (!TryEnsureBackendAuthEnabled())
            yield break;

        SetAuthStatus("Регистрация...");
        m_AuthRequestInFlight = true;
        yield return SendAuthRequest("auth/register", username, password, onSuccessPlayIntro: true);
        m_AuthRequestInFlight = false;
    }

    private IEnumerator LoginRoutine()
    {
        if (!TryGetAuthInput(out string username, out string password))
            yield break;
        if (!TryEnsureBackendAuthEnabled())
            yield break;

        SetAuthStatus("Вход...");
        m_AuthRequestInFlight = true;
        yield return SendAuthRequest("auth/login", username, password, onSuccessPlayIntro: false);
        m_AuthRequestInFlight = false;
    }

    private bool TryGetAuthInput(out string username, out string password)
    {
        username = (m_AuthUsernameInput != null ? m_AuthUsernameInput.value : string.Empty).Trim();
        password = m_AuthPasswordInput != null ? m_AuthPasswordInput.value : string.Empty;

        if (username.Length < 3)
        {
            SetAuthStatus("Логин минимум 3 символа");
            AudioManager.Instance.PlaySfx("BuildFail");
            return false;
        }
        if (password.Length < 6)
        {
            SetAuthStatus("Пароль минимум 6 символов");
            AudioManager.Instance.PlaySfx("BuildFail");
            return false;
        }

        return true;
    }

    private bool TryEnsureBackendAuthEnabled()
    {
        if (!m_BackendEnabled || string.IsNullOrEmpty(m_AdminBaseUrl))
        {
            SetAuthStatus("Backend auth отключен или не настроен");
            AudioManager.Instance.PlaySfx("BuildFail");
            return false;
        }

        return true;
    }

    private IEnumerator SendAuthRequest(string route, string username, string password, bool onSuccessPlayIntro)
    {
        AuthRequest payload = new AuthRequest
        {
            username = username,
            password = password
        };

        string url = $"{m_AdminBaseUrl}/{route}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = m_BackendTimeoutSec;
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                SetAuthStatus(ExtractApiError(req, $"Ошибка сети: {req.error}"));
                AudioManager.Instance.PlaySfx("BuildFail");
                yield break;
            }

            AuthResponse response = null;
            try
            {
                response = JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
            }
            catch (Exception ex)
            {
                SetAuthStatus($"Ошибка парсинга: {ex.Message}");
                AudioManager.Instance.PlaySfx("BuildFail");
                yield break;
            }

            if (response == null || string.IsNullOrWhiteSpace(response.username) || string.IsNullOrWhiteSpace(response.token))
            {
                SetAuthStatus("Невалидный ответ backend");
                AudioManager.Instance.PlaySfx("BuildFail");
                yield break;
            }

            AuthSystem.SetAuthenticatedSession(response.username, response.token, response.isAdmin, response.expiresUtc);
            if (m_AuthPasswordInput != null)
                m_AuthPasswordInput.value = string.Empty;
            SetAuthStatus($"AUTH: {response.username}");
            AudioManager.Instance.PlaySfx(onSuccessPlayIntro ? "TowerBuild" : "GameStart");

            if (onSuccessPlayIntro)
                ShowRegistrationIntro();
            else
                RefreshAuthState();
        }
    }

    private void DoLogout()
    {
        AuthSystem.Logout();
        SetUpgradePanelVisible(false);
        SetAdminPanelVisible(false);
        SetAuthStatus("Сессия завершена. Войди снова");
        AudioManager.Instance.PlaySfx("BuildFail");
    }

    private void OnAuthChanged()
    {
        RefreshAuthState();
    }

    private void ShowRegistrationIntro()
    {
        StoryBoard storyBoard = UiManager.Instance.GetUi<StoryBoard>();
        if (storyBoard == null)
        {
            RefreshAuthState();
            return;
        }

        UiManager.Instance.SetOnlyVisible<StoryBoard>();
        storyBoard.BeginIntroPlayback(OnRegistrationIntroFinished);
    }

    private void OnRegistrationIntroFinished()
    {
        UiManager.Instance.SetOnlyVisible<StartMenu>();
        RefreshAuthState();
    }

    private void RefreshAuthState(string fallbackMessage = "")
    {
        bool loggedIn = AuthSystem.IsLoggedIn;
        if (loggedIn)
            SetAuthStatus($"AUTH: {AuthSystem.CurrentUser}");
        else if (!string.IsNullOrEmpty(fallbackMessage))
            SetAuthStatus($"AUTH: {fallbackMessage}");
        else
            SetAuthStatus("AUTH: not logged in");

        if (m_PlayBtn != null)
        {
            m_PlayBtn.style.opacity = loggedIn ? 1.0f : 0.55f;
            m_PlayBtn.SetEnabled(loggedIn);
        }
        if (m_UpgradesOpenBtn != null)
        {
            m_UpgradesOpenBtn.style.opacity = loggedIn ? 1.0f : 0.55f;
            m_UpgradesOpenBtn.SetEnabled(loggedIn);
        }
        if (m_AdminOpenBtn != null)
        {
            bool admin = IsCurrentUserAdmin() && m_BackendEnabled && !string.IsNullOrEmpty(m_AdminBaseUrl);
            m_AdminOpenBtn.visible = admin;
            m_AdminOpenBtn.style.display = admin ? DisplayStyle.Flex : DisplayStyle.None;
        }

        SetElementVisible(m_AuthCard, !loggedIn);
        SetElementVisible(m_MenuCard, loggedIn);
        if (!loggedIn)
        {
            SetUpgradePanelVisible(false);
            SetAdminPanelVisible(false);
        }

        RefreshUpgradePanel();
    }

    private void SetElementVisible(VisualElement element, bool visible)
    {
        if (element == null)
            return;
        element.visible = visible;
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetAuthStatus(string text)
    {
        if (m_AuthStatusLbl != null)
            m_AuthStatusLbl.text = text;
        if (m_AuthStatusMenuLbl != null)
            m_AuthStatusMenuLbl.text = text;
    }

    private void SetAdminStatus(string text)
    {
        if (m_AdminStatusLbl != null)
            m_AdminStatusLbl.text = text;
    }

    private void LoadBackendConfig()
    {
        m_AdminBaseUrl = string.Empty;
        m_BackendEnabled = false;
        m_BackendTimeoutSec = 8;

        TextAsset cfgAsset = Resources.Load<TextAsset>("backend_config");
        if (cfgAsset == null || string.IsNullOrWhiteSpace(cfgAsset.text))
            return;

        CloudBackendConfig cfg = null;
        try
        {
            cfg = JsonUtility.FromJson<CloudBackendConfig>(cfgAsset.text);
        }
        catch { }

        if (cfg == null)
            return;

        m_AdminBaseUrl = (cfg.BaseUrl ?? string.Empty).TrimEnd('/');
        m_BackendEnabled = cfg.Enabled && !string.IsNullOrEmpty(m_AdminBaseUrl);
        m_BackendTimeoutSec = Mathf.Clamp(cfg.TimeoutSec, 3, 30);
    }

    private bool IsCurrentUserAdmin()
    {
        return AuthSystem.IsAdmin;
    }

    private IEnumerator RefreshAdminUsers()
    {
        if (!AuthSystem.HasValidAccessToken())
        {
            SetAdminStatus("Сессия истекла, войди заново");
            yield break;
        }

        string q = m_AdminSearchInput != null ? m_AdminSearchInput.value : string.Empty;
        string url = $"{m_AdminBaseUrl}/admin/users?limit=50&q={UnityWebRequest.EscapeURL(q ?? string.Empty)}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = m_BackendTimeoutSec;
            if (!TrySetAuthHeader(req, true))
                yield break;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                SetAdminStatus(ExtractApiError(req, $"Ошибка загрузки: {req.error}"));
                yield break;
            }

            AdminUsersResponse data = null;
            try
            {
                data = JsonUtility.FromJson<AdminUsersResponse>(req.downloadHandler.text);
            }
            catch (Exception ex)
            {
                SetAdminStatus($"Ошибка парсинга: {ex.Message}");
                yield break;
            }

            if (data == null || data.users == null)
            {
                if (m_AdminUsersLbl != null)
                    m_AdminUsersLbl.text = "Пользователи не найдены";
                SetAdminStatus("OK");
                yield break;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.users.Length; i++)
            {
                AdminUserDto u = data.users[i];
                string flag = u.banned ? $" [BANNED:{u.banReason}]" : string.Empty;
                sb.AppendLine($"{i + 1}. {u.username}{flag} | coins:{u.metaCoins} | C:{u.coreHealthLevel} D:{u.towerDamageLevel} F:{u.towerFireRateLevel} R:{u.repairPowerLevel}");
            }

            if (m_AdminUsersLbl != null)
                m_AdminUsersLbl.text = sb.Length == 0 ? "Пусто" : sb.ToString();
            SetAdminStatus($"Загружено: {data.users.Length}");
        }
    }

    private IEnumerator GrantAdminCoins()
    {
        string username = GetAdminTargetUser();
        if (string.IsNullOrEmpty(username))
            yield break;

        int delta = 0;
        if (m_AdminDeltaInput == null || !int.TryParse(m_AdminDeltaInput.value, out delta) || delta == 0)
        {
            SetAdminStatus("Delta должен быть числом (например 500)");
            yield break;
        }

        AdminGrantRequest payload = new AdminGrantRequest { username = username, delta = delta };
        yield return PostAdminJson("grant-coins", JsonUtility.ToJson(payload), "Монеты выданы");
        yield return RefreshAdminUsers();
    }

    private IEnumerator ResetAdminUser()
    {
        string username = GetAdminTargetUser();
        if (string.IsNullOrEmpty(username))
            yield break;

        AdminResetRequest payload = new AdminResetRequest { username = username };
        yield return PostAdminJson("reset-user", JsonUtility.ToJson(payload), "Профиль сброшен");
        yield return RefreshAdminUsers();
    }

    private IEnumerator DeleteAdminUser()
    {
        string username = GetAdminTargetUser();
        if (string.IsNullOrEmpty(username))
            yield break;

        AdminDeleteRequest payload = new AdminDeleteRequest { username = username };
        yield return PostAdminJson("delete-user", JsonUtility.ToJson(payload), "Пользователь удален");
        yield return RefreshAdminUsers();
    }

    private IEnumerator SetBanState(bool banned)
    {
        string username = GetAdminTargetUser();
        if (string.IsNullOrEmpty(username))
            yield break;

        AdminBanRequest payload = new AdminBanRequest
        {
            username = username,
            banned = banned,
            reason = banned ? "Banned by admin" : ""
        };

        yield return PostAdminJson("ban-user", JsonUtility.ToJson(payload), banned ? "Пользователь забанен" : "Пользователь разбанен");
        yield return RefreshAdminUsers();
    }

    private string GetAdminTargetUser()
    {
        string username = m_AdminTargetUserInput != null ? m_AdminTargetUserInput.value.Trim() : string.Empty;
        if (string.IsNullOrEmpty(username))
            SetAdminStatus("Укажи target user");
        return username;
    }

    private IEnumerator PostAdminJson(string route, string jsonBody, string successText)
    {
        if (!AuthSystem.HasValidAccessToken())
        {
            SetAdminStatus("Сессия истекла, войди заново");
            yield break;
        }

        string url = $"{m_AdminBaseUrl}/admin/{route}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = m_BackendTimeoutSec;
            req.SetRequestHeader("Content-Type", "application/json");
            if (!TrySetAuthHeader(req, true))
                yield break;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                SetAdminStatus(ExtractApiError(req, $"Ошибка: {req.error}"));
                yield break;
            }

            SetAdminStatus(successText);
        }
    }

    private bool TrySetAuthHeader(UnityWebRequest req, bool forAdminPanel)
    {
        string token = AuthSystem.AccessToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            if (forAdminPanel)
                SetAdminStatus("Требуется повторный вход");
            else
                SetAuthStatus("Требуется повторный вход");
            return false;
        }

        req.SetRequestHeader("Authorization", $"Bearer {token}");
        return true;
    }

    private string ExtractApiError(UnityWebRequest req, string fallback)
    {
        string text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        try
        {
            ApiErrorResponse error = JsonUtility.FromJson<ApiErrorResponse>(text);
            if (error != null)
            {
                if (!string.IsNullOrWhiteSpace(error.message))
                    return error.message;
                if (!string.IsNullOrWhiteSpace(error.error))
                    return error.error;
                if (!string.IsNullOrWhiteSpace(error.reason))
                    return error.reason;
            }
        }
        catch { }

        return fallback;
    }
}
