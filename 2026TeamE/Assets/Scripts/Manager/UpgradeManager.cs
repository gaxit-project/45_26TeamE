using UnityEngine;
using System.Collections.Generic;

public static class UpgradeManager
{
    public const string DRILL = "Drill";
    public const string SONAR = "Sonar";
    public const string ENGINE = "Engine";

    // メモリ上だけでレベルを保持する辞書（ゲームを閉じると消える）
    private static Dictionary<string, int> currentLevels = new Dictionary<string, int>();

    // ゲーム起動時に自動で実行される初期化処理
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // ゲーム起動時にメモリをクリアし、ディスクのセーブデータも削除する
        currentLevels.Clear();

        PlayerPrefs.DeleteKey($"Upgrade_{DRILL}");
        PlayerPrefs.DeleteKey($"Upgrade_{SONAR}");
        PlayerPrefs.DeleteKey($"Upgrade_{ENGINE}");
        PlayerPrefs.Save();

        Debug.Log("【UpgradeManager】ゲーム起動に伴い、すべての強化レベルを初期化しました。");
    }

    /// <summary>
    /// 指定されたアイテムの現在のレベルを取得します
    /// </summary>
    public static int GetLevel(string itemName)
    {
        if (currentLevels.ContainsKey(itemName))
        {
            return currentLevels[itemName];
        }
        return 0; // 初期値
    }

    /// <summary>
    /// 指定されたアイテムのレベルを1上げます
    /// </summary>
    public static void IncreaseLevel(string itemName)
    {
        int nextLevel = GetLevel(itemName) + 1;
        currentLevels[itemName] = nextLevel;

        // 念のためPlayerPrefsにも一時保存（同一セッション内のシーン遷移対策）
        PlayerPrefs.SetInt($"Upgrade_{itemName}", nextLevel);
        PlayerPrefs.Save();
    }
}
