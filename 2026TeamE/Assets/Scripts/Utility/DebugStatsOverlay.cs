#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

/// <summary>
/// デバッグ中、画面の隅に FPS・メモリ・CPU/GPU の処理時間などを表示する。エディターと Development Build のみ。
/// F8 キーで「右上 → 左上 → 非表示」と切り替える。
/// シーンに置く必要はなく、ゲーム起動時に自動で生成される。
/// </summary>
public sealed class DebugStatsOverlay : MonoBehaviour
{
    private const Key ToggleKey = Key.F8;

    /// <summary>表示を更新する間隔（秒）。毎フレーム文字列を作ると、それ自体が重くなるため間引く。</summary>
    private const float RefreshInterval = 0.5f;

    // 状態の色（色だけに頼らず、文字でも「良好／注意／危険」を出す）
    private static readonly Color GoodColor = new Color32(0x3d, 0xd6, 0x3d, 0xff);
    private static readonly Color WarnColor = new Color32(0xfa, 0xb2, 0x19, 0xff);
    private static readonly Color BadColor = new Color32(0xff, 0x6b, 0x6b, 0xff);

    private enum Corner { TopRight, TopLeft, Hidden }

    private Corner corner = Corner.TopRight;

    private float windowStart;
    private int framesInWindow;
    private float worstFrameTime;

    private float fps;
    private float worstMs;
    private Color fpsColor = Color.white;
    private string fpsLabel = "";
    private string body = "";

    private readonly FrameTiming[] frameTimings = new FrameTiming[1];

    private GUIStyle boxStyle;
    private GUIStyle textStyle;
    private GUIStyle headStyle;
    private Texture2D background;
    private int styledHeight;

    // テスター版（TESTER_BUILD）では表示しない（遊ぶ人の邪魔になるため）
#if !TESTER_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
#endif
    private static void Bootstrap()
    {
        var go = new GameObject("[DebugStatsOverlay]");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<DebugStatsOverlay>();
    }

    private void Start()
    {
        windowStart = Time.unscaledTime;
    }

    private void OnDestroy()
    {
        if (background != null) Destroy(background);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb[ToggleKey].wasPressedThisFrame)
        {
            corner = (Corner)(((int)corner + 1) % 3);
        }

        // CPU/GPU の処理時間を取るため、表示していなくても毎フレーム記録だけはしておく
        FrameTimingManager.CaptureFrameTimings();

        framesInWindow++;
        worstFrameTime = Mathf.Max(worstFrameTime, Time.unscaledDeltaTime);

        float elapsed = Time.unscaledTime - windowStart;
        if (elapsed < RefreshInterval) return;

        fps = framesInWindow / elapsed;
        worstMs = worstFrameTime * 1000f;
        windowStart = Time.unscaledTime;
        framesInWindow = 0;
        worstFrameTime = 0f;

