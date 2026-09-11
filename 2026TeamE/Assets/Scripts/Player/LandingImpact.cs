using UnityEngine;

/// <summary>
/// 落下して着地した瞬間に、落下速度に応じた衝撃（画面揺れ・振動・SE）を出す。
/// </summary>
/// <remarks>
/// プレイヤーに接地判定が無いため、「衝突した瞬間」と「その直前の落下速度」を組み合わせて着地を検出する。
/// 着地の瞬間には速度が失われてしまうので、FixedUpdateで1フレーム前の落下速度を控えておき、
/// OnCollisionEnterではその値を使って衝撃の強さを決めている。
/// </remarks>
[RequireComponent(typeof(Rigidbody))]
public class LandingImpact : MonoBehaviour
{
    [Header("着地と見なす落下速度")]
    [Tooltip("この速度未満で着地しても演出を出さない（軽い段差で反応させないため）")]
    [SerializeField] private float minFallSpeed = 4f;
    [Tooltip("この速度で落ちてきた時に演出が最大になる")]
    [SerializeField] private float maxFallSpeed = 14f;

    [Header("画面揺れ")]
    [Tooltip("最大時の揺れの強さ。0にすると揺れなし")]
    [Range(0f, 1f)]
    [SerializeField] private float shakeStrength = 0.5f;
    [SerializeField] private float shakeDuration = 0.2f;

    [Header("振動")]
    [Tooltip("最大時の振動の強さ。0にすると振動なし")]
    [Range(0f, 1f)]
    [SerializeField] private float rumbleStrength = 0.7f;
    [SerializeField] private float rumbleDuration = 0.12f;

    [Header("着地SE（空なら鳴らさない）")]
    [SerializeField] private string landingSeName = "";

    [Header("連続で鳴らさないための最小間隔（秒）")]
    [SerializeField] private float cooldown = 0.2f;

    private Rigidbody body;

    // 1フレーム前の落下速度（下向きを正とする）
    private float previousFallSpeed;
    private float lastImpactTime = -999f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        // 着地の瞬間には速度が消えてしまうため、毎フレーム落下速度を控えておく
        float downwardSpeed = -body.linearVelocity.y;
        previousFallSpeed = downwardSpeed > 0f ? downwardSpeed : 0f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Time.time < lastImpactTime + cooldown) return;
        if (previousFallSpeed < minFallSpeed) return;

        lastImpactTime = Time.time;
        PlayImpact(Mathf.InverseLerp(minFallSpeed, maxFallSpeed, previousFallSpeed));
    }

    /// <summary>
    /// 衝撃の演出を出す。
    /// </summary>
    /// <param name="impact">衝撃の大きさ（0〜1）</param>
    private void PlayImpact(float impact)
    {
        if (shakeStrength > 0f && CameraController.Instance != null)
        {
            // 弱い着地でも最低限は揺らしたいので、0からではなく半分の強さから始める
            float strength = shakeStrength * Mathf.Lerp(0.5f, 1f, impact);
            CameraController.Instance.AddShake(strength, shakeDuration);
        }

        if (rumbleStrength > 0f && HapticsManager.Instance != null)
        {
            float strength = rumbleStrength * Mathf.Lerp(0.5f, 1f, impact);
            HapticsManager.Instance.PlayPulse(strength, strength * 0.6f, rumbleDuration);
        }

        if (!string.IsNullOrEmpty(landingSeName))
        {
            SoundManager.Instance?.PlaySE(landingSeName);
        }
    }
}
