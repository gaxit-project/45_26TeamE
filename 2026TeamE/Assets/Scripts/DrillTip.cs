using UnityEngine;

public class DrillTip : MonoBehaviour
{
    [SerializeField] float baseDrillInterval = 0.2f;
    [SerializeField] float drillRadius = 1.5f;
    [SerializeField] Transform miningZone;

    [Header("エフェクト")]
    [SerializeField] private ParticleSystem dirtEffect;
    [SerializeField] private float effectKeepTime = 0.2f;

    private float lastDrillTime;
    private PlayerController player;

    private float lastDirtTouchTime = -1f;

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
                Vector3 localPos = terrain.transform.InverseTransformPoint(transform.position);
                float s = terrain.BlockSize;
                int x = Mathf.FloorToInt(localPos.x / s);
                int y = Mathf.FloorToInt(localPos.y / s);
                int z = Mathf.FloorToInt(localPos.z / s);

                if(IsBedrock(terrain, x, y, z))
                {
                    terrain.OnPlayerReachRelayPoint(y);
                }

                float hardness = terrain.GetHardnessAtDepth(y);
                float drillPower = 1.0f + (player.DrillLevel - 1) * 0.5f;
                float currentInterval = baseDrillInterval * (hardness / drillPower);
                if (Time.time < lastDrillTime + currentInterval) return;

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

    private bool IsBedrock(VoxelTerrain terrain, int x, int y, int z)
    {
        return y > 0 && (y % (16 * 10) == 0);
    }
}