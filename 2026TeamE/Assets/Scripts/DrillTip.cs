using UnityEngine;

public class DrillTip : MonoBehaviour
{
    [SerializeField] float baseDrillInterval = 0.2f;
    [SerializeField] float baseDrillRadius = 1.5f;
    [SerializeField] Transform miningZone;

    [Header("エフェクト")]
    [SerializeField] private ParticleSystem dirtEffect;
    [SerializeField] private float effectKeepTime = 0.2f;

    [SerializeField] private float forwardOffset = 0.2f;

    [Header("サウンド設定")]
    [SerializeField] private string drillSECueName = "ドリル";
    [SerializeField] private bool requireTerrainContact = true;

    [Header("掘削時の画面揺れ")]
    [Tooltip("揺れの強さ。0にすると揺れなし。硬いブロックほど自動的に強くなる")]
    [SerializeField] private float digShakeStrength = 0.35f;
    [Tooltip("揺れが収まるまでの時間（秒）")]
    [SerializeField] private float digShakeDuration = 0.12f;

    [Header("掘削時の振動")]
    [Tooltip("ブロックを砕いた瞬間に重ねる振動の強さ。0にすると振動なし")]
    [Range(0f, 1f)]
    [SerializeField] private float digRumbleStrength = 0.6f;
    [Tooltip("その振動が続く時間（秒）")]
    [SerializeField] private float digRumbleDuration = 0.08f;

    private float lastDrillTime;
    private PlayerController player;

    private float lastDirtTouchTime = -100f;
    [SerializeField] private float contactTimeout = 0.5f;

    private SphereCollider myCollider;
    private Vector3 initialLocalCenter;

    public bool IsContactingDiggableSurface => (Time.time - lastDirtTouchTime) <= contactTimeout;

    private float CurrentDrillRadius
    {
        get
        {
            int drillLevel = UpgradeManager.GetLevel(UpgradeManager.DRILL);
            float bonusRadius = (drillLevel - 1) * 0.2f;
            return baseDrillRadius + bonusRadius;
        }
    }

    private float CurrentDrillInterval
    {
        get
        {
            int drillLevel = UpgradeManager.GetLevel(UpgradeManager.DRILL);
            float speedBonus = (drillLevel - 1) * 0.02f;
            return Mathf.Max(0.02f, baseDrillInterval - speedBonus);
        }
    }

