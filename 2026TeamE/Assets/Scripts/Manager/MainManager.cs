using UnityEngine;

public class MainManager : MonoBehaviour
{
    [Header("お金")]
    [SerializeField] private long currentMoney = 0;
    [SerializeField] private int oreValue = 0;

    [Header("ステージ")]
    [SerializeField] private int targetHeight = 500;
    [SerializeField] private int targetWidth = 80;
    [SerializeField] private float blockSize = 0.2f;

    private void Start()
    {
        SpawnNewLevel();
    }

    public void SpawnNewLevel()
    {
        VoxelTerrain.Instance.CreateStage(targetWidth, targetHeight, blockSize);
    }
}
