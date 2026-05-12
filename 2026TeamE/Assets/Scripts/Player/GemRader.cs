using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GemRadar : MonoBehaviour
{
    public enum RadarMode
    {
        Directional,    // 宝石の方向だけ波打つ
        Omnidirectional // 全方位が波打ち＋鼓動（ダブルビート）する
    }

    [Header("レーダー設定")]
    public RadarMode mode = RadarMode.Directional; 
    public int segments = 60;           
    public float baseRadius = 2f;       
    public float waveSpeed = 15f;       

    [Header("宝石の検索設定")]
    public LayerMask gemLayer;          
    public float searchInterval = 0.5f; 

    [Header("距離による波の高さ（Amplitude）")]
    public float maxDistance = 30f;     
    public float minAmplitude = 0.3f;   
    public float maxAmplitude = 2.0f;   

    private LineRenderer lineRenderer;
    private Transform nearestGem;
    private float searchTimer = 0f;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = segments + 1;
        lineRenderer.useWorldSpace = true;
        
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
    }

    void Update()
    {
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
            Vector3 dir = nearestGem.position - transform.position;
            targetAngle = Mathf.Atan2(dir.y, dir.z);

            float distance = dir.magnitude;
            float t = 1f - Mathf.Clamp01(distance / maxDistance);
            currentWaveAmplitude = Mathf.Lerp(minAmplitude, maxAmplitude, t);
        }

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float currentRadius = baseRadius;

            if (hasTarget)
            {
                if (mode == RadarMode.Directional)
                {
                    float angleDiff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg));

                    if (angleDiff < 45f)
                    {
                        float wave = Mathf.Sin(Time.time * waveSpeed - angle * 20f) * currentWaveAmplitude;
                        float falloff = 1f - (angleDiff / 45f);
                        currentRadius += wave * falloff;
                    }
                }
                else if (mode == RadarMode.Omnidirectional)
                {
                    // ① これまでの波打ち（ウネウネ）効果
                    float wavyEdge = Mathf.Sin(Time.time * waveSpeed - angle * 20f) * (currentWaveAmplitude * 0.5f);

                    // ② 鼓動効果（2回伸び縮みして、2拍休む）
                    float cycleLength = 1.5f; // 全体の周期（0.5秒で2回動き、1秒休む）
                    float timeInCycle = Time.time % cycleLength;
                    float heartbeatPulse = 0f;

                    // 1回目の伸び縮み (0.0秒 ～ 0.2秒)
                    if (timeInCycle < 0.2f) {
                        heartbeatPulse = Mathf.Sin((timeInCycle / 0.2f) * Mathf.PI);
                    } 
                    // 2回目の伸び縮み (0.3秒 ～ 0.5秒)
                    else if (timeInCycle > 0.3f && timeInCycle < 0.5f) {
                        heartbeatPulse = Mathf.Sin(((timeInCycle - 0.3f) / 0.2f) * Mathf.PI);
                    }

                    // 鼓動の強さも距離によって変える
                    float pulseEffect = heartbeatPulse * currentWaveAmplitude;

                    // 波打ちと鼓動を両方足す
                    currentRadius += wavyEdge + pulseEffect;
                }
            }

            float z = Mathf.Cos(angle) * currentRadius;
            float y = Mathf.Sin(angle) * currentRadius;

            Vector3 worldPos = transform.position + new Vector3(5, y + 2, z);
            lineRenderer.SetPosition(i, worldPos);
        }
    }

    void FindNearestGem()
    {
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