    void Start()
    {
        player = GetComponentInParent<PlayerController>();

        myCollider = GetComponent<SphereCollider>();
        if (myCollider != null)
        {
            initialLocalCenter = myCollider.center;
        }

        if (dirtEffect != null)
        {
            dirtEffect.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        
        if (myCollider != null)
        {
            Vector3 worldCenter = transform.TransformPoint(initialLocalCenter);
            worldCenter.x = 0f;
            myCollider.center = transform.InverseTransformPoint(worldCenter);
        }
    }

    private void Update()
    {
        HandleDrillSound();
    }

    private void HandleDrillSound()
    {
        if (SoundManager.Instance == null || player == null || string.IsNullOrEmpty(drillSECueName)) return;

        bool isDrilling = player.IsDrilling && player.HasBattery;
        bool shouldPlaySound = requireTerrainContact ? (isDrilling && IsContactingDiggableSurface) : isDrilling;

        if (shouldPlaySound)
        {
            SoundManager.Instance.PlayLoopSE(drillSECueName);
        }
        else
        {
            SoundManager.Instance.StopLoopSE();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (player == null || !player.IsDrilling) return;
        if (other.CompareTag("VoxelTerrain") || other.CompareTag("Block_dirt"))
        {
            lastDirtTouchTime = Time.time;
        }
        if (other.CompareTag("VoxelTerrain"))
        {
            VoxelTerrain terrain = other.GetComponentInParent<VoxelTerrain>();
            if (terrain != null)
            {
                float s = terrain.BlockSize;
                float maxHardness = 0f;

                Vector3[] checkOffsets = {
                    transform.forward * 0.7f,
                    transform.forward * 0.5f + transform.right * 0.3f,
                    transform.forward * 0.5f - transform.right * 0.3f,
                    transform.forward * 0.5f + transform.up * 0.3f
                };

                foreach (Vector3 offset in checkOffsets)
                {
                    Vector3 checkPos = transform.position + offset;
                    checkPos.x = 0f; 
                    Vector3 lp = terrain.transform.InverseTransformPoint(checkPos);
                    int tx = Mathf.FloorToInt(lp.x / s);
                    int ty = Mathf.FloorToInt(lp.y / s);
                    int tz = Mathf.FloorToInt(lp.z / s);

                    float h = terrain.GetHardnessAtPosition(tx, ty, tz);
                    if (h > maxHardness) maxHardness = h;

                    if (IsBedrock(terrain, tx, ty, tz))
                    {
                        terrain.OnPlayerReachRelayPoint(ty);
                        return;
                    }
                }

                float hardness = maxHardness;

                float dot = Mathf.Abs(Vector3.Dot(transform.forward, Vector3.up));
                if (dot > 0.1f && dot < 0.9f)
                {
                    hardness *= 1.8f;
                }

                float currentInterval = CurrentDrillInterval * hardness;

                if (!player.IsDashing && Time.time < lastDrillTime + currentInterval) return;

                Vector3 digPos = transform.position;
                digPos.x = 0f; 
                Vector3 digLocal = terrain.transform.InverseTransformPoint(digPos);
                int dx = Mathf.FloorToInt(digLocal.x / s);
                int dy = Mathf.FloorToInt(digLocal.y / s);
                int dz = Mathf.FloorToInt(digLocal.z / s);

                if (terrain.IsRelayZoneBottom(dy))
                {
                    return;
                }

                BoxCollider box = miningZone.GetComponent<BoxCollider>();
                Vector3 minL = terrain.transform.InverseTransformPoint(box.bounds.min) / s;
                Vector3 maxL = terrain.transform.InverseTransformPoint(box.bounds.max) / s;

                float currentRadius = player.IsDashing ? CurrentDrillRadius : CurrentDrillRadius * 0.8f;

                bool destroyedAnyBlock = terrain.ExecuteDig(dx, dy, dz, currentRadius / s, minL, maxL);

                // 実際にブロックを壊した時だけ画面を揺らす。
                // 掘る対象が無い場所でドリルを回し続けても揺れないようにするため。
                if (destroyedAnyBlock)
                {
                    if (CameraController.Instance != null)
                    {
                        CameraController.Instance.AddShake(CalculateDigShakeStrength(hardness), digShakeDuration);
                    }

                    // 火花が散っているように、掘った瞬間だけライトを強める
                    if (LightController.Instance != null)
                    {
                        LightController.Instance.Flash();
                    }

                    // 砕いた手応えとして、常時振動の上から短く強いパルスを重ねる
                    if (HapticsManager.Instance != null && digRumbleStrength > 0f)
                    {
                        float rumble = CalculateDigRumbleStrength(hardness);
                        HapticsManager.Instance.PlayPulse(rumble, rumble * 0.6f, digRumbleDuration);
                    }
                }

                lastDirtTouchTime = Time.time;
                lastDrillTime = Time.time;
            }
        }
    }

    /// <summary>
    /// 掘削時の画面揺れの強さを求める。
    /// 硬さの生の値（土1〜珪岩20以上）をそのまま掛けると一瞬で上限に張り付いてしまうため、
    /// 0〜1に正規化してから控えめに上乗せする。
    /// </summary>
    /// <summary>
    /// ブロックの硬さを0〜1に正規化する。
    /// 硬さの生の値（土1〜珪岩20以上）をそのまま演出の強さに掛けると
    /// 一瞬で上限に張り付いてしまうため、扱いやすい範囲に直してから使う。
    /// </summary>
    private static float NormalizeHardness(float hardness)
    {
        const float softHardness = 1f;   // 土
        const float hardHardness = 10f;  // 硬岩

        return Mathf.InverseLerp(softHardness, hardHardness, hardness);
    }

    /// <summary>掘削時の画面揺れの強さ。土でも必ず揺れ、硬いブロックでは最大2倍になる。</summary>
    private float CalculateDigShakeStrength(float hardness)
    {
        return digShakeStrength * Mathf.Lerp(1f, 2f, NormalizeHardness(hardness));
    }

    /// <summary>掘削時の振動の強さ。硬いブロックほど強く響かせる。</summary>
    private float CalculateDigRumbleStrength(float hardness)
    {
        return Mathf.Clamp01(digRumbleStrength * Mathf.Lerp(0.7f, 1f, NormalizeHardness(hardness)));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        Vector3 center = transform.position + Vector3.right * forwardOffset;

        int segments = 40;
        float radius = CurrentDrillRadius;

        Vector3 prev = center + new Vector3(0, radius, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            float y = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            Vector3 next = center + new Vector3(0, y, z);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private bool IsBedrock(VoxelTerrain terrain, int x, int y, int z)
    {
        return terrain.IsRelayZoneBottom(y);
    }
}