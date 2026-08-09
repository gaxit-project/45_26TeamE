using UnityEngine;

// チェックポイント、ゲーム進行
public partial class VoxelTerrain
{
    public void OnPlayerReachRelayPoint(int y)
    {
        Debug.Log($"中継地点到達. 深度：{y}");
        if (CheckpointManager.Instance == null) return;

        int currentID = GetRelayID(y);

        if (currentID == CheckpointManager.Instance.GetUsedCheckpointID())
        {
            Debug.Log("同じチェックポイントのためスキップ");
            return;
        }

        if (!IsZoneCleared(currentID))
        {
            int currentCount = zoneCollectedKeyCounts.ContainsKey(currentID) ? zoneCollectedKeyCounts[currentID] : 0;
            Debug.Log($"アクセス拒否：ゾーン {currentID} の鍵が足りません ({currentCount}/3)");
            return;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        Vector3 checkpointPos = player.transform.position;
        checkpointPos.y -= (blockSize * 5f);

        CheckpointManager.Instance.SaveCheckpoint(checkpointPos, currentID);

        if (TryGetComponent<SelectPoint>(out var selectPoint))
        {
            selectPoint.ShowButton();
        }
    }

    public void CollectedKey(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int blockY = Mathf.Clamp(Mathf.FloorToInt(localPos.y / blockSize), 0, heightY - 1);
        int zoneIndex = GetRelayID(blockY);

        CollectedKeyDirect(zoneIndex, worldPos);
        Debug.Log($"<color=yellow>[鍵獲得]</color> 深度: {blockY} (ゾーン: {zoneIndex}) | 現在の鍵: {zoneCollectedKeyCounts[zoneIndex]} / 3個");
    }

    public void CollectedKeyDirect(int zoneIndex, Vector3 worldPos)
    {
        if (!zoneCollectedKeyCounts.ContainsKey(zoneIndex))
        {
            zoneCollectedKeyCounts[zoneIndex] = 0;
        }
        zoneCollectedKeyCounts[zoneIndex]++;

        if (KeyUIController.Instance != null)
        {
            KeyUIController.Instance.FlyAndUpdateKeyUI(zoneCollectedKeyCounts[zoneIndex], worldPos);
        }
    }

    public bool IsZoneCleared(int zoneIndex)
    {
        const int REQUIRED_KEYS = 3;
        if (zoneCollectedKeyCounts.TryGetValue(zoneIndex, out int count))
        {
            return count >= REQUIRED_KEYS;
        }
        return false;
    }

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
        zoneCollectedKeyCounts?.Clear();
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
