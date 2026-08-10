using UnityEngine;

public class LightController : MonoBehaviour
{
    [Header("コンポーネント設定")]
    [SerializeField] private Light playerPointLight;       // プレイヤーのPoint Lightをアタッチ

    [Header("ライト基本設定")]
    [SerializeField] private float baseLightRange = 10.0f;   // インスペクターで設定する初期の照らす範囲
    [SerializeField] private float baseLightIntensity = 1.0f; // インスペクターで設定する初期の明るさ

    [Header("強化倍率設定")]
    [SerializeField] private float rangeBonusPerLevel = 1.2f;     // 1レベルごとに 1.2m 範囲が広がる
    [SerializeField] private float intensityBonusPerLevel = 0.15f; // 1レベルごとに 0.15 明るくなる

    private void Start()
    {
        // もしインスペクターでライトが未設定の場合、自身から自動取得
        if (playerPointLight == null)
        {
            playerPointLight = GetComponent<Light>();
        }

        // 初期状態のライトパラメータを適用
        UpdateLightParameters();
    }

    private void Update()
    {
        // ソナーのOverlapSphereのように、リアルタイムに最新の強化パラメータをコンポーネントへ適用し続ける
        UpdateLightParameters();
    }

    // 現在のレベルに応じたライトの範囲（Range）を計算して返すプロパティ
    public float CurrentLightRange
    {
        get
        {
            // 記憶したUpgradeManagerからLightのレベルを取得
            int lightLevel = UpgradeManager.GetLevel(UpgradeManager.LIGHT);
            float rangeBonus = (lightLevel - 1) * rangeBonusPerLevel;
            return baseLightRange + rangeBonus;
        }
    }

    // 現在のレベルに応じたライトの明るさ（Intensity）を計算して返すプロパティ
    public float CurrentLightIntensity
    {
        get
        {
            int lightLevel = UpgradeManager.GetLevel(UpgradeManager.LIGHT);
            float intensityBonus = (lightLevel - 1) * intensityBonusPerLevel;
            return baseLightIntensity + intensityBonus;
        }
    }

    // 計算された最新の値を実際のLightコンポーネントに代入する処理
    private void UpdateLightParameters()
    {
        if (playerPointLight == null) return;

        // CurrentLightRange と CurrentLightIntensity を使った代入処理
        playerPointLight.range = CurrentLightRange;
        playerPointLight.intensity = CurrentLightIntensity;
    }
}
