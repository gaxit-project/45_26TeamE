#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>人のプレイを 0.25 秒ごとに記録した1行分。</summary>
public struct HumanSample
{
    public float Time;
    /// <summary>x がワールドZ（画面の左右）、y がワールドY（上下）。</summary>
    public Vector2 Position;
    /// <summary>スティックの入力。</summary>
    public Vector2 Move;
    public bool Drill;
    public bool Jump;
    /// <summary>そのプレイでソナーを使った累計回数。</summary>
    public int SonarCount;
    /// <summary>宝石レーダーのギザギザが出ていたか。出ていればその向き。</summary>
    public bool RadarSignal;
    public Vector2 RadarDirection;
    /// <summary>入力まで記録している行か（2.8.0 より前のファイルは位置しか無い）。</summary>
    public bool HasInput;
}

/// <summary>
/// 人がプレイしたときの記録。自動プレイが「お手本」としてなぞったり（Points）、
/// 人の流れや操作の癖を学んだり（Samples → HumanModel）するのに使う。
///
/// Logs/Routes/route_日時_名前_seed〇〇.csv に1プレイ1ファイルで保存する。
/// 他のメンバーのファイルを同じフォルダに入れれば、その人のプレイもお手本になる。
///
/// ファイルの形式（2.8.0 から入力の列、2.9.0 から player を追加。古いファイルもそのまま読める）:
///   # seed=12345 version=2.9.0 player=nakai duration=118.5 doors=1 firstDoor=42.0
///   t,y,z,mx,my,drill,jump,sonar,radar,rx,ry
///   0.00,-11.90,0.00,0.00,-1.00,1,0,0,1,0.71,-0.71
///   ...
/// </summary>
public sealed class HumanRoute
{
    /// <summary>なぞるときの道しるべの間隔。これより近い記録は間引く。</summary>
    private const float WaypointSpacing = 0.5f;

    public string Name { get; private set; }
    public int Seed { get; private set; }

    /// <summary>記録したときのゲームのバージョン（Application.version）。書かれていなければ空。</summary>
    public string Version { get; private set; } = "";

    /// <summary>遊んだ人の名前（TesterProfile）。2.9.0 より前の記録では空。</summary>
    public string Player { get; private set; } = "";
    public float Duration { get; private set; }

    /// <summary>このプレイで入った扉の数と、最初に入るまでの秒数（入っていなければ負）。</summary>
    public int Doors { get; private set; }
    public float FirstDoorTime { get; private set; } = -1f;

    /// <summary>なぞるための道しるべ。x がワールドZ（画面の左右）、y がワールドY（上下）。</summary>
    public List<Vector2> Points { get; } = new List<Vector2>();

    /// <summary>記録したすべての行。</summary>
    public List<HumanSample> Samples { get; } = new List<HumanSample>();

    public static string Folder => Path.Combine(Application.persistentDataPath, "Logs", "Routes");

