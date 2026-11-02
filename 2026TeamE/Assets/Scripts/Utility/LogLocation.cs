#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// ログ（GameLogger）と人のプレイの記録（HumanRoute）をどこに保存するか。
///
/// 何も設定しなければ、今までどおり %USERPROFILE%\AppData\LocalLow\GameCreateProject\CycloneDiver\Logs に保存する。
/// OneDrive や Google ドライブなどの同期フォルダを「共有の保存先」に設定すると、その中の PC 名のフォルダに保存するので、
/// 複数の PC のログが自動で1か所に集まる（取り出して送る手間がいらない）。
///
///   共有の保存先\
///     PC名A\  2026-10-29_101500\ … , Routes\ …
///     PC名B\  …
///
/// 共有の保存先は、次の順で探す。
///   1. 起動引数  -logdir "フォルダ"
///   2. ビルドの exe と同じフォルダの log_location.txt（1行目にフォルダ）
///   3. この PC の設定ファイル（Unity のメニュー「Tools ＞ 自動プレイ ＞ ログの保存先を設定…」で書く）
/// </summary>
public static class LogLocation
{
    public const string ConfigFileName = "log_location.txt";

    /// <summary>今までの保存先（この PC の中）。</summary>
    public static string DefaultRoot => Path.Combine(Application.persistentDataPath, "Logs");

    /// <summary>この PC の設定ファイル。エディターとビルドで同じ場所を見る。</summary>
    public static string ConfigPath => Path.Combine(Application.persistentDataPath, ConfigFileName);

    /// <summary>フォルダ名に使う、この PC の名前。</summary>
    public static string PcName => Sanitize(Environment.MachineName);

    /// <summary>共有の保存先（各 PC のフォルダが並ぶ親フォルダ）。設定されていなければ null。</summary>
    public static string SharedBase
    {
        get
        {
            string configured = FromArgs() ?? FromExeFolder() ?? FromConfigFile();
            return string.IsNullOrWhiteSpace(configured) ? null : configured.Trim().Trim('"');
        }
    }

    /// <summary>この PC がログを書くフォルダ。</summary>
    public static string Root
    {
        get
        {
            string shared = SharedBase;
            return shared != null ? Path.Combine(shared, PcName) : DefaultRoot;
        }
    }

    /// <summary>
    /// ログを読むときに見るフォルダの一覧（PC 名, フォルダ）。共有の保存先があれば、その中の全 PC 分。
    /// </summary>
    public static List<KeyValuePair<string, string>> AllRoots()
    {
        var roots = new List<KeyValuePair<string, string>>();
        string shared = SharedBase;
        if (shared == null || !Directory.Exists(shared))
        {
            roots.Add(new KeyValuePair<string, string>(PcName, DefaultRoot));
            return roots;
        }

        foreach (string dir in Directory.GetDirectories(shared))
        {
            roots.Add(new KeyValuePair<string, string>(Path.GetFileName(dir), dir));
        }
        return roots;
    }

    /// <summary>この PC の設定ファイルに共有の保存先を書く（Unity のメニューから呼ぶ）。</summary>
    public static void SaveSharedBase(string folder)
    {
        Directory.CreateDirectory(Application.persistentDataPath);
        File.WriteAllText(ConfigPath, folder + "\n", new UTF8Encoding(false));
    }

    /// <summary>設定ファイルを消して、今までの保存先に戻す。</summary>
    public static void ClearSharedBase()
    {
        if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
    }

    private static string FromArgs()
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-logdir");
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static string FromExeFolder()
    {
        // エディターでは Application.dataPath の親がプロジェクトのフォルダになるので見ない
        if (Application.isEditor) return null;
        string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", ConfigFileName);
        return ReadFirstLine(path);
    }

    private static string FromConfigFile() => ReadFirstLine(ConfigPath);

    private static string ReadFirstLine(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            foreach (string line in File.ReadAllLines(path))
            {
                if (!string.IsNullOrWhiteSpace(line)) return line;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    private static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "PC";
        var sb = new StringBuilder(name.Length);
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char ch in name.Trim()) sb.Append(Array.IndexOf(invalid, ch) >= 0 || char.IsWhiteSpace(ch) ? '_' : ch);
        return sb.ToString();
    }
}
#endif
