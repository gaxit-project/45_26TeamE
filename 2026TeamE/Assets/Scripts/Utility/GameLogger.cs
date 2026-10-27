using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// デバッグ用のログをファイルに書き出す。エディターと Development Build のときだけ動く。
///
/// 起動するたびに Application.persistentDataPath/Logs/日時/ にフォルダを作り、次の3つを書く。
///   game.log      … Debug.Log の出力とゲーム内イベント（GameLogger.Event）を時系列で全部
///   errors.log    … エラー・例外だけ（スタックトレース付き）
///   telemetry.csv … 1秒ごとの FPS・座標・バッテリー・お金・残り時間など
///
/// どのシーンにも置く必要はなく、ゲーム起動時に自動で生成される。
/// </summary>
public static class GameLogger
{
    /// <summary>今回のログを書いているフォルダ。未初期化や製品版ビルドでは null。</summary>
    public static string SessionFolder { get; private set; }

    /// <summary>
    /// ゲーム内のイベントを game.log に記録する。
    /// 例: GameLogger.Event("DoorEnter", "pos=(0,-20,35)")
    /// 製品版ビルドでは呼び出しごと消えるので、引数の文字列組み立てのコストもかからない。
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Event(string name, string detail = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Write(gameLog, string.IsNullOrEmpty(detail) ? $"EVENT {name}" : $"EVENT {name} {detail}");
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    /// <summary>残しておくセッションフォルダの数。これより古いものは起動時に削除する。</summary>
    private const int MaxSessionFolders = 30;

    private static readonly object writeLock = new object();
    private static readonly Stopwatch clock = new Stopwatch();

    private static StreamWriter gameLog;
    private static StreamWriter errorLog;
    private static StreamWriter telemetry;

    // メインスレッド以外からのログでも書けるよう、メインスレッドで毎フレーム更新した値を使う
    private static volatile int currentFrame;
    private static volatile string currentScene = "";
    private static Vector3 currentPlayerPos;
    private static bool hasPlayer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // エディターでドメインリロードを切っている場合に二重に開かないようにする
        if (gameLog != null) return;

        try
        {
            string root = Path.Combine(Application.persistentDataPath, "Logs");
            DeleteOldSessions(root);

            SessionFolder = Path.Combine(root, DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(SessionFolder);

            gameLog = OpenWriter("game.log");
            errorLog = OpenWriter("errors.log");
            telemetry = OpenWriter("telemetry.csv");
        }
        catch (Exception e)
        {
            // ログが書けなくてもゲームは止めない
            Debug.LogWarning($"[GameLogger] ログフォルダを作れなかったため、ファイルへのログ出力を無効にします。{e.Message}");
            CloseAll();
            return;
        }

        clock.Start();
        currentScene = SceneManager.GetActiveScene().name;

        WriteSessionHeader();
        telemetry.WriteLine("time_s,frame,scene,fps,worst_frame_ms,pos_x,pos_y,pos_z,player_state,battery,money,time_left,mem_mb");

        Application.logMessageReceivedThreaded += OnUnityLog;
        Application.quitting += OnQuitting;
        SceneManager.sceneLoaded += OnSceneLoaded;

        var go = new GameObject("[GameLogger]");
        go.hideFlags = HideFlags.HideInHierarchy;
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<Sampler>();

        Debug.Log($"[GameLogger] ログ出力先: {SessionFolder}");
    }

    private static StreamWriter OpenWriter(string fileName)
    {
        // Excel で開いても文字化けしないよう BOM 付き UTF-8 にする
        return new StreamWriter(Path.Combine(SessionFolder, fileName), false, new UTF8Encoding(true)) { AutoFlush = true };
    }

    private static void DeleteOldSessions(string root)
    {
        if (!Directory.Exists(root)) return;

        // フォルダ名が日時なので、名前順＝古い順になる
        var old = Directory.GetDirectories(root)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        for (int i = 0; i <= old.Count - MaxSessionFolders; i++)
        {
            try { Directory.Delete(old[i], true); }
            catch (IOException) { /* 開いているなどで消せなければ次回に回す */ }
        }
    }

    private static void WriteSessionHeader()
    {
        Write(gameLog, "SESSION START");
        Write(gameLog, $"  version={Application.version} unity={Application.unityVersion} platform={Application.platform}");
        Write(gameLog, $"  os={SystemInfo.operatingSystem}");
        Write(gameLog, $"  cpu={SystemInfo.processorType} ({SystemInfo.processorCount} threads) ram={SystemInfo.systemMemorySize}MB");
        Write(gameLog, $"  gpu={SystemInfo.graphicsDeviceName} vram={SystemInfo.graphicsMemorySize}MB api={SystemInfo.graphicsDeviceType}");
        Write(gameLog, $"  args={string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentScene = scene.name;
        // 設定の保存・反映まわりのバグを追えるよう、シーンに入るたびに画面設定を残す
        Event("SceneLoaded", $"{scene.name} mode={mode} screen={Screen.width}x{Screen.height} {Screen.fullScreenMode} targetFps={Application.targetFrameRate} vSync={QualitySettings.vSyncCount}");
    }

    private static void OnUnityLog(string message, string stackTrace, LogType type)
    {
        // 自分自身が書いたイベントは Debug.Log を通さないので、ここに来るのは他のスクリプトとエンジンのログだけ
        string label = type switch
        {
            LogType.Exception => "EXCEPTION",
            LogType.Error => "ERROR",
            LogType.Assert => "ASSERT",
            LogType.Warning => "WARN",
            _ => "LOG",
        };

        Write(gameLog, $"{label} {message}");

        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
        {
            string trace = string.IsNullOrEmpty(stackTrace) ? "  (スタックトレースなし)" : Indent(stackTrace);
            Write(errorLog, $"{label} {message}\n{trace}");
        }
    }

    private static string Indent(string text)
    {
        return "  " + text.TrimEnd().Replace("\n", "\n  ");
    }

    private static void OnQuitting()
    {
        Write(gameLog, "SESSION END");
        Application.logMessageReceivedThreaded -= OnUnityLog;
        Application.quitting -= OnQuitting;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        CloseAll();
    }

    private static void CloseAll()
    {
        lock (writeLock)
        {
            gameLog?.Dispose();
            errorLog?.Dispose();
            telemetry?.Dispose();
            gameLog = errorLog = telemetry = null;
        }
    }

    private static void Write(StreamWriter writer, string body)
    {
        if (writer == null) return;

        string pos = hasPlayer
            ? string.Format(CultureInfo.InvariantCulture, "({0:F1},{1:F1},{2:F1})", currentPlayerPos.x, currentPlayerPos.y, currentPlayerPos.z)
            : "-";
        string line = string.Format(CultureInfo.InvariantCulture,
            "{0:HH:mm:ss.fff} [{1,9:F2}s f={2} {3} pos={4}] {5}",
            DateTime.Now, clock.Elapsed.TotalSeconds, currentFrame, currentScene, pos, body);

        lock (writeLock)
        {
            try { writer.WriteLine(line); }
            catch (ObjectDisposedException) { /* 終了処理と重なった場合は捨てる */ }
            catch (IOException) { /* ディスクがいっぱいなどでもゲームは止めない */ }
        }
    }

    /// <summary>毎フレームの状態を拾い、1秒ごとに telemetry.csv に1行書く。</summary>
    private sealed class Sampler : MonoBehaviour
    {
        private const float SampleInterval = 1f;

        private float windowStart;
        private int framesInWindow;
        private float worstFrameTime;

        private MoneyManager subscribedMoney;
        private int lastMoney;

        private void Start()
        {
            windowStart = Time.unscaledTime;
        }

        private void Update()
        {
            currentFrame = Time.frameCount;

            PlayerController player = PlayerController.Instance;
            hasPlayer = player != null;
            if (hasPlayer) currentPlayerPos = player.transform.position;

            WatchMoney();

            // ポーズ中（timeScale=0）も計測を続けるため、実時間で数える
            framesInWindow++;
            worstFrameTime = Mathf.Max(worstFrameTime, Time.unscaledDeltaTime);

            float elapsed = Time.unscaledTime - windowStart;
            if (elapsed >= SampleInterval)
            {
                WriteSample(player, framesInWindow / elapsed);
                windowStart = Time.unscaledTime;
                framesInWindow = 0;
                worstFrameTime = 0f;
            }
        }

        /// <summary>
        /// MoneyManager は途中のシーンで生成されるため、見つかった時点で購読してお金の増減を記録する。
        /// </summary>
        private void WatchMoney()
        {
            MoneyManager money = MoneyManager.Instance;
            if (money == subscribedMoney) return;

            if (subscribedMoney != null) subscribedMoney.OnMoneyChanged -= OnMoneyChanged;
            subscribedMoney = money;
            if (money == null) return;

            money.OnMoneyChanged += OnMoneyChanged;
            lastMoney = money.GetMoney();
        }

        private void OnMoneyChanged(int money)
        {
            int delta = money - lastMoney;
            lastMoney = money;
            if (delta == 0) return;
            Event("MoneyChanged", $"{money - delta} -> {money} ({delta:+#;-#})");
        }

        private void WriteSample(PlayerController player, float fps)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(160);

            sb.Append(clock.Elapsed.TotalSeconds.ToString("F1", c)).Append(',');
            sb.Append(Time.frameCount).Append(',');
            sb.Append(currentScene).Append(',');
            sb.Append(fps.ToString("F1", c)).Append(',');
            sb.Append((worstFrameTime * 1000f).ToString("F1", c)).Append(',');

            if (player != null)
            {
                Vector3 p = player.transform.position;
                sb.Append(p.x.ToString("F2", c)).Append(',');
                sb.Append(p.y.ToString("F2", c)).Append(',');
                sb.Append(p.z.ToString("F2", c)).Append(',');
                sb.Append(player.currentState).Append(',');
                sb.Append(player.currentBattery.ToString("F0", c)).Append(',');
            }
            else
            {
                sb.Append(",,,,,");
            }

            if (MoneyManager.Instance != null) sb.Append(MoneyManager.Instance.GetMoney());
            sb.Append(',');
            if (TimerManager.Instance != null) sb.Append(TimerManager.Instance.TotalTime.ToString("F1", c));
            sb.Append(',');
            sb.Append((GC.GetTotalMemory(false) / (1024f * 1024f)).ToString("F1", c));

            lock (writeLock)
            {
                try { telemetry?.WriteLine(sb.ToString()); }
                catch (ObjectDisposedException) { }
                catch (IOException) { }
            }
        }

        private void OnDestroy()
        {
            if (subscribedMoney != null) subscribedMoney.OnMoneyChanged -= OnMoneyChanged;
        }
    }

#endif
}
