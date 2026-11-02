using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 自動プレイのログを見るダッシュボードを開くメニュー。
///
/// Logs フォルダの全セッションを読み込んで dashboard_data.js にまとめ、
/// dashboard.html と一緒に Logs フォルダへ置いてブラウザで開く。
/// （ブラウザはローカルのファイルを勝手に読めないため、データを JS として横に置いて読ませる）
/// 共有の保存先（LogLocation）を設定していれば、全 PC 分をまとめて読み、共有の保存先に置く。
/// </summary>
public static class AutoPlayDashboardMenu
{
    private const string MenuRoot = "Tools/自動プレイ/";
    private const string TemplateFileName = "dashboard.html";

    [Serializable]
    private class SessionData
    {
        public string name;
        /// <summary>どの PC のログか。</summary>
        public string pc;
        /// <summary>dashboard.html から見たセッションフォルダの場所（スクショの読み込みに使う）。</summary>
        public string path;
        public string gameLog;
        public string errorLog;
        public string telemetry;
        public string[] coverageNames;
        public string[] coverageTexts;
    }

    [Serializable]
    private class DashboardData
    {
        public string generatedAt;
        public SessionData[] sessions;
        public string[] routeNames;
        public string[] routeTexts;
    }

    [MenuItem(MenuRoot + "ダッシュボードを開く", priority = 1)]
    private static void OpenDashboard()
    {
        // 共有の保存先があれば全 PC 分を集めてそこに置く。無ければこの PC の Logs に置く
        string shared = LogLocation.SharedBase;
        string root = shared ?? LogLocation.DefaultRoot;
        if (!Directory.Exists(root))
        {
            EditorUtility.DisplayDialog("自動プレイ ダッシュボード",
                "ログがまだありません。\nプレイモードで遊ぶか、F9 で自動プレイを動かしてから開いてください。", "OK");
            return;
        }

        string template = FindTemplate();
        if (template == null)
        {
            EditorUtility.DisplayDialog("自動プレイ ダッシュボード", $"{TemplateFileName} が見つかりませんでした。", "OK");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("自動プレイ ダッシュボード", "ログを集めています…", 0.3f);

            var sessions = new List<SessionData>();
            var routeFileList = new List<string>();
            foreach (var pcRoot in LogLocation.AllRoots())
            {
                if (!Directory.Exists(pcRoot.Value)) continue;
                foreach (string dir in Directory.GetDirectories(pcRoot.Value))
                {
                    if (!File.Exists(Path.Combine(dir, "game.log"))) continue;
                    string relative = shared != null ? $"{pcRoot.Key}/{Path.GetFileName(dir)}" : Path.GetFileName(dir);
                    sessions.Add(ReadSession(dir, pcRoot.Key, relative));
                }

                // 人のプレイのお手本ルート（自動プレイがなぞったルートをマップに重ねるため）
                string routesDir = Path.Combine(pcRoot.Value, "Routes");
                if (Directory.Exists(routesDir)) routeFileList.AddRange(Directory.GetFiles(routesDir, "route_*.csv"));
            }

            var data = new DashboardData
            {
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                sessions = sessions.OrderByDescending(x => x.name, StringComparer.Ordinal).ToArray(),
            };
            string[] routeFiles = routeFileList.ToArray();
            data.routeNames = routeFiles.Select(Path.GetFileName).ToArray();
            data.routeTexts = routeFiles.Select(ReadShared).ToArray();

            File.WriteAllText(Path.Combine(root, "dashboard_data.js"),
                "window.AUTOPLAY_DATA = " + JsonUtility.ToJson(data) + ";\n", new UTF8Encoding(false));
            File.Copy(template, Path.Combine(root, TemplateFileName), true);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AutoPlayDashboard] ダッシュボードを作れませんでした。{e}");
            return;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Application.OpenURL(new Uri(Path.Combine(root, TemplateFileName)).AbsoluteUri);
    }

    [MenuItem(MenuRoot + "ログフォルダを開く", priority = 2)]
    private static void RevealLogs()
    {
        string root = LogLocation.Root;
        Directory.CreateDirectory(root);
        EditorUtility.RevealInFinder(root + Path.DirectorySeparatorChar);
    }

