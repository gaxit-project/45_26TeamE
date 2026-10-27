#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// テストプレイしている人の名前。人のプレイ記録（HumanRoute）に残し、誰のプレイかを区別するために使う。
///
/// 名前は次の順で決める。
///   1. 起動引数 "-tester 名前"
///   2. 前回入力した名前（PlayerPrefs）
///   3. テスター版（TESTER_BUILD）なら、起動時に入力してもらう。開発中（エディター等）は PC のユーザー名
/// </summary>
public static class TesterProfile
{
    private const string PrefKey = "AutoPlay.TesterName";
    private const int MaxNameLength = 20;

    /// <summary>テスターに配るビルドか。自動プレイやデバッグ表示を無効にし、記録だけを取る。</summary>
#if TESTER_BUILD
    public const bool IsTesterBuild = true;
#else
    public const bool IsTesterBuild = false;
#endif

    /// <summary>テスターの名前。まだ決まっていなければ空。</summary>
    public static string Name { get; private set; } = "";

    /// <summary>ファイル名に使える形にした名前。</summary>
    /// （エディターでプレイしていないときにメニューから書き出す場合は、まだ名前が無いので PC のユーザー名を使う）
    public static string FileSafeName => Sanitize(string.IsNullOrEmpty(Name) && !IsTesterBuild ? Environment.UserName : Name);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-tester");
        if (i >= 0 && i + 1 < args.Length) Name = args[i + 1].Trim();
        if (string.IsNullOrEmpty(Name)) Name = PlayerPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(Name) && !IsTesterBuild) Name = Environment.UserName;

        var go = new GameObject("[TesterPanel]");
        go.hideFlags = HideFlags.HideInHierarchy;
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<TesterPanel>();
    }

    public static void SetName(string name)
    {
        Name = name.Trim();
        if (Name.Length > MaxNameLength) Name = Name.Substring(0, MaxNameLength);
        PlayerPrefs.SetString(PrefKey, Name);
        PlayerPrefs.Save();
        GameLogger.Event("TesterName", $"name={FileSafeName}");
    }

    /// <summary>ファイル名に使えない文字と空白を「_」にする。空なら "unknown"。</summary>
    public static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unknown";
        var sb = new StringBuilder(name.Length);
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char ch in name.Trim())
        {
            sb.Append(Array.IndexOf(invalid, ch) >= 0 || char.IsWhiteSpace(ch) || ch == ',' || ch == '=' ? '_' : ch);
        }
        return sb.ToString();
    }
}

/// <summary>
/// テスター向けの画面。名前の入力と、タイトル画面での「記録を書き出す」ボタンを出す。
/// ゲームの UI とは別に、OnGUI で描いている（どのシーンにも置かなくてよいように）。
/// </summary>
public sealed class TesterPanel : MonoBehaviour
{
    private const string TitleScene = "01_Title";

    private bool prompting;
    private string nameInput = "";
    private string message = "";
    private float messageUntil;
    private readonly List<InputDevice> disabledDevices = new List<InputDevice>();

    private GUIStyle boxStyle, labelStyle, buttonStyle, fieldStyle, smallStyle;
    private int styledHeight;
    private Texture2D background;

    private void Start()
    {
        if (string.IsNullOrEmpty(TesterProfile.Name)) OpenPrompt();
    }

    private void OnDestroy()
    {
        ReleaseInput();
        if (background != null) Destroy(background);
    }

    private void OpenPrompt()
    {
        prompting = true;
        nameInput = TesterProfile.Name;

        // 名前を打っている間に、Enter やスペースでゲームが進まないようにする（OnGUI の文字入力は Input System と別なので打てる）
        foreach (InputDevice device in InputSystem.devices)
        {
            if (device.enabled && (device is Keyboard || device is Mouse || device is Gamepad))
            {
                InputSystem.DisableDevice(device);
                disabledDevices.Add(device);
            }
        }
    }

    private void ReleaseInput()
    {
        foreach (InputDevice device in disabledDevices)
        {
            if (device.added) InputSystem.EnableDevice(device);
        }
        disabledDevices.Clear();
    }

    private void OnGUI()
    {
        EnsureStyles();
        float s = Screen.height / 1080f;

        if (prompting)
        {
            DrawPrompt(s);
            return;
        }

#if !TESTER_BUILD
        // 開発中は Unity のメニュー（Tools ＞ 自動プレイ）から書き出せるので、タイトル画面には出さない
        return;
#else
        if (SceneManager.GetActiveScene().name != TitleScene) return;

        // タイトル画面の左下に、名前と書き出しボタンを小さく出す
        // 名前が長いとボタンと重なるので、名前・ボタン・説明を縦に並べる
        var area = new Rect(16 * s, Screen.height - 196 * s, 620 * s, 180 * s);
        GUI.Box(area, GUIContent.none, boxStyle);
        GUILayout.BeginArea(new Rect(area.x + 12 * s, area.y + 8 * s, area.width - 24 * s, area.height - 16 * s));
        GUILayout.Label($"テスター: {TesterProfile.Name}", labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("名前を変える", buttonStyle, GUILayout.Width(200 * s), GUILayout.Height(48 * s))) OpenPrompt();
        if (GUILayout.Button("記録を書き出す", buttonStyle, GUILayout.Width(240 * s), GUILayout.Height(48 * s))) Export();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Label(Time.unscaledTime < messageUntil ? message : "遊んだ記録は自動で保存されます。終わったら「記録を書き出す」で開発チームに送ってください。", smallStyle);
        GUILayout.EndArea();
#endif
    }

