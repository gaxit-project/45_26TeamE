using UnityEngine;

public class DrillTip : MonoBehaviour
{
    [SerializeField] float drillInterval = 0.2f;
    [SerializeField] float drillRadius = 1.5f;
    [SerializeField] Transform miningZone;

    private float lastDrillTime;
    private PlayerController player;

    void Start()
    {
        player = GetComponentInParent<PlayerController>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (player == null || !player.IsDrilling || !player.HasBattery) return;
        if (Time.time < lastDrillTime + drillInterval) return;

        if (other.CompareTag("VoxelTerrain"))
        {
            VoxelTerrain terrain = other.GetComponent<VoxelTerrain>();
            if (terrain != null)
            {
                Vector3 localPos = terrain.transform.InverseTransformPoint(transform.position);
                float s = terrain.BlockSize;
                int x = Mathf.FloorToInt(localPos.x / s);
                int y = Mathf.FloorToInt(localPos.y / s);
                int z = Mathf.FloorToInt(localPos.z / s);

                Bounds bounds = miningZone.GetComponent<BoxCollider>().bounds;

                Vector3 minL = terrain.transform.InverseTransformPoint(bounds.min) / s;
                Vector3 maxL = terrain.transform.InverseTransformPoint(bounds.max) / s;

                terrain.ExecuteDig(x, y, z, drillRadius / s, minL, maxL);
                lastDrillTime = Time.time;
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
