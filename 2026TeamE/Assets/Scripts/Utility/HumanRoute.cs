#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 人がプレイしたときの道のり。自動プレイが「お手本」としてなぞるために使う。
///
/// Logs/Routes/route_日時_seed〇〇.csv に1プレイ1ファイルで保存する。
/// 他のメンバーのファイルを同じフォルダに入れれば、その人のプレイもお手本になる。
///
/// ファイルの形式:
///   # seed=12345 version=2.8.0 duration=118.5
///   t,y,z
///   0.0,-11.9,0.0
///   ...
/// </summary>
public sealed class HumanRoute
{
    public string Name { get; private set; }
    public int Seed { get; private set; }

    /// <summary>道しるべ。x がワールドZ（画面の左右）、y がワールドY（上下）。</summary>
    public List<Vector2> Points { get; } = new List<Vector2>();

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
        var route = new HumanRoute { Name = name };
        bool hasSeed = false;

        foreach (string line in lines)
        {
            if (line.StartsWith("#"))
            {
                foreach (string kv in line.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (kv.StartsWith("seed=") && int.TryParse(kv.Substring(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
                    {
                        route.Seed = seed;
                        hasSeed = true;
                    }
                }
                continue;
            }

            string[] cols = line.Split(',');
            if (cols.Length < 3) continue;
            if (float.TryParse(cols[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(cols[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                route.Points.Add(new Vector2(z, y));
            }
        }

        return hasSeed && route.Points.Count >= 2 ? route : null;
    }

    /// <summary>道のりをファイルに書く。書いたファイル名を返す。</summary>
    public static string Save(int seed, IReadOnlyList<Vector3> samples, float duration)
    {
        Directory.CreateDirectory(Folder);
        string fileName = $"route_{DateTime.Now:yyyy-MM-dd_HHmmss}_seed{seed}.csv";

        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder(samples.Count * 24);
        sb.Append("# seed=").Append(seed.ToString(c))
          .Append(" version=").Append(Application.version)
          .Append(" duration=").Append(duration.ToString("F1", c)).Append('\n');
        sb.Append("t,y,z\n");
        foreach (Vector3 s in samples)
        {
            // s.x に経過秒、s.y に高さ、s.z に左右を入れている
            sb.Append(s.x.ToString("F1", c)).Append(',')
              .Append(s.y.ToString("F2", c)).Append(',')
              .Append(s.z.ToString("F2", c)).Append('\n');
        }

        File.WriteAllText(Path.Combine(Folder, fileName), sb.ToString(), new UTF8Encoding(false));
        return fileName;
    }
}

/// <summary>
/// 人がプレイしている間（自動プレイを動かしていない間）の道のりを記録し、1プレイ終わるごとに保存する。
/// 途中で一度でも自動プレイが動いたプレイは、人のお手本にならないので保存しない。
/// </summary>
public sealed class HumanRouteRecorder : MonoBehaviour
{
    private const float SampleInterval = 0.5f;
    private const float MinMove = 0.5f;
    private const int MinPoints = 20;

    private VoxelTerrain terrain;
    // 時間切れ・クリアで記録を締めた地形。シーンを抜けるまで残っているので、同じ地形で記録し直さないよう覚えておく
    private VoxelTerrain finishedTerrain;
    private int seed;
    private float startTime;
    private float nextSampleTime;
    private bool touchedByBot;
    private readonly List<Vector3> samples = new List<Vector3>();

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
            GameLogger.Event("StageSeed", $"seed={seed}");
        }

        if (AutoPlayBot.Instance != null && AutoPlayBot.Instance.IsRunning) touchedByBot = true;

        // 時間切れ・クリアで1プレイ終わり
        if (player.currentState == PlayerController.PlayerState.GameOver || player.currentState == PlayerController.PlayerState.GameClear)
        {
            Finish();
            return;
        }

        if (!player.IsControlEnabled || Time.time < nextSampleTime) return;
        nextSampleTime = Time.time + SampleInterval;

        Vector3 p = player.transform.position;
        if (samples.Count > 0)
        {
            Vector3 last = samples[samples.Count - 1];
            if (new Vector2(p.z - last.z, p.y - last.y).sqrMagnitude < MinMove * MinMove) return;
        }
        samples.Add(new Vector3(Time.time - startTime, p.y, p.z));
    }

    private void Finish()
    {
        if (terrain != null && !touchedByBot && samples.Count >= MinPoints)
        {
            try
            {
                string fileName = HumanRoute.Save(seed, samples, Time.time - startTime);
                GameLogger.Event("RouteSaved", $"route={fileName} seed={seed} points={samples.Count}");
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
