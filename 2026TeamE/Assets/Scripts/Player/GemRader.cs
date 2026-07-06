using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class GemRadar : MonoBehaviour
{
    public enum RadarMode
    {
        Directional,    
        Omnidirectional 
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
    public float baseMaxDistance = 15f; 
    public float baseWaveAngle = 45f;   
    
    [Header("距離による波の高さ（Amplitude）")]
    public float minAmplitude = 0.3f;   
    public float maxAmplitude = 2.0f;   

    private LineRenderer lineRenderer;
    private float searchTimer = 0f;

    private float currentMaxDistance;
    private float currentMaxAngle;
    private float maxInaccuracyRange; 

    private List<Transform> detectedGems = new List<Transform>();
    private Transform nearestGem; 

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = segments + 1;
        lineRenderer.useWorldSpace = true;
        
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;

        // 爆弾レイヤー（Bomb）もレーダーの対象に含める
        int bombLayer = LayerMask.NameToLayer("Bomb");
        if (bombLayer != -1)
        {
            gemLayer.value |= (1 << bombLayer);
        }

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
                // 単純に足すのではなく、一番影響が強い（波が大きい）宝石を一つだけ選ぶ
                float maxInfluence = 0f;
                float finalWave = 0f;

                foreach (Transform gem in detectedGems)
                {
                    if (gem == null) continue;

                    Vector3 dir = gem.position - transform.position;
                    float distance = dir.magnitude;

                    if (distance > currentMaxDistance) continue;

                    float noise = Mathf.PerlinNoise(Time.time * 0.5f, gem.GetInstanceID() * 0.1f) * 2f - 1f;
                    float smoothOffset = noise * maxInaccuracyRange;
                    float targetAngle = Mathf.Atan2(dir.y, dir.z) + (smoothOffset * Mathf.Deg2Rad);

                    float t = 1f - Mathf.Clamp01(distance / currentMaxDistance);
                    float currentWaveAmplitude = Mathf.Lerp(minAmplitude, maxAmplitude, t);

                    float angleDiff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg));
                    
                    if (angleDiff < currentMaxAngle)
                    {
                        // 影響力（本来の波の高さ × 中央からの近さによるフェード）
                        float falloff = 1f - (angleDiff / currentMaxAngle);
                        float influence = currentWaveAmplitude * falloff;

                        // もしこの宝石の影響力が、他の宝石よりも強ければ、その波の形を採用する
                        if (influence > maxInfluence)
                        {
                            maxInfluence = influence;
                            finalWave = Mathf.Sin(Time.time * waveSpeed - angle * 20f) * influence;
                        }
                    }
                }

                currentRadius += finalWave;
            }
            else if (mode == RadarMode.Omnidirectional && hasOmniTarget)
            {
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
        
        System.Array.Sort(colliders, (a, b) => 
        {
            float distA = (a.transform.position - transform.position).sqrMagnitude;
            float distB = (b.transform.position - transform.position).sqrMagnitude;
            return distA.CompareTo(distB);
        });

        detectedGems.Clear();
        nearestGem = null;

        int count = Mathf.Min(3, colliders.Length);
        for (int i = 0; i < count; i++)
        {
            detectedGems.Add(colliders[i].transform);
        }

        if (detectedGems.Count > 0)
        {
            nearestGem = detectedGems[0];
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
