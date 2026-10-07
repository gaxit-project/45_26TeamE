using UnityEngine;


public partial class VoxelTerrain
{
    public void RestartFromCheckpoint()
    {
        if (CheckpointManager.Instance == null) return;

        Vector3 lastPos = CheckpointManager.Instance.GetLastCheckpoint();
        GameObject player = GameObject.FindWithTag("Player");

        if (player != null)
        {
            player.transform.position = lastPos;
            ClearBlocksAroundPoint(lastPos, 4.0f);

            CheckpointManager.Instance.MarkCheckpointAsUsed();
        }
    }

    public void ResetRuntime(bool regenerateStage = false)
    {
        chunksToUpdate?.Clear();
        zoneUnlockedFlags?.Clear();
        zoneInitialGemValues?.Clear();
        if (spawnedTreasures != null)
        {
            for (int i = spawnedTreasures.Count - 1; i >= 0; i--)
            {
                var go = spawnedTreasures[i];
                if (go != null) Destroy(go);
            }
            spawnedTreasures.Clear();
        }
        if (regenerateStage)
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            CreateStage(maxStageWidthZ, GetTotalHeight(), blockSize);
        }
        else
        {
            if (chunks != null)
            {
                for (int i = 0; i < chunks.Length; i++)
                {
                    UpdateChunkMesh(i);
                }
            }
        }
    }

    private void TeleportPlayerToStart(int x, int y, int z)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Vector3 worldPos = transform.position + new Vector3(x * blockSize, y * blockSize + 1.5f, z * blockSize);
            player.transform.position = worldPos;
        }
    }
}
