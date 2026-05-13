using UnityEngine;

public class DrillTip : MonoBehaviour
{
    [SerializeField] float baseDrillInterval = 0.2f; // インスペクターで設定した初期の速度間隔
    [SerializeField] float baseDrillRadius = 1.5f;   // インスペクターで設定した初期の範囲大きさ
    [SerializeField] Transform miningZone;

    [Header("エフェクト")]
    [SerializeField] private ParticleSystem dirtEffect;
    [SerializeField] private float effectKeepTime = 0.2f;

    private float lastDrillTime;
    private PlayerController player;

    private float lastDirtTouchTime = -1f;

    // ★現在のレベルに応じた掘削半径（初期値に加算）
    private float CurrentDrillRadius
    {
        get
        {
            int drillLevel = UpgradeManager.GetLevel(UpgradeManager.DRILL);
            float bonusRadius = (drillLevel - 1) * 0.2f; // 1レベルごとに 0.2m 拡大
            return baseDrillRadius + bonusRadius;
        }
    }

    // ★現在のレベルに応じたベースインターバル（初期値から減算して高速化）
    private float CurrentDrillInterval
    {
        get
        {
            int drillLevel = UpgradeManager.GetLevel(UpgradeManager.DRILL);
            // 1レベルごとに 0.02秒 ずつ間隔を短縮（Lv.1=0.2s, Lv.2=0.18s, Lv.3=0.16s...）
            float speedBonus = (drillLevel - 1) * 0.02f;

            // 計算結果がマイナス（0秒以下）にならないように下限（0.02秒）を設定
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
                    if (h > maxHardness) { maxHardness = h; }
                    if (IsBedrock(terrain, tx, ty, tz))
                    {
                        terrain.OnPlayerReachRelayPoint(ty);
                        return;
                    }
                }

                float hardness = maxHardness;

                // 斜め掘りの時の硬さ補正
                float dot = Mathf.Abs(Vector3.Dot(transform.forward, Vector3.up));
                if (dot > 0.1f && dot < 0.9f)
                {
                    hardness *= 1.8f;
                }

                // ★強化されたベースインターバルを元に、ブロックの硬さ（hardness）を計算する
                float currentInterval = CurrentDrillInterval * hardness;

                // --- ダッシュ中は硬さ（インターバル）を無視して即座に掘削する ---
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