    /// <summary>Routes フォルダの全ルートを読み込む。読めないファイルは飛ばす。</summary>
    public static List<HumanRoute> LoadAll()
    {
        var routes = new List<HumanRoute>();
        if (!Directory.Exists(Folder)) return routes;

        foreach (string path in Directory.GetFiles(Folder, "route_*.csv"))
        {
            try
            {
                HumanRoute route = Parse(Path.GetFileName(path), File.ReadAllLines(path));
                if (route != null) routes.Add(route);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HumanRoute] {Path.GetFileName(path)} を読めませんでした。{e.Message}");
            }
        }
        return routes;
    }

    private static HumanRoute Parse(string name, string[] lines)
    {
        var c = CultureInfo.InvariantCulture;
        var route = new HumanRoute { Name = name };
        bool hasSeed = false;

        foreach (string line in lines)
        {
            if (line.StartsWith("#"))
            {
                foreach (string kv in line.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = kv.IndexOf('=');
                    if (eq < 0) continue;
                    string key = kv.Substring(0, eq), value = kv.Substring(eq + 1);
                    switch (key)
                    {
                        case "seed": hasSeed = int.TryParse(value, NumberStyles.Integer, c, out int seed); route.Seed = seed; break;
                        case "version": route.Version = value; break;
                        case "player": route.Player = value; break;
                        case "duration": float.TryParse(value, NumberStyles.Float, c, out float d); route.Duration = d; break;
                        case "doors": int.TryParse(value, NumberStyles.Integer, c, out int doors); route.Doors = doors; break;
                        case "firstDoor": route.FirstDoorTime = float.TryParse(value, NumberStyles.Float, c, out float f) ? f : -1f; break;
                    }
                }
                continue;
            }

            string[] cols = line.Split(',');
            if (cols.Length < 3 || !float.TryParse(cols[0], NumberStyles.Float, c, out float t)) continue;

            var sample = new HumanSample
            {
                Time = t,
                Position = new Vector2(ParseFloat(cols[2]), ParseFloat(cols[1])),
                HasInput = cols.Length >= 11,
            };
            if (sample.HasInput)
            {
                sample.Move = new Vector2(ParseFloat(cols[3]), ParseFloat(cols[4]));
                sample.Drill = cols[5] == "1";
                sample.Jump = cols[6] == "1";
                sample.SonarCount = (int)ParseFloat(cols[7]);
                sample.RadarSignal = cols[8] == "1";
                sample.RadarDirection = new Vector2(ParseFloat(cols[9]), ParseFloat(cols[10]));
            }
            route.Samples.Add(sample);

            if (route.Points.Count == 0 || (route.Points[route.Points.Count - 1] - sample.Position).sqrMagnitude >= WaypointSpacing * WaypointSpacing)
            {
                route.Points.Add(sample.Position);
            }
        }

        return hasSeed && route.Points.Count >= 2 ? route : null;
    }

    /// <summary>"2.9.0" のようなバージョン文字列を比べられる形にする。読めなければ false。</summary>
    public static bool TryParseVersion(string text, out System.Version version)
    {
        version = null;
        return !string.IsNullOrEmpty(text) && System.Version.TryParse(text.Trim(), out version);
    }

    private static float ParseFloat(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

    /// <summary>記録をファイルに書く。書いたファイル名を返す。</summary>
    public static string Save(int seed, IReadOnlyList<HumanSample> samples, float duration, int doors, float firstDoorTime)
    {
        Directory.CreateDirectory(Folder);
        string player = TesterProfile.FileSafeName;
        string fileName = $"route_{DateTime.Now:yyyy-MM-dd_HHmmss}_{player}_seed{seed}.csv";

        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder(samples.Count * 64);
        sb.Append("# seed=").Append(seed.ToString(c))
          .Append(" version=").Append(Application.version)
          .Append(" player=").Append(player)
          .Append(" duration=").Append(duration.ToString("F1", c))
          .Append(" doors=").Append(doors.ToString(c))
          .Append(" firstDoor=").Append(firstDoorTime.ToString("F1", c)).Append('\n');
        sb.Append("t,y,z,mx,my,drill,jump,sonar,radar,rx,ry\n");
        foreach (HumanSample s in samples)
        {
            sb.Append(s.Time.ToString("F2", c)).Append(',')
              .Append(s.Position.y.ToString("F2", c)).Append(',')
              .Append(s.Position.x.ToString("F2", c)).Append(',')
              .Append(s.Move.x.ToString("F2", c)).Append(',')
              .Append(s.Move.y.ToString("F2", c)).Append(',')
              .Append(s.Drill ? '1' : '0').Append(',')
              .Append(s.Jump ? '1' : '0').Append(',')
              .Append(s.SonarCount.ToString(c)).Append(',')
              .Append(s.RadarSignal ? '1' : '0').Append(',')
              .Append(s.RadarDirection.x.ToString("F2", c)).Append(',')
              .Append(s.RadarDirection.y.ToString("F2", c)).Append('\n');
        }

        File.WriteAllText(Path.Combine(Folder, fileName), sb.ToString(), new UTF8Encoding(false));
        return fileName;
    }
}

