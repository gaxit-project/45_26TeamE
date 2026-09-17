using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゲームパッドの振動を一元管理する。
/// </summary>
/// <remarks>
/// 振動を「下地」と「パルス」の2層で扱う。
/// 下地は掘削中のような継続的な振動、パルスはブロックを砕いた瞬間のような一時的な衝撃を表す。
/// パルスが終わっても下地は残るため、パルスによって継続中の振動が途切れることがない。
/// </remarks>
public class HapticsManager : MonoBehaviour
{
    public static HapticsManager Instance { get; private set; }

    // 継続的に鳴らし続ける振動
    private float baseLow;
    private float baseHigh;

    // 下地に一時的に重ねる振動
    private float pulseLow;
    private float pulseHigh;
    private float pulseRemainingTime;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        Stop();
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Stop();
    }

    void Update()
    {
        // 常にモーター出力を更新（ポーズ/解除の切り替えに即座に対応するため）
        ApplyMotorSpeeds();

        if (pulseRemainingTime <= 0f) return;

        // ポーズやミーティングで時間が止まっていても振動が固まらないよう、実時間で減らす
        pulseRemainingTime -= Time.unscaledDeltaTime;

        if (pulseRemainingTime <= 0f)
        {
            pulseRemainingTime = 0f;
            pulseLow = 0f;
            pulseHigh = 0f;
            ApplyMotorSpeeds();
        }
    }

    /// <summary>
    /// 継続的な振動を設定する。次に <see cref="Stop"/> が呼ばれるまで鳴り続ける。
    /// </summary>
    public void PlayContinuous(float low, float high)
    {
        baseLow = low;
        baseHigh = high;
        ApplyMotorSpeeds();
    }

    /// <summary>
    /// 継続的な振動の上に、一時的な振動を重ねる。
    /// 終了後は下地の振動に戻るため、掘削中に呼んでも常時振動は途切れない。
    /// </summary>
    public void PlayPulse(float low, float high, float duration)
    {
        if (duration <= 0f) return;

        // 重なった場合は強い方・長い方を優先し、弱いパルスで上書きしない
        if (pulseRemainingTime > 0f)
        {
            low = Mathf.Max(low, pulseLow);
            high = Mathf.Max(high, pulseHigh);
            duration = Mathf.Max(duration, pulseRemainingTime);
        }

        pulseLow = low;
        pulseHigh = high;
        pulseRemainingTime = duration;

        ApplyMotorSpeeds();
    }

    /// <summary>
    /// 下地もパルスも含めて、すべての振動を止める。
    /// </summary>
    public void Stop()
    {
        baseLow = 0f;
        baseHigh = 0f;
        pulseLow = 0f;
        pulseHigh = 0f;
        pulseRemainingTime = 0f;

        ApplyMotorSpeeds();
    }

    private float lastSentLow = -1f;
    private float lastSentHigh = -1f;

    /// <summary>下地とパルスの強いほうを実際のモーター出力として反映する。</summary>
    private void ApplyMotorSpeeds()
    {
        float low = Mathf.Clamp01(Mathf.Max(baseLow, pulseLow));
        float high = Mathf.Clamp01(Mathf.Max(baseHigh, pulseHigh));

        if (Time.timeScale == 0f)
        {
            low = 0f;
            high = 0f;
        }

        if (Mathf.Approximately(low, lastSentLow) && Mathf.Approximately(high, lastSentHigh))
        {
            return;
        }

        lastSentLow = low;
        lastSentHigh = high;

        foreach (var pad in Gamepad.all)
        {
            pad?.SetMotorSpeeds(low, high);
        }
    }

    void OnApplicationQuit() => Stop();
}
