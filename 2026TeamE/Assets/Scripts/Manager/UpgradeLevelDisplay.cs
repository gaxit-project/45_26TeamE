using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class UpgradeLevelDisplay : MonoBehaviour
{
    [System.Serializable]
    public class LevelDisplay
    {
        public string itemName;              
        public TextMeshProUGUI levelText;    
    }

    [SerializeField]
    private List<LevelDisplay> displays = new List<LevelDisplay>();

    private void Start()
    {
        UpdateLevels();
    }

    public void UpdateLevels()
    {
        foreach (var display in displays)
        {
            if (display.levelText != null)
            {
                display.levelText.text = UpgradeManager.GetLevel(display.itemName).ToString();
            }
        }
    }
}