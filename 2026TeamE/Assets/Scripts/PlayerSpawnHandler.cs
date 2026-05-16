using UnityEngine;

public class PlayerSpawnHandler : MonoBehaviour
{
    void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        if (CheckpointManager.Instance != null && CheckpointManager.Instance.HasCheckpoint())
        {
            Vector3 restartPos = CheckpointManager.Instance.GetLastCheckpoint();
            player.transform.position = restartPos;
            if (VoxelTerrain.Instance != null)
            {
                VoxelTerrain.Instance.ClearBlocksAroundPoint(restartPos, 4.0f);
            }
        }
        else
        {
            Debug.Log("初期スポーン位置");
        }
    }
}