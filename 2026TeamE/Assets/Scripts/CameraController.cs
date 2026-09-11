using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("追跡対象")]
    public Transform player;

    [Header("設定")]
    public float smoothTime = 1.0f;
    public Vector3 offset = new Vector3(0, 0, -2f);

    [Header("画面揺れ")]
    [Tooltip("揺れの最大幅（ワールド単位）。0にすると揺れなし")]
    [SerializeField] private float maxShakeAmount = 0.25f;

    private Vector3 currentVelocity = Vector3.zero;

    // 揺れの残り時間と、その揺れ全体の長さ・強さ
    private float shakeRemainingTime;
    private float shakeTotalDuration;
    private float shakeStrength;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 画面を揺らす。掘削や爆発など、衝撃を伝えたい場面から呼ぶ。
    /// </summary>
    /// <param name="strength">揺れの強さ（0〜1想定）</param>
    /// <param name="duration">揺れが収まるまでの時間（秒）</param>
    public void AddShake(float strength, float duration)
    {
        if (strength <= 0f || duration <= 0f) return;

        strength = Mathf.Clamp01(strength);

        // まだ揺れている最中なら、強い方・長い方を採用する。
        // 弱い揺れで上書きされず、かつ掘り続けている間は揺れが途切れない。
        if (shakeRemainingTime > 0f)
        {
            strength = Mathf.Max(strength, shakeStrength);
            duration = Mathf.Max(duration, shakeRemainingTime);
        }

        shakeStrength = strength;
        shakeTotalDuration = duration;
        shakeRemainingTime = duration;
    }

    void LateUpdate()
    {
        if (player == null) return;

        Vector3 targetPosition = player.position + offset;

        Vector3 nextPosition = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            smoothTime
        );

        nextPosition.y = targetPosition.y;

        transform.position = nextPosition + CalculateShakeOffset();
    }

    /// <summary>
    /// このフレームの揺れ幅を求める。時間が経つほど揺れが収まっていく。
    /// </summary>
    private Vector3 CalculateShakeOffset()
    {
        if (shakeRemainingTime <= 0f || shakeTotalDuration <= 0f) return Vector3.zero;

        shakeRemainingTime = Mathf.Max(0f, shakeRemainingTime - Time.deltaTime);

        // 残り時間の割合をそのまま減衰に使う（1→0）
        float decay = shakeRemainingTime / shakeTotalDuration;
        float amount = maxShakeAmount * shakeStrength * decay;

        // 画面はYZ平面なので、その2軸だけを揺らす
        return new Vector3(
            0f,
            Random.Range(-amount, amount),
            Random.Range(-amount, amount)
        );
    }
}
