using UnityEngine;
using System.Collections;

public class PlayerSpawnHandler : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return null;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) yield break;

        if (CheckpointManager.Instance != null && CheckpointManager.Instance.HasCheckpoint())
        {
            Vector3 pos = CheckpointManager.Instance.GetLastCheckpoint();
            player.transform.position = pos;

            if (VoxelTerrain.Instance != null)
            {
                VoxelTerrain.Instance.ClearBlocksAroundPoint(pos, 4.0f);
            }

            CheckpointManager.Instance.MarkCheckpointAsUsed();
        }
    }
}