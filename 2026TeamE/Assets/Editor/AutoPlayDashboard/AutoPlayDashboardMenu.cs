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
/// </summary>
public static class AutoPlayDashboardMenu
{
    private const string MenuRoot = "Tools/自動プレイ/";
    private const string TemplateFileName = "dashboard.html";

    [Serializable]
    private class SessionData
    {
        public string name;
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
    }

    private static string LogsRoot => Path.Combine(Application.persistentDataPath, "Logs");

    [MenuItem(MenuRoot + "ダッシュボードを開く", priority = 1)]
    private static void OpenDashboard()
    {
        string root = LogsRoot;
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

            var data = new DashboardData
            {
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                sessions = Directory.GetDirectories(root)
                    .Where(dir => File.Exists(Path.Combine(dir, "game.log")))
                    .OrderByDescending(dir => Path.GetFileName(dir), StringComparer.Ordinal)
                    .Select(ReadSession)
                    .ToArray(),
            };

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
        Directory.CreateDirectory(LogsRoot);
        EditorUtility.RevealInFinder(LogsRoot + Path.DirectorySeparatorChar);
    }

    private static SessionData ReadSession(string dir)
    {
        string[] coverageFiles = Directory.GetFiles(dir, "coverage_*.txt");
        return new SessionData
        {
            name = Path.GetFileName(dir),
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
