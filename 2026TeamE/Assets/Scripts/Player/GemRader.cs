using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GemRadar : MonoBehaviour
{
    [Header("レーダー設定")]
    public int segments = 60;           // 円の滑らかさ
    public float baseRadius = 2f;       // 基本の円の大きさ
    public float waveSpeed = 15f;       // 波が揺れるスピード

    [Header("宝石の検索設定")]
    public LayerMask gemLayer;          // 探したいレイヤー（インスペクターで設定）
    public float searchInterval = 0.5f; // 何秒ごとに一番近い宝石を探し直すか

    [Header("距離による波の高さ（Amplitude）")]
    public float maxDistance = 30f;     // これ以上遠いと波は最小になる（検索範囲でもあります）
    public float minAmplitude = 0.3f;   // 遠いときの波の高さ（最小）
    public float maxAmplitude = 2.0f;   // 近いときの波の高さ（最大）

    private LineRenderer lineRenderer;
    private Transform nearestGem;
    private float searchTimer = 0f;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = segments + 1;
        
        // キャラクターの向きに影響されないようにワールド座標を使用
        lineRenderer.useWorldSpace = true;
        
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
    }

    void Update()
    {
        // 毎フレーム探すと重くなるため、一定時間ごとに一番近い宝石を探す
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0)
        {
            searchTimer = searchInterval;
            FindNearestGem();
        }

        float targetAngle = 0f;
        bool hasTarget = nearestGem != null;
        float currentWaveAmplitude = 0f;

        if (hasTarget)
        {
            // プレイヤーから宝石への絶対的な方向を計算
            Vector3 dir = nearestGem.position - transform.position;
            targetAngle = Mathf.Atan2(dir.y, dir.z);

            // 距離を測り、0.3 ～ 2.0 の間で Amplitude を計算する
            float distance = dir.magnitude;
            
            // 距離が maxDistance より遠い場合は t=0 (minAmplitude)、距離が 0 なら t=1 (maxAmplitude) 
            float t = 1f - Mathf.Clamp01(distance / maxDistance);
            currentWaveAmplitude = Mathf.Lerp(minAmplitude, maxAmplitude, t);
        }

        // 円の描画と波の計算
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float currentRadius = baseRadius;

            if (hasTarget)
            {
                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg));

                // 宝石がある方向（±45度の範囲内）だけ波打たせる
                if (angleDiff < 45f)
                {
                    // 計算した currentWaveAmplitude を使って波の高さを決める
                    float wave = Mathf.Sin(Time.time * waveSpeed - angle * 20f) * currentWaveAmplitude;
                    float falloff = 1f - (angleDiff / 45f);
                    currentRadius += wave * falloff;
                }
            }

            // 円の座標を計算
            float z = Mathf.Cos(angle) * currentRadius;
            float y = Mathf.Sin(angle) * currentRadius;

            // プレイヤーの現在位置を足してワールド座標にする
            Vector3 worldPos = transform.position + new Vector3(5, y+2, z);
            lineRenderer.SetPosition(i, worldPos);
        }
    }

    // Physics.OverlapSphere を使って指定レイヤーの宝石を探す
    void FindNearestGem()
    {
        // プレイヤーを中心に、maxDistanceの範囲内にある指定レイヤーのコライダーを一括取得
        Collider[] colliders = Physics.OverlapSphere(transform.position, maxDistance, gemLayer);
        
        float minDistance = float.MaxValue;
        nearestGem = null;

        foreach (Collider col in colliders)
        {
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestGem = col.transform;
            }
        }
    }
}
