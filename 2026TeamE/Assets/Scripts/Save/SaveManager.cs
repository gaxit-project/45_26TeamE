using System;
using System.IO;
using UnityEngine;

public static class SaveManager
{
    private const string SettingsFileName = "settings.json";
    private const string RankingFileName = "ranking.json";

    private static SettingsData settings;
    private static RankingData ranking;

    // 設定データ。nullの時はファイルから読み込む。
    public static SettingsData Settings
    {
        get
        {
            if(settings == null) settings = LoadSettings();
            return settings;
        }
    }

    // ランキングデータ。nullの時はファイルから読み込む。
    public static RankingData Ranking
    {
        get
        {
            if(ranking == null) ranking = LoadRanking();
            return ranking;
        }
    }

    public static void SaveSettings() => Write(SettingsFileName, Settings);
    public static void SaveRanking() => Write(RankingFileName, Ranking);


    // ーーーーーーーーーーーーーーーーー読み込みーーーーーーーーーーーーーー
    
    private static SettingsData LoadSettings()
    {
        SettingsData data = Read<SettingsData>(SettingsFileName);
        if(data == null)
        {
            data = new SettingsData
            {
                frameRate = PlayerPrefs.GetInt("SavedFPS", SettingsData.DefaultFrameRate),
                bgmVolume = PlayerPrefs.GetFloat("SavedBGMSliderValue", SettingsData.DefaultVolume),
                seVolume = PlayerPrefs.GetFloat("SavedSESliderValue", SettingsData.DefaultVolume),
            };
            Write(SettingsFileName, data);
        }
        data.version = SettingsData.CurrentVersion;
        return data;
    }

    private static RankingData LoadRanking()
    {
        RankingData data = Read<RankingData>(RankingFileName);
        if(data == null)
        {
            string oldJson = PlayerPrefs.GetString("LocalRankingData", "");
            data = string.IsNullOrEmpty(oldJson) ? new RankingData() : JsonUtility.FromJson<RankingData>(oldJson);
            Write(RankingFileName, data);
        }
        return data;
    }


    // ーーーーーーーーーーーーーーーーーファイル操作ーーーーーーーーーーーーーー

    private static string GetPath(string fileName) => Path.Combine(Application.persistentDataPath, fileName);

    // ファイルからデータを読み込む。無い、壊れている場合はnullを返す。
    private static T Read<T>(string fileName) where T : class
    {
        string path = GetPath(fileName);
        if(!File.Exists(path)) return null;

        try
        {
            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }
        catch(Exception e)
        {
            Debug.LogError($"[SaveManager] {fileName}の読み込みに失敗しました。{e.Message}");
            File.Copy(path, path + ".broken", true);
            return null;
        }
    }

    // 一時ファイルに書いてから差し替える
    private static void Write<T>(String fileName, T data)
    {
        string path = GetPath(fileName);
        string tempPath = path + ".tmp";

        try
        {
            File.WriteAllText(tempPath, JsonUtility.ToJson(data));
            if(File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch(Exception e)
        {
            Debug.LogError($"[SaveManager] {fileName}の保存に失敗しました。{e.Message}");
        }
    }
}