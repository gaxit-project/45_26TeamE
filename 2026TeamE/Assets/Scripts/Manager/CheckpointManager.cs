using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    private Vector3 savedLocalPosition;
    private bool hasCheckpoint = false;

    private int lastCheckpointID = -1;   // 最後に保存したチェックポイント
    private int usedCheckpointID = -1;   // リスポーンで使ったチェックポイント

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // チェックポイント保存（ローカル座標＋ID）
    public void SaveCheckpoint(Vector3 worldPos, int checkpointID)
    {
        if (VoxelTerrain.Instance != null)
        {
            savedLocalPosition = VoxelTerrain.Instance.transform.InverseTransformPoint(worldPos);
        }
        else
        {
            savedLocalPosition = worldPos;
        }

        hasCheckpoint = true;
        lastCheckpointID = checkpointID;

        Debug.Log($"チェックポイント保存 ID:{checkpointID}");
    }

    public bool HasCheckpoint() => hasCheckpoint;

    public Vector3 GetLastCheckpoint()
    {
        if (VoxelTerrain.Instance != null)
        {
            return VoxelTerrain.Instance.transform.TransformPoint(savedLocalPosition);
        }
        return savedLocalPosition;
    }

    // リスポーン時に呼ぶ
    public void MarkCheckpointAsUsed()
    {
        usedCheckpointID = lastCheckpointID;
    }

    public int GetUsedCheckpointID()
    {
        return usedCheckpointID;
    }

    public int GetLastCheckpointID()
    {
        return lastCheckpointID;
    }

    public void ResetCheckpoint()
    {
        hasCheckpoint = false;
        savedLocalPosition = Vector3.zero;
        lastCheckpointID = -1;
        usedCheckpointID = -1;
    }
}