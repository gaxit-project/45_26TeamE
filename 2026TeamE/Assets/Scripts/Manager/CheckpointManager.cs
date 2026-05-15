using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    private Vector3 savedPosition;
    private bool hasCheckpoint = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // ← シーン跨ぎ
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 中継地点を保存
    public void SaveCheckpoint(Vector3 position)
    {
        savedPosition = position;
        hasCheckpoint = true;

        Debug.Log("チェックポイント保存: " + position);
    }

    // 復元用
    public bool HasCheckpoint()
    {
        return hasCheckpoint;
    }

    public Vector3 GetCheckpointPosition()
    {
        return savedPosition;
    }
}