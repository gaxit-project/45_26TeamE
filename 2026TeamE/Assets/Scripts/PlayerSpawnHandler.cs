using UnityEngine;

public class PlayerSpawnHandler : MonoBehaviour
{
    void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        if (CheckpointManager.Instance != null && CheckpointManager.Instance.HasCheckpoint())
        {
            player.transform.position = CheckpointManager.Instance.GetCheckpointPosition();
            Debug.Log("チェックポイントから再開");
        }
        else
        {
            Debug.Log("初期スポーン位置");
        }
    }
}