        if (corner != Corner.Hidden) RebuildText();
    }

    private void RebuildText()
    {
        int target = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
        if (fps >= target * 0.9f) { fpsColor = GoodColor; fpsLabel = "良好"; }
        else if (fps >= 30f) { fpsColor = WarnColor; fpsLabel = "注意"; }
        else { fpsColor = BadColor; fpsLabel = "危険"; }

        var sb = new StringBuilder(512);

        sb.Append("フレーム時間  平均 ").Append((1000f / Mathf.Max(fps, 0.01f)).ToString("F1"))
          .Append("ms / 最大 ").Append(worstMs.ToString("F1")).Append("ms\n");

        if (FrameTimingManager.GetLatestTimings(1, frameTimings) > 0)
        {
            sb.Append("CPU ").Append(frameTimings[0].cpuFrameTime.ToString("F1")).Append("ms  ");
            sb.Append("GPU ").Append(frameTimings[0].gpuFrameTime > 0 ? frameTimings[0].gpuFrameTime.ToString("F1") + "ms" : "-").Append('\n');
        }
        else
        {
            sb.Append("CPU/GPU  -（Player Settings の Frame Timing Stats が無効）\n");
        }

        long allocated = Profiler.GetTotalAllocatedMemoryLong();
        long reserved = Profiler.GetTotalReservedMemoryLong();
        long mono = Profiler.GetMonoUsedSizeLong();
        sb.Append("メモリ  使用 ").Append(ToMb(allocated)).Append(" / 確保 ").Append(ToMb(reserved))
          .Append(" / スクリプト ").Append(ToMb(mono)).Append('\n');
        sb.Append("搭載RAM ").Append(SystemInfo.systemMemorySize).Append("MB  VRAM ").Append(SystemInfo.graphicsMemorySize).Append("MB\n");

        sb.Append("画面  ").Append(Screen.width).Append('x').Append(Screen.height).Append(' ')
          .Append(Screen.fullScreenMode == FullScreenMode.Windowed ? "ウィンドウ" : "フルスクリーン")
          .Append("  目標 ").Append(Application.targetFrameRate > 0 ? Application.targetFrameRate + "fps" : "無制限")
          .Append("  VSync ").Append(QualitySettings.vSyncCount > 0 ? "ON" : "OFF").Append('\n');

        sb.Append("シーン  ").Append(SceneManager.GetActiveScene().name)
          .Append("  時間の速さ ").Append(Time.timeScale.ToString("0.##")).Append('\n');

        PlayerController player = PlayerController.Instance;
        if (player != null)
        {
            Vector3 p = player.transform.position;
            sb.Append("プレイヤー  横 ").Append(p.z.ToString("F1")).Append("  高さ ").Append(p.y.ToString("F1"))
              .Append("  バッテリー ").Append(player.currentBattery.ToString("F0")).Append('\n');
        }

        AutoPlayBot bot = AutoPlayBot.Instance;
        if (bot != null && bot.IsRunning)
        {
            sb.Append("自動プレイ  実行中  詰まり ").Append(bot.StuckCount).Append("回");
            if (bot.CurrentCoverage.HasValue)
            {
                sb.Append("  カバー率 ").Append((bot.CurrentCoverage.Value * 100f).ToString("F1")).Append('%');
            }
            sb.Append('\n');
        }
        else
        {
            sb.Append("自動プレイ  停止中\n");
        }

        sb.Append("ウィンドウ  ").Append(Application.isFocused ? "操作中" : "他のウィンドウで作業中")
          .Append("（バックグラウンド実行 ").Append(DebugBackgroundRunner.IsActive ? "ON" : "OFF").Append("）\n");

        sb.Append("v").Append(Application.version).Append("   F8 表示切替 / F9 自動プレイ");
        body = sb.ToString();
    }

    private static string ToMb(long bytes) => (bytes / (1024f * 1024f)).ToString("F0") + "MB";

    private void OnGUI()
    {
        if (corner == Corner.Hidden) return;
        if (string.IsNullOrEmpty(body)) RebuildText();

        EnsureStyles();

        float scale = Screen.height / 1080f;
        float width = 470f * scale;
        float pad = 10f * scale;

        string head = $"FPS {fps:F0}  {fpsLabel}";
        float headHeight = headStyle.CalcHeight(new GUIContent(head), width - pad * 2);
        float bodyHeight = textStyle.CalcHeight(new GUIContent(body), width - pad * 2);
        float height = headHeight + bodyHeight + pad * 2;

        float x = corner == Corner.TopRight ? Screen.width - width - pad : pad;
        var rect = new Rect(x, pad, width, height);

        GUI.Box(rect, GUIContent.none, boxStyle);

        headStyle.normal.textColor = fpsColor;
        GUI.Label(new Rect(rect.x + pad, rect.y + pad, width - pad * 2, headHeight), head, headStyle);
        GUI.Label(new Rect(rect.x + pad, rect.y + pad + headHeight, width - pad * 2, bodyHeight), body, textStyle);
    }

    /// <summary>画面の高さに合わせて文字の大きさを変える。解像度を変えたときだけ作り直す。</summary>
    private void EnsureStyles()
    {
        if (boxStyle != null && styledHeight == Screen.height) return;
        styledHeight = Screen.height;
        float scale = Screen.height / 1080f;

        if (background == null)
        {
            background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
            background.Apply();
        }

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = background;

        textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(10, Mathf.RoundToInt(17 * scale)),
            wordWrap = true,
            richText = false,
        };
        textStyle.normal.textColor = new Color(0.94f, 0.94f, 0.92f);

        headStyle = new GUIStyle(textStyle)
        {
            fontSize = Mathf.Max(14, Mathf.RoundToInt(26 * scale)),
            fontStyle = FontStyle.Bold,
        };
    }
}
#endif
