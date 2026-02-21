using System.Collections.Generic;
using UnityEngine;

public class GameSessionDirector : MonoBehaviour
{
    private const float TUTORIAL_DURATION = 30.0f;
    private const float WAVE_LENGTH = 60.0f;
    private const float NEXT_WAVE_WARNING = 8.0f;
    private const float MINI_BOSS_INTERVAL = 120.0f;
    private const float MINI_BOSS_FIRST_DELAY = 55.0f;
    private const float MINI_BOSS_SPAWN_DISTANCE = 16.0f;
    private const float MINI_BOSS_KILL_TRIGGER_RATIO = 0.6f;
    private const float MAP_AUTOSWITCH_TIME = 210.0f;
    private const float PORTAL_REPEAT_GRACE_TIME = 1.0f;
    private const float FINAL_MAP_PORTAL_ARM_DELAY = 8.0f;
    private const float FINAL_MAP_PORTAL_MIN_TIME_BEFORE_WIN = 12.0f;
    private const int TOTAL_MAPS = 3;
    private static readonly int[] MAP_KILL_GOALS = new int[] { 35, 65, 100 };
    private static readonly Vector3[] MAP_PLAYER_POSITIONS = new Vector3[]
    {
        new Vector3(0.0f, 0.0f, 0.0f),
        new Vector3(140.0f, -230.0f, 0.0f),
        new Vector3(-20.0f, 220.0f, 0.0f),
    };
    private static readonly Vector3[] MAP_PORTAL_POSITIONS = new Vector3[]
    {
        new Vector3(110.0f, 120.0f, 0.0f),
        new Vector3(220.0f, -250.0f, 0.0f),
        new Vector3(141.9f, 785.6f, 0.0f),
    };
    private static readonly Collider2D[] OVERLAP_BUFFER = new Collider2D[256];

    private static GameSessionDirector s_Instance;

