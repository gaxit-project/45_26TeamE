using UnityEngine;

public class DrillTip : MonoBehaviour
{
    [SerializeField] float baseDrillInterval = 0.2f;
    [SerializeField] float baseDrillRadius = 1.5f;
    [SerializeField] Transform miningZone;

    [Header("可視化")]
    [SerializeField] Transform rangeVisualizer; // ← 追加

    [Header("エフェクト")]
    [SerializeField] private ParticleSystem dirtEffect;
    [SerializeField] private float effectKeepTime = 0.2f;

    private float lastDrillTime;
    private PlayerController player;
    private float lastDirtTouchTime = -1f;

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

                BoxCollider box = miningZone.GetComponent<BoxCollider>();
                Vector3 minL = terrain.transform.InverseTransformPoint(box.bounds.min) / s;
                Vector3 maxL = terrain.transform.InverseTransformPoint(box.bounds.max) / s;

                float radius = player.IsDashing ? CurrentDrillRadius : CurrentDrillRadius * 0.8f;

                terrain.ExecuteDig(dx, dy, dz, radius / s, minL, maxL);
                lastDrillTime = Time.time;
            }
        }
    }

    void LateUpdate()
    {
        bool isRecentlyTouching = (Time.time <= lastDirtTouchTime + effectKeepTime);

        if (player != null && player.IsDrilling && isRecentlyTouching)
        {
            if (dirtEffect != null && !dirtEffect.isPlaying)
            {
                dirtEffect.Play();
            }
        }
        else
        {
            if (dirtEffect != null && dirtEffect.isPlaying)
            {
                dirtEffect.Stop();
            }
        }

        // =========================
        // ★ 掘削範囲の可視化
        // =========================
        if (rangeVisualizer != null && player != null)
        {
            float radius = player.IsDashing ? CurrentDrillRadius : CurrentDrillRadius * 0.8f;
            float size = radius * 2f;

            rangeVisualizer.position = transform.position + transform.forward * radius;
            rangeVisualizer.localScale = new Vector3(size, size, size);

            // 掘ってる時だけ表示
            rangeVisualizer.gameObject.SetActive(player.IsDrilling);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, CurrentDrillRadius);

        if (miningZone != null)
        {
            BoxCollider box = miningZone.GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.color = new Color(1, 1, 0, 0.3f);
                Gizmos.DrawCube(miningZone.TransformPoint(box.center), Vector3.Scale(miningZone.lossyScale, box.size));
            }
        }
    }

    private bool IsBedrock(VoxelTerrain terrain, int x, int y, int z)
    {
        int interval = terrain.ChunkSizeY * 10;
        return y > 0 && (y % interval == 0);
    }
}