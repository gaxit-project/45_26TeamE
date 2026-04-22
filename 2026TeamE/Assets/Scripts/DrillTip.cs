using UnityEngine;

public class DrillTip : MonoBehaviour
{
    [SerializeField] float drillInterval = 0.2f;
    [SerializeField] float drillRadius = 1.5f;
    [SerializeField] Transform miningZone;

    [Header("エフェクト")]
    [SerializeField] private ParticleSystem dirtEffect; // ドリル先端から出続ける土のエフェクト
    [SerializeField] private float effectKeepTime = 0.2f; // ★追加：地形から離れた後にエフェクトを残す時間（秒）

    private float lastDrillTime;
    private PlayerController player;

    // ★追加：最後に地形に触れた時間を記録する変数
    private float lastDirtTouchTime = -1f;

    void Start()
    {
        player = GetComponentInParent<PlayerController>();
    }

    // ※Update()にあった isTouchingDirt = false; は不要になったので削除しました

    private void OnTriggerStay(Collider other)
    {
        if (player == null || !player.IsDrilling) return;

        // ★変更：ボクセル地形か、土ブロックに触れていたら「最後に触れた時間」を現在時刻で上書き
        if (other.CompareTag("VoxelTerrain") || other.CompareTag("Block_dirt"))
        {
            lastDirtTouchTime = Time.time;
        }

        if (Time.time < lastDrillTime + drillInterval) return;

        if (other.CompareTag("VoxelTerrain"))
        {
            VoxelTerrain terrain = other.GetComponentInParent<VoxelTerrain>();
            if (terrain != null)
            {
                Vector3 localPos = terrain.transform.InverseTransformPoint(transform.position);
                float s = terrain.BlockSize;
                int x = Mathf.FloorToInt(localPos.x / s);
                int y = Mathf.FloorToInt(localPos.y / s);
                int z = Mathf.FloorToInt(localPos.z / s);

                BoxCollider box = miningZone.GetComponent<BoxCollider>();
                Vector3 minL = terrain.transform.InverseTransformPoint(box.bounds.min) / s;
                Vector3 maxL = terrain.transform.InverseTransformPoint(box.bounds.max) / s;

                terrain.ExecuteDig(x, y, z, drillRadius / s, minL, maxL);
                lastDrillTime = Time.time;
            }
        }
    }

    void LateUpdate()
    {
        // ★追加：現在時刻が「最後に触れた時間 + 0.2秒」以内かどうかを判定
        bool isRecentlyTouching = (Time.time <= lastDirtTouchTime + effectKeepTime);

        // ドリルが回転していて、かつ「現在または最近(0.2秒以内)地形に触れていた」なら再生
        if (player != null && player.IsDrilling && isRecentlyTouching)
        {
            if (dirtEffect != null && !dirtEffect.isPlaying)
            {
                dirtEffect.Play();
            }
        }
        else
        {
            // ドリルボタンを離したか、0.2秒以上空振りしている時はエフェクトを停止
            if (dirtEffect != null && dirtEffect.isPlaying)
            {
                dirtEffect.Stop();
            }
        }
    }

    // エディタのSceneビューにデバッグ用の図形を表示する
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, drillRadius);

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
}