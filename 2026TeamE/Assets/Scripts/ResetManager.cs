using UnityEngine;




public static class ResetManager
{
    public static void ResetAll()
    {
        
        ItemInventoryManager.ClearCollectedData();

        
        if (MainManager.Instance != null)
        {
            Object.Destroy(MainManager.Instance.gameObject);
        }
        if (MoneyManager.Instance != null)
        {
            Object.Destroy(MoneyManager.Instance.gameObject);
        }
        if (CheckpointManager.Instance != null)
        {
            Object.Destroy(CheckpointManager.Instance.gameObject);
        }
        if (VoxelTerrain.Instance != null)
        {
            Object.Destroy(VoxelTerrain.Instance.gameObject);
        }

        
        TimelineManager.ResetTimeline();

        
        UpgradeManager.ResetUpgrades();

        
        TimerManager.ResetFirstLoad();

        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopLoopSE();
            SoundManager.Instance.StopSE();
        }
        if (HapticsManager.Instance != null)
        {
            HapticsManager.Instance.Stop();
        }
    }
}
