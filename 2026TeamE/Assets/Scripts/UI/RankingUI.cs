using UnityEngine;
using TMPro;

public class RankingUI : MonoBehaviour
{
    [System.Serializable]
    public class RankingRow
    {
        public TextMeshProUGUI rankText;
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI dateText;
    }

    [Header("ランキング行のUI (1位〜5位)")]
    public RankingRow[] rankingRows;
    
    [Header("ハイライト設定")]
    public Color normalColor = Color.white;
    public Color highlightColor = Color.yellow;

    void OnEnable()
    {
        UpdateRankingUI();
    }

    public void UpdateRankingUI()
    {
        RankingData data = RankingManager.LoadRankingData();
        string currentRunId = RankingManager.CurrentRunId;

        for (int i = 0; i < rankingRows.Length; i++)
        {
            string rankString = GetEnglishRank(i + 1);

            if (i < data.entries.Count)
            {
                ScoreEntry entry = data.entries[i];
                if (rankingRows[i].rankText != null) rankingRows[i].rankText.text = rankString;
                if (rankingRows[i].scoreText != null) rankingRows[i].scoreText.text = entry.score.ToString("N0");
                if (rankingRows[i].dateText != null) rankingRows[i].dateText.text = entry.date;

                // 今回のスコアなら色を変える
                bool isCurrent = (!string.IsNullOrEmpty(currentRunId) && entry.runId == currentRunId);
                SetRowColor(rankingRows[i], isCurrent ? highlightColor : normalColor);
            }
            else
            {
                // データがない場合は0点として表記
                if (rankingRows[i].rankText != null) rankingRows[i].rankText.text = rankString;
                if (rankingRows[i].scoreText != null) rankingRows[i].scoreText.text = "0";
                if (rankingRows[i].dateText != null) rankingRows[i].dateText.text = "---";
                SetRowColor(rankingRows[i], normalColor);
            }
        }
    }

    private string GetEnglishRank(int rank)
    {
        switch (rank)
        {
            case 1: return "1st";
            case 2: return "2nd";
            case 3: return "3rd";
            default: return rank + "th";
        }
    }

    private void SetRowColor(RankingRow row, Color color)
    {
        if (row.rankText != null) row.rankText.color = color;
        if (row.scoreText != null) row.scoreText.color = color;
        if (row.dateText != null) row.dateText.color = color;
    }
}