    private bool m_SessionActive = false;
    private float m_SessionTime = 0.0f;
    private float m_NextBossTime = MINI_BOSS_INTERVAL;
    private int m_Wave = 1;
    private int m_MapStartKillCount = 0;
    private float m_CoreHealthAtWaveStart = -1.0f;
    private float m_LastManualAdvanceTime = -10.0f;
    private float m_FinalMapPortalUnlockTime = 0.0f;
    private Portal m_Portal;
    private bool m_MapBossSpawned = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (s_Instance != null)
            return;
        GameObject go = new GameObject("GameSessionDirector");
        s_Instance = go.AddComponent<GameSessionDirector>();
        DontDestroyOnLoad(go);
    }

    private void ResetSession()
    {
        m_SessionActive = false;
        m_SessionTime = 0.0f;
        m_NextBossTime = MINI_BOSS_FIRST_DELAY;
        m_Wave = 1;
        m_MapStartKillCount = 0;
        m_LastManualAdvanceTime = -10.0f;
        m_FinalMapPortalUnlockTime = 0.0f;
        m_MapBossSpawned = false;
        DifficultySettings.CurrentMap = 1;
        DifficultySettings.CurrentWave = 1;
        m_CoreHealthAtWaveStart = -1.0f;
    }

    private void EnsureSessionStarted()
    {
        if (m_SessionActive)
            return;

        if (GameManager.Instance == null || GameManager.Instance.CurrGameState != GameState.InGame)
            return;
        if (LevelManager.Instance == null || LevelManager.Instance.Player == null)
            return;

        m_SessionActive = true;
        m_SessionTime = 0.0f;
        m_NextBossTime = MINI_BOSS_FIRST_DELAY;
        m_Wave = 1;
        m_MapStartKillCount = GameStat.Instance.KillCount;
        DifficultySettings.CurrentMap = 1;
        DifficultySettings.CurrentWave = 1;
        m_CoreHealthAtWaveStart = LevelManager.Instance.Player.ShipBuilder.CoreHealth;
        m_MapBossSpawned = false;
        m_FinalMapPortalUnlockTime = 0.0f;
        ApplyMapLayout();
        SetAlert("MAP 1 STARTED");
        CancelInvoke(nameof(ClearAlert));
        Invoke(nameof(ClearAlert), 3.0f);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrGameState != GameState.InGame)
        {
            ResetSession();
            return;
        }

        EnsureSessionStarted();
        if (!m_SessionActive)
            return;

        m_SessionTime += Time.deltaTime;
        TryProgressMapByKills();
        TryProgressMapByTime();

        int nextWave = Mathf.Max(1, Mathf.FloorToInt(m_SessionTime / WAVE_LENGTH) + 1);
        if (nextWave != m_Wave)
        {
            RewardPerfectWave();
            m_Wave = nextWave;
            DifficultySettings.CurrentWave = m_Wave;
            m_CoreHealthAtWaveStart = LevelManager.Instance.Player.ShipBuilder.CoreHealth;
            SetAlert($"WAVE {m_Wave} STARTED!");
            CancelInvoke(nameof(ClearAlert));
            Invoke(nameof(ClearAlert), 3.0f);
        }

        UpdateHudText();
        HandleMiniBoss();
    }

    private void UpdateHudText()
    {
        InGameHud hud = UiManager.Instance.GetUi<InGameHud>();
        if (hud == null)
            return;

        int mapKillGoal = GetCurrentMapKillGoal();
        int mapKills = Mathf.Max(0, GameStat.Instance.KillCount - m_MapStartKillCount);
        hud.SetWaveLabel($"MAP {DifficultySettings.CurrentMap}/{TOTAL_MAPS} | {DifficultySettings.Current.ToString().ToUpper()} | WAVE {m_Wave} | KILLS {mapKills}/{mapKillGoal}");

        float timeToNextWave = WAVE_LENGTH - (m_SessionTime % WAVE_LENGTH);
        if (timeToNextWave <= NEXT_WAVE_WARNING && timeToNextWave > 0.1f)
        {
            hud.SetAlertLabel($"NEXT WAVE IN {Mathf.CeilToInt(timeToNextWave)}");
        }

        if (m_SessionTime <= TUTORIAL_DURATION)
        {
            if (m_SessionTime < 10.0f)
                hud.SetTutorialLabel("Держи SPACE, чтобы строить плитки и башни.");
            else if (m_SessionTime < 20.0f)
                hud.SetTutorialLabel("Укрепляй путь к порталу и не дай разрушить ядро.");
            else
                hud.SetTutorialLabel("R чинит ядро, T чинит сеть, а существующие башни можно улучшать.");
        }
        else
        {
            hud.SetTutorialLabel(string.Empty);
        }
    }

    private void HandleMiniBoss()
    {
        if (m_MapBossSpawned)
            return;

        int mapKillGoal = GetCurrentMapKillGoal();
        int mapKills = Mathf.Max(0, GameStat.Instance.KillCount - m_MapStartKillCount);
        bool timeTrigger = m_SessionTime >= m_NextBossTime && m_SessionTime >= MINI_BOSS_FIRST_DELAY;
        bool killTrigger = mapKills >= Mathf.CeilToInt(mapKillGoal * MINI_BOSS_KILL_TRIGGER_RATIO);

        if (!timeTrigger && !killTrigger)
            return;

        if (SpawnMiniBoss())
        {
            m_MapBossSpawned = true;
            SetAlert("MINI-BOSS INBOUND! + BONUS REWARD");
            CancelInvoke(nameof(ClearAlert));
            Invoke(nameof(ClearAlert), 4.0f);
            m_NextBossTime = m_SessionTime + MINI_BOSS_INTERVAL;
        }
        else
        {
            // Retry soon if spawn failed.
            m_NextBossTime = m_SessionTime + 10.0f;
        }
    }

    private void RewardPerfectWave()
    {
        if (LevelManager.Instance == null || LevelManager.Instance.Player == null)
            return;
        if (m_CoreHealthAtWaveStart < 0)
            return;

        float currentCoreHealth = LevelManager.Instance.Player.ShipBuilder.CoreHealth;
        if (currentCoreHealth >= m_CoreHealthAtWaveStart - 0.01f)
        {
            int bonus = 120 + (m_Wave * 20);
            GameStat.Instance.AddEssence(bonus);
            SetAlert($"PERFECT WAVE BONUS +{bonus}");
            CancelInvoke(nameof(ClearAlert));
            Invoke(nameof(ClearAlert), 3.0f);
        }
    }

    private bool SpawnMiniBoss()
    {
        if (LevelManager.Instance == null || LevelManager.Instance.Player == null)
            return false;

        GameObject prefab = null;
        EnemySpawnerLevel[] spawners = Object.FindObjectsByType<EnemySpawnerLevel>(FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i].m_EnemyPool == null || spawners[i].m_EnemyPool.Length == 0)
                continue;
            for (int j = 0; j < spawners[i].m_EnemyPool.Length; j++)
            {
                if (spawners[i].m_EnemyPool[j] != null)
                {
                    prefab = spawners[i].m_EnemyPool[j];
                    break;
                }
            }
            if (prefab != null)
                break;
        }

        if (prefab == null)
        {
            Enemy[] activeEnemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            if (activeEnemies.Length > 0)
            {
                prefab = activeEnemies[Random.Range(0, activeEnemies.Length)].gameObject;
            }
        }

        if (prefab == null)
            return false;

        Vector2 spawnPos = (Vector2)LevelManager.Instance.Player.transform.position + Random.insideUnitCircle.normalized * MINI_BOSS_SPAWN_DISTANCE;
        GameObject bossObj = Instantiate(prefab, spawnPos, Quaternion.identity);
        Enemy boss = bossObj.GetComponent<Enemy>();
        if (boss == null)
        {
            Destroy(bossObj);
            return false;
        }

        boss.InitializeEnemy();
        boss.ApplyMiniBossBuff();
        bossObj.name = $"{bossObj.name}_MiniBoss";
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx("EnemySpawn");
        return true;
    }

    private void TryProgressMapByKills()
    {
        int mapKills = Mathf.Max(0, GameStat.Instance.KillCount - m_MapStartKillCount);
        if (mapKills < GetCurrentMapKillGoal())
            return;
        if (DifficultySettings.CurrentMap >= TOTAL_MAPS)
            return;

        AdvanceToNextMapInternal();
    }

    private void AdvanceToNextMapInternal()
    {
        DifficultySettings.CurrentMap += 1;
        m_LastManualAdvanceTime = Time.time;
        DifficultySettings.CurrentWave = 1;
        m_Wave = 1;
        m_SessionTime = 0.0f;
        m_NextBossTime = MINI_BOSS_FIRST_DELAY;
        m_MapStartKillCount = GameStat.Instance.KillCount;
        m_CoreHealthAtWaveStart = LevelManager.Instance.Player.ShipBuilder.CoreHealth;
        m_MapBossSpawned = false;
        m_FinalMapPortalUnlockTime = DifficultySettings.CurrentMap >= TOTAL_MAPS ? Time.time + FINAL_MAP_PORTAL_ARM_DELAY : 0.0f;

        ApplyMapLayout();

        int mapBonus = 350 + DifficultySettings.CurrentMap * 150;
        GameStat.Instance.AddEssence(mapBonus);
        SetAlert($"MAP {DifficultySettings.CurrentMap} STARTED! BONUS +{mapBonus}");
        CancelInvoke(nameof(ClearAlert));
        Invoke(nameof(ClearAlert), 5.0f);
    }

    private void TryProgressMapByTime()
    {
        if (DifficultySettings.CurrentMap >= TOTAL_MAPS)
            return;
        if (m_SessionTime < MAP_AUTOSWITCH_TIME)
            return;

        AdvanceToNextMapInternal();
    }

    public static bool AdvanceMapViaPortal()
    {
        if (s_Instance == null)
            return false;
        if (DifficultySettings.CurrentMap >= TOTAL_MAPS)
            return false;
        if (Time.time - s_Instance.m_LastManualAdvanceTime < PORTAL_REPEAT_GRACE_TIME)
            return false;
        s_Instance.AdvanceToNextMapInternal();
        return true;
    }

    public static bool IsPortalRepeatGraceActive()
    {
        if (s_Instance == null)
            return false;
        return Time.time - s_Instance.m_LastManualAdvanceTime < PORTAL_REPEAT_GRACE_TIME;
    }

    public static bool IsPortalWinLocked()
    {
        if (s_Instance == null)
            return true;
        if (DifficultySettings.CurrentMap < TOTAL_MAPS)
            return true;
        if (IsPortalRepeatGraceActive())
            return true;
        if (Time.time < s_Instance.m_FinalMapPortalUnlockTime)
            return true;
        if (s_Instance.m_SessionTime < FINAL_MAP_PORTAL_MIN_TIME_BEFORE_WIN)
            return true;
        return false;
    }

    private void SetAlert(string value)
    {
        InGameHud hud = UiManager.Instance.GetUi<InGameHud>();
        if (hud != null)
            hud.SetAlertLabel(value);
    }

    private void ClearAlert()
    {
        InGameHud hud = UiManager.Instance.GetUi<InGameHud>();
        if (hud != null)
            hud.SetAlertLabel(string.Empty);
    }

    private void ApplyMapLayout()
    {
        if (LevelManager.Instance == null || LevelManager.Instance.Player == null)
            return;

        int mapIndex = Mathf.Clamp(DifficultySettings.CurrentMap - 1, 0, TOTAL_MAPS - 1);
        Player player = LevelManager.Instance.Player;
        Vector3 targetPlayerPosition = MAP_PLAYER_POSITIONS[mapIndex];

        ClearActiveEnemies();

        Vector3 safePlayerPosition = ResolveSafePlayerPosition(player, targetPlayerPosition);
        TeleportPlayer(player, safePlayerPosition);

        if (CameraFollow.Instance != null)
            CameraFollow.Instance.transform.position = CameraFollow.Instance.Offset + safePlayerPosition;

        if (m_Portal == null)
            m_Portal = Object.FindFirstObjectByType<Portal>();
        if (m_Portal != null)
            m_Portal.transform.position = MAP_PORTAL_POSITIONS[mapIndex];
    }

    private Vector3 ResolveSafePlayerPosition(Player player, Vector3 preferredPosition)
    {
        if (player == null)
            return preferredPosition;

        Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(false);
        if (playerColliders == null || playerColliders.Length == 0)
            return preferredPosition;

        const float radiusStep = 1.75f;
        const int ringCount = 34;
        const int samplesPerRing = 36;

        Vector3 bestPosition = preferredPosition;
        int bestOverlapCount;

        TeleportPlayer(player, preferredPosition);
        Physics2D.SyncTransforms();
        bestOverlapCount = CountBlockingOverlaps(player.transform, playerColliders);
        if (bestOverlapCount == 0)
            return preferredPosition;

        for (int ring = 1; ring <= ringCount; ring++)
        {
            float radius = ring * radiusStep;
            for (int i = 0; i < samplesPerRing; i++)
            {
                float t = (float)i / samplesPerRing;
                float angle = t * Mathf.PI * 2.0f;
                Vector3 candidate = preferredPosition + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0.0f) * radius;

                TeleportPlayer(player, candidate);
                Physics2D.SyncTransforms();

                int overlapCount = CountBlockingOverlaps(player.transform, playerColliders);
                if (overlapCount < bestOverlapCount)
                {
                    bestOverlapCount = overlapCount;
                    bestPosition = candidate;

                    if (bestOverlapCount == 0)
                        return bestPosition;
                }
            }
        }

        const int randomSamples = 72;
        const float randomRadius = 80.0f;
        for (int i = 0; i < randomSamples; i++)
        {
            Vector3 candidate = preferredPosition + (Vector3)(Random.insideUnitCircle * randomRadius);
            TeleportPlayer(player, candidate);
            Physics2D.SyncTransforms();

            int overlapCount = CountBlockingOverlaps(player.transform, playerColliders);
            if (overlapCount < bestOverlapCount)
            {
                bestOverlapCount = overlapCount;
                bestPosition = candidate;
                if (bestOverlapCount == 0)
                    return bestPosition;
            }
        }

        if (bestOverlapCount > 0)
        {
            Debug.LogWarning($"Could not find fully clear map spawn for player, using best overlap candidate ({bestOverlapCount} overlaps).");
        }

        return bestPosition;
    }

    private int CountBlockingOverlaps(Transform playerRoot, Collider2D[] playerColliders)
    {
        if (playerRoot == null || playerColliders == null)
            return 0;

        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = false,
            useDepth = false,
            useNormalAngle = false
        };

        HashSet<Collider2D> blockingColliders = new HashSet<Collider2D>();

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D ownCollider = playerColliders[i];
            if (ownCollider == null || !ownCollider.enabled || ownCollider.isTrigger)
                continue;

            int overlapCount = ownCollider.Overlap(filter, OVERLAP_BUFFER);
            for (int j = 0; j < overlapCount; j++)
            {
                Collider2D other = OVERLAP_BUFFER[j];
                if (other == null || !other.enabled || other.isTrigger)
                    continue;
                if (other.transform.IsChildOf(playerRoot))
                    continue;

                blockingColliders.Add(other);
            }
        }

        return blockingColliders.Count;
    }

    private void TeleportPlayer(Player player, Vector3 worldPosition)
    {
        if (player == null)
            return;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = worldPosition;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0.0f;
        }

        player.transform.position = worldPosition;
    }

    private void ClearActiveEnemies()
    {
        Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i].gameObject.SetActive(false);
        }
    }

    private int GetCurrentMapKillGoal()
    {
        int mapIndex = Mathf.Clamp(DifficultySettings.CurrentMap - 1, 0, MAP_KILL_GOALS.Length - 1);
        float difficultyScale = 1.0f;
        switch (DifficultySettings.Current)
        {
            case DifficultyLevel.Easy:
                difficultyScale = 0.85f;
                break;
            case DifficultyLevel.Hard:
                difficultyScale = 1.25f;
                break;
        }

        return Mathf.Max(1, Mathf.RoundToInt(MAP_KILL_GOALS[mapIndex] * difficultyScale));
    }
}
