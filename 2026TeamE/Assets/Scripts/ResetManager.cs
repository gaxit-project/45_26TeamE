using UnityEngine;

/// <summary>
/// ゲーム全体の進行状態を一括リセットする
/// </summary>
public static class ResetManager
{
    public static void ResetAll()
    {
        // アイテムのリセット
        ItemInventoryManager.ClearCollectedData();

        // 進行状況を保持するマネージャーの破棄
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

        // タイムラインの再生状態をリセット
        TimelineManager.ResetTimeline();

        // アップグレードのリセット
        UpgradeManager.ResetUpgrades();

        // タイマーの待機フラグをリセット
        TimerManager.ResetFirstLoad();

        // ループSE・ハプティクスを停止
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