    [MenuItem(MenuRoot + "ログの保存先を設定…", priority = 3)]
    private static void SetSharedLocation()
    {
        string current = LogLocation.SharedBase;
        string oneDrive = Environment.GetEnvironmentVariable("OneDrive");
        string folder = EditorUtility.OpenFolderPanel("ログの保存先（OneDrive などの同期フォルダ）", current ?? oneDrive ?? "", "");
        if (string.IsNullOrEmpty(folder)) return;

        folder = folder.Replace('/', Path.DirectorySeparatorChar);
        LogLocation.SaveSharedBase(folder);
        string pcFolder = Path.Combine(folder, LogLocation.PcName);
        Directory.CreateDirectory(pcFolder);

        bool copy = Directory.Exists(LogLocation.DefaultRoot) && EditorUtility.DisplayDialog("ログの保存先",
            $"これからのログは次のフォルダに保存します（エディターも、この PC のビルドも同じです）。\n\n{pcFolder}\n\n" +
            "これまでこの PC に保存したログと記録も、そこへコピーしますか？", "コピーする", "コピーしない");
        int copied = copy ? CopyMissing(LogLocation.DefaultRoot, pcFolder) : 0;

        EditorUtility.DisplayDialog("ログの保存先",
            $"保存先を設定しました。\n{pcFolder}" + (copy ? $"\n\nこれまでのファイルを {copied} 件コピーしました。" : "") +
            "\n\nほかの PC も同じ同期フォルダを設定すると、PC ごとのフォルダに分かれて集まります。" +
            "\nUnity の無い PC では、ビルドの exe と同じフォルダに log_location.txt（1行目に保存先のフォルダ）を置くか、" +
            "起動オプション -logdir \"フォルダ\" で指定できます。", "OK");
    }

    [MenuItem(MenuRoot + "ログの保存先を元に戻す", priority = 4)]
    private static void ResetSharedLocation()
    {
        LogLocation.ClearSharedBase();
        EditorUtility.DisplayDialog("ログの保存先",
            $"この PC の中のいつもの場所に戻しました。\n{LogLocation.DefaultRoot}\n\n同期フォルダに保存したログはそのまま残っています。", "OK");
    }

    /// <summary>コピー先に無いファイルだけをコピーする。コピーした数を返す。</summary>
    private static int CopyMissing(string from, string to)
    {
        int count = 0;
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(to, file.Substring(from.Length).TrimStart(Path.DirectorySeparatorChar));
            if (File.Exists(target) || Path.GetFileName(file).StartsWith("dashboard")) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target);
            count++;
        }
        return count;
    }

    private static SessionData ReadSession(string dir, string pc, string relativePath)
    {
        string[] coverageFiles = Directory.GetFiles(dir, "coverage_*.txt");
        return new SessionData
        {
            name = Path.GetFileName(dir),
            pc = pc,
            path = relativePath,
            gameLog = ReadShared(Path.Combine(dir, "game.log")),
            errorLog = ReadShared(Path.Combine(dir, "errors.log")),
            telemetry = ReadShared(Path.Combine(dir, "telemetry.csv")),
            coverageNames = coverageFiles.Select(Path.GetFileName).ToArray(),
            coverageTexts = coverageFiles.Select(ReadShared).ToArray(),
        };
    }

    /// <summary>
    /// プレイ中でゲーム側が書き込み中のファイルも読めるよう、共有モードで開く。
    /// </summary>
    private static string ReadShared(string path)
    {
        if (!File.Exists(path)) return "";
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            return reader.ReadToEnd();
        }
    }

    /// <summary>このスクリプトと同じフォルダにある dashboard.html を探す（フォルダを移動しても動くように）。</summary>
    private static string FindTemplate()
    {
        IEnumerable<string> guids = AssetDatabase.FindAssets("dashboard t:TextAsset");
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(assetPath) == TemplateFileName && assetPath.Contains("AutoPlayDashboard"))
            {
                return Path.GetFullPath(assetPath);
            }
        }

        string fallback = Path.Combine(Application.dataPath, "Editor", "AutoPlayDashboard", TemplateFileName);
        return File.Exists(fallback) ? fallback : null;
    }
}
