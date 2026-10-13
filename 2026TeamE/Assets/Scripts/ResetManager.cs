using UnityEngine;

public static class ResetManager
{
    public static void ResetAll()
    {
        // どこかで止めたまま戻ってくる経路があるため、時間の進みを必ず元に戻す。
        // 0のままだとタイトルのInvokeやAnimatorが動かず、操作不能になる。
        Time.timeScale = 1f;

        ItemInventoryManager.ClearCollectedData();

        FinalResultManager.ResetStats();

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

        // 導入演出は PlayableDirector ごと作り直す。
        // TimelineManager と PlayableDirector は同じ GameObject にあり DontDestroyOnLoad で生き残るが、
        // 再生済みの Director は二度と再生されず、タイムライン側のバインド先（カメラ・プレイヤー）も
        // 破棄済みのオブジェクトを指したままになる。
        // ここで破棄しておけば、次に 02_Main を読み込んだときシーン側の TimeLine が
        // 正しいバインドを持った状態で Instance になり、導入演出が最初から再生される。
        if (TimelineManager.Instance != null)
        {
            Object.Destroy(TimelineManager.Instance.gameObject);
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
