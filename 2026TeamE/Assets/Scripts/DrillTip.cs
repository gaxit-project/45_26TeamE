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

    private float lastDrillTime;
    private PlayerController player;

    private float lastDirtTouchTime = -100f;
    [SerializeField] private float contactTimeout = 0.5f;

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

        if (dirtEffect != null)
        {
            dirtEffect.gameObject.SetActive(false);
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

                Vector3 digLocal = terrain.transform.InverseTransformPoint(transform.position);
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

                terrain.ExecuteDig(dx, dy, dz, currentRadius / s, minL, maxL);
                lastDirtTouchTime = Time.time;
                lastDrillTime = Time.time;
            }
        }
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