/// <summary>
/// 人がプレイしている間（自動プレイを動かしていない間）の位置と操作を記録し、1プレイ終わるごとに保存する。
/// 途中で一度でも自動プレイが動いたプレイは、人のお手本にならないので保存しない。
/// </summary>
public sealed class HumanRouteRecorder : MonoBehaviour
{
    private const float SampleInterval = 0.25f;
    private const int MinSamples = 40;

    private VoxelTerrain terrain;
    // 時間切れ・クリアで記録を締めた地形。シーンを抜けるまで残っているので、同じ地形で記録し直さないよう覚えておく
    private VoxelTerrain finishedTerrain;
    private int seed;
    private float startTime;
    private float nextSampleTime;
    private bool touchedByBot;
    private readonly List<HumanSample> samples = new List<HumanSample>();

    // 扉: 入るとショップが開くので、ショップが開いた回数で数える
    private bool wasShopOpen;
    private int doors;
    private float firstDoorTime;

    private GemRadar radar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[HumanRouteRecorder]");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<HumanRouteRecorder>();
    }

    private void Update()
    {
        VoxelTerrain current = VoxelTerrain.Instance;
        PlayerController player = PlayerController.Instance;

        // 地形が変わった・消えた（シーンを抜けた、作り直した）らそのプレイは終わり
        if (terrain != null && !ReferenceEquals(current, terrain)) Finish();

        if (current == null || !current.IsStageGenerated || current.IsGenerating || player == null) return;
        if (ReferenceEquals(current, finishedTerrain)) return;

        if (terrain == null)
        {
            terrain = current;
            seed = VoxelTerrain.LastUsedSeed;
            startTime = Time.time;
            nextSampleTime = 0f;
            touchedByBot = false;
            samples.Clear();
            wasShopOpen = false;
            doors = 0;
            firstDoorTime = -1f;
            GameLogger.Event("StageSeed", $"seed={seed}");
        }

        if (AutoPlayBot.Instance != null && AutoPlayBot.Instance.IsRunning) touchedByBot = true;

        bool shopOpen = ShoppingManager.Instance != null && ShoppingManager.Instance.IsOpen;
        if (shopOpen && !wasShopOpen)
        {
            doors++;
            if (firstDoorTime < 0f) firstDoorTime = Time.time - startTime;
        }
        wasShopOpen = shopOpen;

        // 時間切れ・クリアで1プレイ終わり
        if (player.currentState == PlayerController.PlayerState.GameOver || player.currentState == PlayerController.PlayerState.GameClear)
        {
            Finish();
            return;
        }

        if (!player.IsControlEnabled || Time.time < nextSampleTime) return;
        nextSampleTime = Time.time + SampleInterval;

        if (radar == null || !radar.isActiveAndEnabled) radar = FindAnyObjectByType<GemRadar>();

        Vector3 p = player.transform.position;
        samples.Add(new HumanSample
        {
            Time = Time.time - startTime,
            Position = new Vector2(p.z, p.y),
            Move = player.MoveInput,
            Drill = player.IsDrillHeld,
            Jump = player.IsJumpHeld,
            SonarCount = player.SonarUseCount,
            RadarSignal = radar != null && radar.HasSignal,
            RadarDirection = radar != null ? radar.SignalDirection : Vector2.zero,
            HasInput = true,
        });
    }

    private void Finish()
    {
        if (terrain != null && !touchedByBot && samples.Count >= MinSamples)
        {
            try
            {
                string fileName = HumanRoute.Save(seed, samples, Time.time - startTime, doors, firstDoorTime);
                GameLogger.Event("RouteSaved", $"route={fileName} seed={seed} points={samples.Count} doors={doors} player={TesterProfile.FileSafeName}");
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[HumanRouteRecorder] ルートを保存できませんでした。{e.Message}");
            }
        }

        finishedTerrain = terrain;
        terrain = null;
        samples.Clear();
    }

    private void OnApplicationQuit()
    {
        Finish();
    }
}
#endif
