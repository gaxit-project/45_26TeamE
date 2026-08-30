using UnityEngine;
using System.Collections.Generic;

public static class UpgradeManager
{
    public const string DRILL = "Drill";
    public const string SONAR = "Sonar";
    public const string ENGINE = "Engine";
    public const string RADER = "Rader";
    public const string LIGHT = "Light";
    public const string JET = "Jet";

    
    private static Dictionary<string, int> currentLevels = new Dictionary<string, int>();

    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ResetUpgrades()
    {
        
        currentLevels.Clear();

        PlayerPrefs.DeleteKey($"Upgrade_{DRILL}");
        PlayerPrefs.DeleteKey($"Upgrade_{SONAR}");
        PlayerPrefs.DeleteKey($"Upgrade_{ENGINE}");
        PlayerPrefs.DeleteKey($"Upgrade_{LIGHT}");
        PlayerPrefs.DeleteKey($"Upgrade_{RADER}");
        PlayerPrefs.DeleteKey($"Upgrade_{JET}");
        PlayerPrefs.Save();

        Debug.Log("【UpgradeManager】ゲーム起動に伴い、すべての強化レベルを初期化しました。");
    }

    
    
    
    public static int GetLevel(string itemName)
    {
        if (currentLevels.ContainsKey(itemName))
        {
            return currentLevels[itemName];
        }
        return 1; 
    }

    
    
    
    public static void IncreaseLevel(string itemName)
    {
        int nextLevel = GetLevel(itemName) + 1;
        currentLevels[itemName] = nextLevel;

        
        PlayerPrefs.SetInt($"Upgrade_{itemName}", nextLevel);
        PlayerPrefs.Save();
    }
}
