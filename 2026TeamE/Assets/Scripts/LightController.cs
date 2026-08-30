using UnityEngine;

public class LightController : MonoBehaviour
{
    [Header("コンポーネント設定")]
    [SerializeField] private Light playerPointLight;       

    [Header("ライト基本設定")]
    [SerializeField] private float baseLightRange = 10.0f;   
    [SerializeField] private float baseLightIntensity = 1.0f; 

    [Header("強化倍率設定")]
    [SerializeField] private float rangeBonusPerLevel = 1.2f;     
    [SerializeField] private float intensityBonusPerLevel = 0.15f; 

    private void Start()
    {
        
        if (playerPointLight == null)
        {
            playerPointLight = GetComponent<Light>();
        }

        
        UpdateLightParameters();
    }

    private void Update()
    {
        
        UpdateLightParameters();
    }

    
    public float CurrentLightRange
    {
        get
        {
            
            int lightLevel = UpgradeManager.GetLevel(UpgradeManager.LIGHT);
            float rangeBonus = (lightLevel - 1) * rangeBonusPerLevel;
            return baseLightRange + rangeBonus;
        }
    }

    
    public float CurrentLightIntensity
    {
        get
        {
            int lightLevel = UpgradeManager.GetLevel(UpgradeManager.LIGHT);
            float intensityBonus = (lightLevel - 1) * intensityBonusPerLevel;
            return baseLightIntensity + intensityBonus;
        }
    }

    
    private void UpdateLightParameters()
    {
        if (playerPointLight == null) return;

        
        playerPointLight.range = CurrentLightRange;
        playerPointLight.intensity = CurrentLightIntensity;
    }
}
