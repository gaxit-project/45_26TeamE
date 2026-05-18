using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class GemRadar : MonoBehaviour
{
    public enum RadarMode
    {
        Directional,    // 宝石の方向だけ波打つ
        Omnidirectional // 全方位が波打ち＋鼓動（ダブルビート）する
    }

    [Header("レーダー基本設定")]
    public RadarMode mode = RadarMode.Directional; 
    public int segments = 60;           
    public float baseRadius = 2f;       
    public float waveSpeed = 15f;       

    [Header("宝石の検索設定")]
    public LayerMask gemLayer;          
    public float searchInterval = 0.5f; 

    [Header("レーダー性能の基準値（レベル2相当）")]
    [Tooltip("ここに入力した数値を基準にして、レベルに応じて自動で掛け算・割り算されます")]
    public float baseMaxDistance = 30f; 
    public float baseWaveAngle = 45f;   
    
    [Header("距離による波の高さ（Amplitude）")]
    public float minAmplitude = 0.3f;   
    public float maxAmplitude = 2.0f;   

    private LineRenderer lineRenderer;
    private float searchTimer = 0f;

    // レベル計算用の実際のパラメータ
    private float currentMaxDistance;
    private float currentMaxAngle;
    private float maxInaccuracyRange; // 方向のブレの最大範囲

    // 範囲内のすべての宝石を保持するリスト
    private List<Transform> detectedGems = new List<Transform>();
    private Transform nearestGem; // Omnidirectionalモード用

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = segments + 1;
        lineRenderer.useWorldSpace = true;
        
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;

        UpdateRadarParameters();
    }

    void Update()
    {
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0)
        {
            searchTimer = searchInterval;
            FindGemsInRange();
        }

        // Omnidirectionalモード用（一番近い宝石だけを使う旧処理）
        float omniTargetAngle = 0f;
        float omniCurrentWaveAmplitude = 0f;
        bool hasOmniTarget = nearestGem != null;

        if (hasOmniTarget && mode == RadarMode.Omnidirectional)
        {
            Vector3 dir = nearestGem.position - transform.position;
            float noise = Mathf.PerlinNoise(Time.time * 0.5f, 0f) * 2f - 1f;
            float smoothOffset = noise * maxInaccuracyRange;
            omniTargetAngle = Mathf.Atan2(dir.y, dir.z) + (smoothOffset * Mathf.Deg2Rad);

            float distance = dir.magnitude;
            float t = 1f - Mathf.Clamp01(distance / currentMaxDistance);
            omniCurrentWaveAmplitude = Mathf.Lerp(minAmplitude, maxAmplitude, t);
        }

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float currentRadius = baseRadius;

            if (mode == RadarMode.Directional)
            {
                // 複数の宝石が作る波を足し合わせる変数
                float combinedWave = 0f;

                foreach (Transform gem in detectedGems)
                {
                    if (gem == null) continue;

                    Vector3 dir = gem.position - transform.position;
                    float distance = dir.magnitude;

                    // 範囲外ならスキップ
                    if (distance > currentMaxDistance) continue;

                    // 1. この宝石専用のブレ（フワフワ）を計算
                    // gem.GetInstanceID() を使うことで、Aの宝石とBの宝石で別々の揺らぎ方をする
                    float noise = Mathf.PerlinNoise(Time.time * 0.5f, gem.GetInstanceID() * 0.1f) * 2f - 1f;
                    float smoothOffset = noise * maxInaccuracyRange;
                    float targetAngle = Mathf.Atan2(dir.y, dir.z) + (smoothOffset * Mathf.Deg2Rad);

                    // 2. 距離による波の強さを計算
                    float t = 1f - Mathf.Clamp01(distance / currentMaxDistance);
                    float currentWaveAmplitude = Mathf.Lerp(minAmplitude, maxAmplitude, t);

                    // 3. この宝石が、現在の円の点(angle)に与える影響を加算
                    float angleDiff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg));
                    if (angleDiff < currentMaxAngle)
                    {
                        float wave = Mathf.Sin(Time.time * waveSpeed - angle * 20f) * currentWaveAmplitude;
                        float falloff = 1f - (angleDiff / currentMaxAngle);
                        combinedWave += wave * falloff;
                    }
                }

                // すべての宝石の波を合計して半径に足す
                currentRadius += combinedWave;
            }
            else if (mode == RadarMode.Omnidirectional && hasOmniTarget)
            {
                // 鼓動モードはこれまで通り（一番近い宝石だけ）
                float wavyEdge = Mathf.Sin(Time.time * waveSpeed - angle * 20f) * (omniCurrentWaveAmplitude * 0.5f);

                float cycleLength = 1.5f; 
                float timeInCycle = Time.time % cycleLength;
                float heartbeatPulse = 0f;

                if (timeInCycle < 0.2f) {
                    heartbeatPulse = Mathf.Sin((timeInCycle / 0.2f) * Mathf.PI);
                } 
                else if (timeInCycle > 0.3f && timeInCycle < 0.5f) {
                    heartbeatPulse = Mathf.Sin(((timeInCycle - 0.3f) / 0.2f) * Mathf.PI);
                }

                float pulseEffect = heartbeatPulse * omniCurrentWaveAmplitude;
                currentRadius += wavyEdge + pulseEffect;
            }

            float z = Mathf.Cos(angle) * currentRadius;
            float y = Mathf.Sin(angle) * currentRadius;

            Vector3 worldPos = transform.position + new Vector3(5, y + 2, z);
            lineRenderer.SetPosition(i, worldPos);
        }
    }

    void FindGemsInRange()
    {
        UpdateRadarParameters();

        Collider[] colliders = Physics.OverlapSphere(transform.position, currentMaxDistance, gemLayer);
        
        detectedGems.Clear();
        float minDistance = float.MaxValue;
        nearestGem = null;

        foreach (Collider col in colliders)
        {
            detectedGems.Add(col.transform);

            // 鼓動モード（変更なし）のために、一番近い宝石も記憶しておく
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestGem = col.transform;
            }
        }
    }

    void UpdateRadarParameters()
    {
        int radarLevel = UpgradeManager.GetLevel(UpgradeManager.RADER);
        if (radarLevel <= 0) radarLevel = 1;

        if (radarLevel == 1)
        {
            currentMaxDistance = baseMaxDistance * 0.5f; 
            currentMaxAngle = baseWaveAngle * 2.0f;     
            maxInaccuracyRange = 45f; 
        }
        else if (radarLevel == 2)
        {
            currentMaxDistance = baseMaxDistance * 1.0f;
            currentMaxAngle = baseWaveAngle * 1.0f;
            maxInaccuracyRange = 15f; 
        }
        else
        {
            currentMaxDistance = baseMaxDistance * 1.5f;
            currentMaxAngle = baseWaveAngle * 0.5f;
            maxInaccuracyRange = 0f;  
        }
    }
}
