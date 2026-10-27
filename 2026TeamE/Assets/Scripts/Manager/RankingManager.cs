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
    private const int MAX_RANKING_COUNT = 5;

    public static string CurrentRunId { get; private set; }

    public static void SaveScore(int score)
    {
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

        data.entries.Sort((a, b) => b.score.CompareTo(a.score));

        if (data.entries.Count > MAX_RANKING_COUNT)
        {
            data.entries.RemoveRange(MAX_RANKING_COUNT, data.entries.Count - MAX_RANKING_COUNT);
        }

        SaveManager.SaveRanking();
    }

    public static RankingData LoadRankingData() => SaveManager.Ranking;
}