    private void DrawPrompt(float s)
    {
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, boxStyle);

        float w = 720 * s, h = 300 * s;
        GUILayout.BeginArea(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h), boxStyle);
        GUILayout.Space(16 * s);
        GUILayout.Label("テストプレイへのご協力ありがとうございます", labelStyle);
        GUILayout.Label("あなたの名前（ニックネーム可）を入力してください。\n遊んだ記録が誰のものか分かるように、記録ファイルに付けます。", smallStyle);
        GUILayout.Space(10 * s);
        GUI.SetNextControlName("TesterName");
        nameInput = GUILayout.TextField(nameInput ?? "", 20, fieldStyle);
        GUI.FocusControl("TesterName");
        GUILayout.Space(14 * s);

        bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        GUI.enabled = !string.IsNullOrWhiteSpace(nameInput);
        if ((GUILayout.Button("決定", buttonStyle, GUILayout.Height(56 * s)) || enter) && GUI.enabled)
        {
            TesterProfile.SetName(nameInput);
            prompting = false;
            ReleaseInput();
        }
        GUI.enabled = true;
        GUILayout.EndArea();
    }

    private void Export()
    {
        try
        {
            string path = RecordExporter.ExportToDesktop();
            message = $"書き出しました: {path}";
            Application.OpenURL(new Uri(Path.GetDirectoryName(path)).AbsoluteUri);
        }
        catch (Exception e)
        {
            message = $"書き出せませんでした: {e.Message}";
            Debug.LogWarning($"[TesterPanel] 記録を書き出せませんでした。{e}");
        }
        messageUntil = Time.unscaledTime + 10f;
    }

    private void EnsureStyles()
    {
        if (boxStyle != null && styledHeight == Screen.height) return;
        styledHeight = Screen.height;
        float s = Screen.height / 1080f;

        if (background == null)
        {
            background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.75f));
            background.Apply();
        }

        boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 10, 10) };
        boxStyle.normal.background = background;
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * s), fontStyle = FontStyle.Bold, wordWrap = true };
        labelStyle.normal.textColor = Color.white;
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(19 * s), wordWrap = true };
        smallStyle.normal.textColor = new Color(0.9f, 0.9f, 0.88f);
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(21 * s) };
        fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(28 * s), fixedHeight = 52 * s };
    }
}

/// <summary>
/// テスターの記録（人のプレイの記録と、各回のログ）を1つの zip にまとめる。
/// zip の中身:
///   info.txt                … 名前・バージョン・書き出した日時
///   Routes/route_〜.csv     … 人のプレイの記録（自動プレイのお手本・学習に使う）
///   Sessions/日時/〜        … 各回のログ（バグの調査用。PC の CPU・GPU などの情報を含む）
/// </summary>
public static class RecordExporter
{
    private static string LogsRoot => Path.Combine(Application.persistentDataPath, "Logs");

    public static string ExportToDesktop()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop)) desktop = Application.persistentDataPath;

        string zipPath = Path.Combine(desktop, $"{Application.productName}_記録_{TesterProfile.FileSafeName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        int routes = 0, sessions = 0;

        using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            string routesDir = Path.Combine(LogsRoot, "Routes");
            if (Directory.Exists(routesDir))
            {
                foreach (string file in Directory.GetFiles(routesDir, "route_*.csv"))
                {
                    AddFile(zip, file, "Routes/" + Path.GetFileName(file));
                    routes++;
                }
            }

            if (Directory.Exists(LogsRoot))
            {
                foreach (string dir in Directory.GetDirectories(LogsRoot))
                {
                    string name = Path.GetFileName(dir);
                    if (!char.IsDigit(name[0])) continue;
                    foreach (string file in Directory.GetFiles(dir))
                    {
                        AddFile(zip, file, $"Sessions/{name}/{Path.GetFileName(file)}");
                    }
                    sessions++;
                }
            }

            ZipArchiveEntry info = zip.CreateEntry("info.txt");
            using (var writer = new StreamWriter(info.Open(), new UTF8Encoding(false)))
            {
                writer.WriteLine($"player={TesterProfile.FileSafeName}");
                writer.WriteLine($"version={Application.version}");
                writer.WriteLine($"exported={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"routes={routes}");
                writer.WriteLine($"sessions={sessions}");
            }
        }

        GameLogger.Event("RecordsExported", $"routes={routes} sessions={sessions} file={Path.GetFileName(zipPath)}");
        return zipPath;
    }

    /// <summary>ゲームが書き込み中のログも含められるよう、共有モードで読んで入れる。</summary>
    private static void AddFile(ZipArchive zip, string path, string entryName)
    {
        ZipArchiveEntry entry = zip.CreateEntry(entryName);
        using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (Stream target = entry.Open())
        {
            source.CopyTo(target);
        }
    }
}
#endif
