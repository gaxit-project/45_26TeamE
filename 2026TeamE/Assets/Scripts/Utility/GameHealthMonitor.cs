#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲームの状態を見張って、バグの兆候を GameLogger に記録する。エディターと Development Build のみ
/// （テスター版でも動く。人が遊んでいるときに起きたものも拾うため）。
///
///   WallClip / OutOfStage … 体が土や岩の中に入り込んだ、ステージの外・底に出た（スクショ付き）
///   ValueAnomaly         … お金のマイナス、バッテリー・残り時間のマイナス、位置・速度が壊れた値（NaN など）
///   RunStart / RunSummary … 1プレイの始めの状態（強化レベル・所持金など）と、終わったときのまとめ
///   ZoneReached          … 新しい層に初めて着いた
///   MenuStuck            … 自動プレイ中に、タイトル・リザルトなどの画面から長く抜けられない（スクショ付き）
///   FrameSpike           … 1フレームが 100ms を超えた。そのとき何をしていたかも残す
/// </summary>
public sealed class GameHealthMonitor : MonoBehaviour
{
    private const float CheckInterval = 0.2f;
    /// <summary>体が埋まった状態がこの秒数続いたら記録する（掘っている最中の一瞬の重なりは数えない）。</summary>
    private const float ClipSeconds = 1f;
    private const float SpikeMs = 100f;
    /// <summary>シーンを読み込んだ直後は重いのが当たり前なので、この秒数は FrameSpike を数えない。</summary>
    private const float SpikeGraceAfterLoad = 2f;
    private const float MenuStuckSeconds = 60f;
    /// <summary>これより速く動いていたら、物理が壊れているとみなす。</summary>
    private const float MaxSaneSpeed = 100f;

    private float nextCheckTime;
    private float sceneLoadedAt;

    // 1プレイ
    private VoxelTerrain runTerrain;
    private VoxelTerrain endedTerrain;
    private float runStart;
    private float runMaxDepth;
    private int runMaxZone;
    private int runDoors;
    private int runDamage;
    private int runMoneyStart;
    private int runEarnedStart;
    private bool runBot;
    private bool wasShopOpen;
    private bool wasTakingDamage;

    // めり込み
    private float clipTime;
    private bool clipReported;

    // 値の異常（同じ種類は、正常に戻るまで1回だけ記録する）
    private readonly HashSet<string> activeAnomalies = new HashSet<string>();
    private Rigidbody playerBody;

    // メニューでの詰まり
    private bool menuReported;

