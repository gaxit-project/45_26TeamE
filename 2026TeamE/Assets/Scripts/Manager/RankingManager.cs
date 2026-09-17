using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ScoreEntry
{
    public int score;
    public string date;
    public string runId; 
}

[System.Serializable]
public class RankingData
{
    public List<ScoreEntry> entries = new List<ScoreEntry>();
}

public static class RankingManager
{
    private const string RANKING_SAVE_KEY = "LocalRankingData";
    private const int MAX_RANKING_COUNT = 5;

    public static string CurrentRunId { get; private set; }

    public static void SaveScore(int score)
    {
        // 0点以下の場合はランキングに登録しない
        if (score <= 0)
        {
            CurrentRunId = null;
            return;
        }

        CurrentRunId = System.Guid.NewGuid().ToString();
        ScoreEntry newEntry = new ScoreEntry 
        { 
            score = score, 
            date = System.DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
            runId = CurrentRunId
        };

        RankingData data = LoadRankingData();
        data.entries.Add(newEntry);
        
        // 降順にソート（スコアが高い順）
        data.entries.Sort((a, b) => b.score.CompareTo(a.score));

        // 上位5件に絞る
        if (data.entries.Count > MAX_RANKING_COUNT)
        {
            data.entries.RemoveRange(MAX_RANKING_COUNT, data.entries.Count - MAX_RANKING_COUNT);
        }

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(RANKING_SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    public static RankingData LoadRankingData()
    {
        string json = PlayerPrefs.GetString(RANKING_SAVE_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
            return new RankingData();
        }
        return JsonUtility.FromJson<RankingData>(json);
    }
}
