using UnityEngine;

public class DrillTip : MonoBehaviour
{
    [SerializeField] float baseDrillInterval = 0.2f;
    [SerializeField] float baseDrillRadius = 1.5f;
    [SerializeField] Transform miningZone;

    [Header("エフェクト")]
    [SerializeField] private ParticleSystem dirtEffect;
    [SerializeField] private float effectKeepTime = 0.2f;

    [Header("可視化(LineRenderer)")]
    [SerializeField] private LineRenderer radiusRenderer;
    [SerializeField] private int segments = 40;
    [SerializeField] private float forwardOffset = 0.2f; // ← 壁から浮かせる量

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

        // 古いドリル先端のエフェクトがオンのままだと勝手に出続けてしまうため、ここで無効化します
        if (dirtEffect != null)
        {
            dirtEffect.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        DrawRadius();
    }

    private void DrawRadius()
    {
        if (radiusRenderer == null) return;

        float radius = CurrentDrillRadius;

        // ★ ワールド固定 +X に押し出す（回転の影響なし）
        Vector3 center = transform.position + Vector3.right * forwardOffset;

        radiusRenderer.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;

            float y = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            // ★ YZ平面の円
            Vector3 pos = center + new Vector3(0, y, z);

            radiusRenderer.SetPosition(i, pos);
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

                BoxCollider box = miningZone.GetComponent<BoxCollider>();
                Vector3 minL = terrain.transform.InverseTransformPoint(box.bounds.min) / s;
                Vector3 maxL = terrain.transform.InverseTransformPoint(box.bounds.max) / s;

                float currentRadius = player.IsDashing ? CurrentDrillRadius : CurrentDrillRadius * 0.8f;

                terrain.ExecuteDig(dx, dy, dz, currentRadius / s, minL, maxL);

                lastDrillTime = Time.time;
            }
        }
    }

    private void LateUpdate()
    {
        // 描画処理（LineRenderer等の更新があればここ）
        // パーティクルの再生処理はVoxelTerrain.cs（ブロック破壊時）に完全に任せるため、削除しました。
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        // ★ Gizmosもワールド+X固定にする
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
        int interval = terrain.ChunkSizeY * 10;
        return y > 0 && (y % interval == 0);
    }
}