    // 重いフレーム
    private int lastGcCount;
    private float nextSpikeLogTime;
    private int suppressedSpikes;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[GameHealthMonitor]");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<GameHealthMonitor>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameLogger.SessionEnding += OnSessionEnding;
        sceneLoadedAt = Time.unscaledTime;
        lastGcCount = GC.CollectionCount(0);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameLogger.SessionEnding -= OnSessionEnding;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        sceneLoadedAt = Time.unscaledTime;
        menuReported = false;
    }

    private void OnSessionEnding()
    {
        EndRun("quit");
    }

    private void Update()
    {
        CheckFrameSpike();

        if (Time.unscaledTime < nextCheckTime) return;
        nextCheckTime = Time.unscaledTime + CheckInterval;

        VoxelTerrain terrain = VoxelTerrain.Instance;
        PlayerController player = PlayerController.Instance;

        TrackRun(terrain, player);
        CheckPosition(terrain, player);
        CheckValues(player);
        CheckMenuStuck(player);
    }

    // ーーーーーーーーーーーーーーーーー1プレイのまとめーーーーーーーーーーーーーー

    private void TrackRun(VoxelTerrain terrain, PlayerController player)
    {
        // シーンを抜けて地形が消えた・変わった
        if (runTerrain != null && !ReferenceEquals(terrain, runTerrain)) EndRun("scene");

        if (terrain == null || !terrain.IsStageGenerated || terrain.IsGenerating || player == null) return;
        if (ReferenceEquals(terrain, endedTerrain)) return;

        if (runTerrain == null)
        {
            if (!player.IsControlEnabled) return;
            StartRun(terrain, player);
        }

        if (AutoPlayBot.Instance != null && AutoPlayBot.Instance.IsRunning) runBot = true;

        Vector3 p = player.transform.position;
        runMaxDepth = Mathf.Max(runMaxDepth, -p.y);

        int zone = ZoneOf(terrain, p);
        if (zone > runMaxZone)
        {
            runMaxZone = zone;
            GameLogger.Event("ZoneReached", string.Format(CultureInfo.InvariantCulture, "zone={0} after={1:F1}s", zone + 1, Time.time - runStart));
        }

        bool shopOpen = ShoppingManager.Instance != null && ShoppingManager.Instance.IsOpen;
        if (shopOpen && !wasShopOpen) runDoors++;
        wasShopOpen = shopOpen;

        bool takingDamage = player.currentState == PlayerController.PlayerState.TakingDamage;
        if (takingDamage && !wasTakingDamage) runDamage++;
        wasTakingDamage = takingDamage;

        if (player.currentState == PlayerController.PlayerState.GameClear) EndRun("clear");
        else if (player.currentState == PlayerController.PlayerState.GameOver) EndRun("timeup");
    }

    private void StartRun(VoxelTerrain terrain, PlayerController player)
    {
        runTerrain = terrain;
        runStart = Time.time;
        runMaxDepth = -player.transform.position.y;
        runMaxZone = ZoneOf(terrain, player.transform.position);
        runDoors = runDamage = 0;
        runBot = false;
        wasShopOpen = wasTakingDamage = false;
        runMoneyStart = MoneyManager.Instance != null ? MoneyManager.Instance.GetMoney() : 0;
        runEarnedStart = MoneyManager.Instance != null ? MoneyManager.Instance.GetTotalEarnedMoney() : 0;

        // 強化はプレイをまたいで残るので、「強化済みだから起きた」バグを再現できるよう始めの状態を残す
        var upgrades = new StringBuilder();
        foreach (string name in new[] { UpgradeManager.DRILL, UpgradeManager.SONAR, UpgradeManager.ENGINE, UpgradeManager.RADER, UpgradeManager.LIGHT, UpgradeManager.JET })
        {
            if (upgrades.Length > 0) upgrades.Append(',');
            upgrades.Append(name).Append(':').Append(UpgradeManager.GetLevel(name));
        }

        GameLogger.Event("RunStart", string.Format(CultureInfo.InvariantCulture,
            "seed={0} upgrades={1} money={2} battery={3:F0} timeLeft={4:F1}",
            VoxelTerrain.LastUsedSeed, upgrades, runMoneyStart, player.currentBattery,
            TimerManager.Instance != null ? TimerManager.Instance.TotalTime : -1f));
    }

    private void EndRun(string reason)
    {
        if (runTerrain == null) return;

        int moneyEnd = MoneyManager.Instance != null ? MoneyManager.Instance.GetMoney() : runMoneyStart;
        int earned = MoneyManager.Instance != null ? MoneyManager.Instance.GetTotalEarnedMoney() - runEarnedStart : 0;
        GameLogger.Event("RunSummary", string.Format(CultureInfo.InvariantCulture,
            "reason={0} seconds={1:F0} maxDepth={2:F0} zone={3} doors={4} damage={5} money={6}->{7} earned={8} bot={9}",
            reason, Time.time - runStart, runMaxDepth, runMaxZone + 1, runDoors, runDamage, runMoneyStart, moneyEnd, earned, runBot));

        // 時間切れ・クリアのあとも、シーンを抜けるまでは同じ地形が残るので、もう一度数え始めないよう覚えておく
        endedTerrain = runTerrain;
        runTerrain = null;
    }

    /// <summary>位置が何層目か（0 が一番上の層）。</summary>
    private static int ZoneOf(VoxelTerrain terrain, Vector3 worldPos)
    {
        if (terrain.HeightY <= 0) return 0;
        Vector3 local = terrain.transform.InverseTransformPoint(worldPos);
        int y = Mathf.Clamp(Mathf.RoundToInt(local.y / terrain.BlockSize), 0, terrain.HeightY - 1);
        return terrain.GetRelayID(y);
    }

    // ーーーーーーーーーーーーーーーーー壁へのめり込み・ステージの外ーーーーーーーーーーーーーー

    private void CheckPosition(VoxelTerrain terrain, PlayerController player)
    {
        if (runTerrain == null || player == null || !player.IsControlEnabled)
        {
            clipTime = 0f;
            return;
        }

        Vector3 p = player.transform.position;
        // 原点は足元なので、体の中心あたりを見る
        Vector3 center = p + Vector3.up * 1f;
        VoxelTerrain.BlockType? block = terrain.GetBlockAt(center);
        bool buried = block.HasValue && block.Value != VoxelTerrain.BlockType.Air;

        if (!buried)
        {
            if (clipReported)
            {
                GameLogger.Event("WallClipResolved", string.Format(CultureInfo.InvariantCulture, "after={0:F1}s", clipTime));
            }
            clipTime = 0f;
            clipReported = false;
            return;
        }

        clipTime += CheckInterval;
        if (clipReported || clipTime < ClipSeconds) return;
        clipReported = true;

        string shot = GameLogger.CaptureScreenshot("clip");
        Vector3 local = terrain.transform.InverseTransformPoint(center);
        if (block.Value == VoxelTerrain.BlockType.Boundary)
        {
            // 外周の壁の中、または各層の幅・ステージの底より外に出ている
            string where = local.y < 0f ? "below" : "outside";
            GameLogger.Event("OutOfStage", string.Format(CultureInfo.InvariantCulture,
                "where={0} depth={1:F1} for={2:F1}s screenshot={3}", where, -p.y, clipTime, shot));
        }
        else
        {
            GameLogger.Event("WallClip", string.Format(CultureInfo.InvariantCulture,
                "block={0} depth={1:F1} for={2:F1}s dashing={3} screenshot={4}", block.Value, -p.y, clipTime, player.IsDashing, shot));
        }
    }

    // ーーーーーーーーーーーーーーーーー値の異常ーーーーーーーーーーーーーー

    private void CheckValues(PlayerController player)
    {
        if (MoneyManager.Instance != null)
        {
            int money = MoneyManager.Instance.GetMoney();
            Check("money", money < 0, money.ToString(CultureInfo.InvariantCulture));
        }

        if (TimerManager.Instance != null)
        {
            float left = TimerManager.Instance.TotalTime;
            Check("timeLeft", !IsFinite(left) || left < 0f, left.ToString("F2", CultureInfo.InvariantCulture));
        }

        if (player == null)
        {
            playerBody = null;
            return;
        }

        Check("battery", !IsFinite(player.currentBattery) || player.currentBattery < 0f, player.currentBattery.ToString("F1", CultureInfo.InvariantCulture));

        Vector3 p = player.transform.position;
        Check("position", !IsFinite(p.x) || !IsFinite(p.y) || !IsFinite(p.z), p.ToString());

        if (playerBody == null) playerBody = player.GetComponent<Rigidbody>();
        if (playerBody != null)
        {
            Vector3 v = playerBody.linearVelocity;
            bool broken = !IsFinite(v.x) || !IsFinite(v.y) || !IsFinite(v.z) || v.magnitude > MaxSaneSpeed;
            Check("velocity", broken, v.magnitude.ToString("F1", CultureInfo.InvariantCulture));
        }
    }

    private void Check(string kind, bool abnormal, string value)
    {
        if (!abnormal)
        {
            if (activeAnomalies.Remove(kind)) GameLogger.Event("ValueAnomalyResolved", $"kind={kind}");
            return;
        }

        if (!activeAnomalies.Add(kind)) return;
        GameLogger.Event("ValueAnomaly", $"kind={kind} value={value}");
    }

    private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

    // ーーーーーーーーーーーーーーーーーメニューでの詰まりーーーーーーーーーーーーーー

    /// <summary>
    /// 自動プレイ中に、プレイヤーのいない画面（タイトル・リザルトなど）から長く抜けられないときに記録する。
    /// メインシーンでショップから戻れないなどは、AutoPlayBot の NoControl が拾う。
    /// </summary>
    private void CheckMenuStuck(PlayerController player)
    {
        bool botRunning = AutoPlayBot.Instance != null && AutoPlayBot.Instance.IsRunning;
        if (!botRunning || player != null || menuReported) return;

        float stay = Time.unscaledTime - sceneLoadedAt;
        if (stay < MenuStuckSeconds) return;

        menuReported = true;
        string shot = GameLogger.CaptureScreenshot("menu");
        GameLogger.Event("MenuStuck", string.Format(CultureInfo.InvariantCulture,
            "scene={0} for={1:F0}s timeScale={2} loading={3} screenshot={4}",
            SceneManager.GetActiveScene().name, stay, Time.timeScale,
            SceneLoader.Instance != null && SceneLoader.Instance.IsLoading, shot));
    }

    // ーーーーーーーーーーーーーーーーー重いフレームーーーーーーーーーーーーーー

    private void CheckFrameSpike()
    {
        int gc = GC.CollectionCount(0);
        bool gcHappened = gc != lastGcCount;
        lastGcCount = gc;

        float ms = Time.unscaledDeltaTime * 1000f;
        if (ms < SpikeMs) return;

        // 読み込み中・地形の生成中・別ウィンドウ作業中は重いのが当たり前なので数えない
        if (Time.unscaledTime - sceneLoadedAt < SpikeGraceAfterLoad) return;
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) return;
        VoxelTerrain terrain = VoxelTerrain.Instance;
        if (terrain != null && terrain.IsGenerating) return;
        if (!Application.isFocused) return;

        // 続けて起きることが多いので、1秒に1回にまとめる（まとめた回数も残す）
        if (Time.unscaledTime < nextSpikeLogTime)
        {
            suppressedSpikes++;
            return;
        }
        nextSpikeLogTime = Time.unscaledTime + 1f;

        PlayerController player = PlayerController.Instance;
        bool shopOpen = ShoppingManager.Instance != null && ShoppingManager.Instance.IsOpen;
        GameLogger.Event("FrameSpike", string.Format(CultureInfo.InvariantCulture,
            "ms={0:F0} chunks={1} drilling={2} dashing={3} sonar={4} gc={5} shop={6} more={7}",
            ms,
            terrain != null ? terrain.ChunksRebuiltLastFrame : 0,
            player != null && player.IsDrilling,
            player != null && player.IsDashing,
            FindObjectsByType<Sonar>().Length,
            gcHappened,
            shopOpen,
            suppressedSpikes));
        suppressedSpikes = 0;
    }
}
#endif
