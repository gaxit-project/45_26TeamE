using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// テスターに遊んでもらって、人のプレイの記録を集めるためのメニュー。
///
///   テスター版をビルド         … Development Build に TESTER_BUILD を付けてビルドする。
///                                 自動プレイ（F9）・右上のデバッグ表示・バックグラウンド実行は無効で、記録だけを取る
///   テスターの記録を取り込む   … テスターがタイトル画面の「記録を書き出す」で作った zip を Logs に取り込む
///   記録をまとめて書き出す     … 自分の記録を、テスターと同じ形の zip にする（他の開発メンバーに渡す用）
/// </summary>
public static class TesterRecordsMenu
{
    private const string MenuRoot = "Tools/自動プレイ/";
    private const string Title = "テスターの記録";

    private static string LogsRoot => Path.Combine(Application.persistentDataPath, "Logs");

    [MenuItem(MenuRoot + "テスター版をビルド", priority = 20)]
    private static void BuildTesterVersion()
    {
        string folder = EditorUtility.SaveFolderPanel("テスター版の出力先フォルダ", "", "");
        if (string.IsNullOrEmpty(folder)) return;

        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        bool windows = target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64;
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = Path.Combine(folder, PlayerSettings.productName + (windows ? ".exe" : "")),
            target = target,
            targetGroup = BuildPipeline.GetBuildTargetGroup(target),
            // 記録の仕組みは Development Build にだけ入るので、必ず付ける
            options = BuildOptions.Development,
            // Player Settings は変えずに、このビルドだけに付ける
            extraScriptingDefines = new[] { "TESTER_BUILD" },
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog(Title,
                $"テスター版（v{Application.version}）をビルドしました。\n\n{folder}\n\nこのフォルダをまるごと渡してください。" +
                "\n遊び終わったら、タイトル画面左下の「記録を書き出す」でできる zip を送ってもらいます。", "OK");
            EditorUtility.RevealInFinder(options.locationPathName);
        }
        else
        {
            EditorUtility.DisplayDialog(Title, $"ビルドに失敗しました（{report.summary.result}）。Console を確認してください。", "OK");
        }
    }

    [MenuItem(MenuRoot + "テスターの記録を取り込む", priority = 21)]
    private static void ImportTesterRecords()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string zipPath = EditorUtility.OpenFilePanel("テスターの記録（zip）を選ぶ", desktop, "zip");
        if (string.IsNullOrEmpty(zipPath)) return;

        int routes = 0, sessions = 0, skipped = 0;
        string player = "unknown";

        try
        {
            using (ZipArchive zip = ZipFile.OpenRead(zipPath))
            {
                ZipArchiveEntry info = zip.GetEntry("info.txt");
                if (info != null)
                {
                    using (var reader = new StreamReader(info.Open()))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.StartsWith("player=")) player = line.Substring(7).Trim();
                        }
                    }
                }

                var importedSessions = new System.Collections.Generic.HashSet<string>();
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    string[] parts = entry.FullName.Split('/');
                    string target = null;

                    if (parts.Length == 2 && parts[0] == "Routes" && parts[1].EndsWith(".csv"))
                    {
                        target = Path.Combine(LogsRoot, "Routes", parts[1]);
                    }
                    else if (parts.Length == 3 && parts[0] == "Sessions" && parts[2].Length > 0)
                    {
                        // 自分のログと区別できるよう、フォルダ名の後ろにテスターの名前を付ける
                        string sessionFolder = $"{parts[1]}_{player}";
                        target = Path.Combine(LogsRoot, sessionFolder, parts[2]);
                        importedSessions.Add(sessionFolder);
                    }

                    if (target == null) continue;
                    if (File.Exists(target))
                    {
                        skipped++;
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    entry.ExtractToFile(target);
                    if (parts[0] == "Routes") routes++;
                }
                sessions = importedSessions.Count;
            }
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog(Title, $"取り込めませんでした。\n{e.Message}", "OK");
            return;
        }

        EditorUtility.DisplayDialog(Title,
            $"{player} さんの記録を取り込みました。\n\nプレイの記録（お手本・学習用）: {routes} 件\nログ: {sessions} 回分" +
            (skipped > 0 ? $"\n取り込み済みで飛ばしたファイル: {skipped} 件" : "") +
            "\n\n次に自動プレイを始めたときから、学習とお手本に使われます。", "OK");
    }

    [MenuItem(MenuRoot + "記録をまとめて書き出す", priority = 22)]
    private static void ExportRecords()
    {
        try
        {
            string path = RecordExporter.ExportToDesktop();
            EditorUtility.RevealInFinder(path);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog(Title, $"書き出せませんでした。\n{e.Message}", "OK");
        }
    }
}
