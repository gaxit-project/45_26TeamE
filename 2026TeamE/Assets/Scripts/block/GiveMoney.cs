using UnityEngine;

public class GiveMoney : MonoBehaviour
{
    public MoneyManager moneyManager;

    [Header("換金額")]
    [SerializeField] private int dirtValue = 10;
    [SerializeField] private int oreValue = 50;

    void Start()
    {
        if (moneyManager == null)
        {
            moneyManager = MoneyManager.Instance;
        }

        if (VoxelTerrain.Instance != null)
        {
            VoxelTerrain.Instance.OnBlocksDestroyedByPlayer += HandleBlocksDestroyed;
        }
    }

    private void HandleBlocksDestroyed(int dirtCount, int oreCount)
    {
        if (moneyManager == null)
        {
            moneyManager = MoneyManager.Instance;
        }
        if (moneyManager == null) return;

        int totalIncrease = (dirtCount * dirtValue) + (oreCount * oreValue);
        if (totalIncrease > 0)
        {
            moneyManager.MoneyOnHandIncrease(totalIncrease);
        }
    }

    void OnDestroy()
    {
        if (VoxelTerrain.Instance != null)
        {
            VoxelTerrain.Instance.OnBlocksDestroyedByPlayer -= HandleBlocksDestroyed;
        }
    }
}
