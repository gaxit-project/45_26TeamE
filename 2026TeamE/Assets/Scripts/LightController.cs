using UnityEngine;

public class LightController : MonoBehaviour
{
    public static LightController Instance { get; private set; }

    [Header("コンポーネント設定")]
    [SerializeField] private Light playerPointLight;

    [Header("ライト基本設定")]
    [SerializeField] private float baseLightRange = 10.0f;
    [SerializeField] private float baseLightIntensity = 1.0f;

    [Header("強化倍率設定")]
    [SerializeField] private float rangeBonusPerLevel = 1.2f;
    [SerializeField] private float intensityBonusPerLevel = 0.15f;

    [Header("掘削中の明滅")]
    [Tooltip("掘っている間に上乗せする明るさの倍率。1にすると明滅なし")]
    [SerializeField] private float digFlashIntensityScale = 1.25f;
    [Tooltip("掘っている間に上乗せする照射範囲の倍率。1にすると変化なし")]
    [SerializeField] private float digFlashRangeScale = 1.1f;
    [Tooltip("明滅が消えるまでの時間（秒）。掘り続けている間は上書きされ続ける")]
    [SerializeField] private float digFlashDuration = 0.12f;
    [Tooltip("明滅のちらつく速さ")]
    [SerializeField] private float digFlickerSpeed = 30f;

    // 明滅の残り時間。掘るたびに補充される
    private float flashRemainingTime;

    private void Awake()
    {
        Instance = this;
    }

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

    /// <summary>
    /// 掘削などに合わせてライトを一瞬強める。ベースの明るさ設定は変更しない。
    /// </summary>
    public void Flash()
    {
        flashRemainingTime = digFlashDuration;
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

        float flashAmount = ConsumeFlashAmount();

        // 明滅していない時（flashAmountが0）は、これまで通りベース値がそのまま使われる
        playerPointLight.range = CurrentLightRange * Mathf.Lerp(1f, digFlashRangeScale, flashAmount);
        playerPointLight.intensity = CurrentLightIntensity * Mathf.Lerp(1f, digFlashIntensityScale, flashAmount);
    }

    /// <summary>
    /// このフレームの明滅の強さ（0〜1）を求め、残り時間を進める。
    /// 火花が散っているように見せるため、減衰しながら細かくちらつかせる。
    /// </summary>
    private float ConsumeFlashAmount()
    {
        if (flashRemainingTime <= 0f || digFlashDuration <= 0f) return 0f;

        flashRemainingTime = Mathf.Max(0f, flashRemainingTime - Time.deltaTime);

        float decay = flashRemainingTime / digFlashDuration;
        float flicker = Mathf.PingPong(Time.time * digFlickerSpeed, 1f);

        return decay * Mathf.Lerp(0.6f, 1f, flicker);
    }
}
