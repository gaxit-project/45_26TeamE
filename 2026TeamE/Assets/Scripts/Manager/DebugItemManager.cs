using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 開発・デバッグ専用のアイテム取得・進行管理マネージャー。
/// 製品版ビルド時には自動でコードが除外されます。
/// </summary>
public class DebugItemManager : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD

    [Header("アイテムデータ設定（インスペクターで割り当てる）")]
    [SerializeField] private ItemData jewelryData;
    [SerializeField] private ItemData leatherBagData;
    [SerializeField] private ItemData keyData;

    [Header("デバッグ設定")]
    [SerializeField] private float addOxygenTime = 30f; // 酸素取得時の延長時間

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // 【1〜3キー】通常アイテム取得
        if (kb.digit1Key.wasPressedThisFrame) AddDebugItem(ItemType.Jewelry, jewelryData);
        if (kb.digit2Key.wasPressedThisFrame) AddDebugItem(ItemType.LeatherBag, leatherBagData);
        if (kb.digit3Key.wasPressedThisFrame) AddDebugItem(ItemType.Key, keyData);

        // 【4キー】酸素取得（タイム延長）
        if (kb.digit4Key.wasPressedThisFrame)
        {
            if (TimerManager.Instance != null)
            {
                TimerManager.Instance.AddTime(addOxygenTime);
                Debug.Log($"[Debug] 酸素を取得しました。制限時間を {addOxygenTime} 秒延長！");
            }
        }

        // 【6キー】チェックポイント（中継地点）突入処理を実行（ワープせずその場でUI展開）
        if (kb.digit6Key.wasPressedThisFrame)
        {
            EnterCheckpointProcess();
        }

        // 【7キー】BigJewelry (ゴールアイテム) 取得 → リザルトへ移行
        if (kb.digit7Key.wasPressedThisFrame)
        {
            TriggerGoal();
        }

        // 【9キー】アイテム削除（ダメージ等のデバッグ用）
        if (kb.digit9Key.wasPressedThisFrame)
        {
            if (ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.RemoveLastItem())
            {
                Debug.Log("[Debug] 最後のアイテムを削除しました");
            }
        }
    }

    private void AddDebugItem(ItemType type, ItemData data)
    {
        if (ItemInventoryManager.Instance != null && data != null)
        {
            Vector3 spawnPos = Camera.main != null
                ? Camera.main.transform.position + Camera.main.transform.forward * 2f
                : Vector3.zero;

            ItemInventoryManager.Instance.AddItem(type, data.uiIcon, spawnPos, data.moneyValue);
            Debug.Log($"[Debug] {type} を取得しました！");
        }
    }

    // --- 変更箇所 ---
    private void EnterCheckpointProcess()
    {
        // 1. ついでに現在位置を仮のチェックポイントとして保存しておく（必要に応じて）
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && CheckpointManager.Instance != null)
        {
            // ID:999として現在のプレイヤー位置を保存
            CheckpointManager.Instance.SaveCheckpoint(player.transform.position, 999);
        }

        // 2. チェックポイント突入時のUI（SelectPanel等）を強制的に表示する
        SelectPoint selectPoint = FindObjectOfType<SelectPoint>();
        if (selectPoint != null)
        {
            selectPoint.ShowButton();
            Debug.Log("[Debug] チェックポイント突入処理を実行し、UIを表示しました！");
        }
        else
        {
            Debug.LogWarning("[Debug] シーン内に SelectPoint スクリプトが見つかりませんでした。");
        }
    }
    // --------------

    private void TriggerGoal()
    {
        Debug.Log("[Debug] 7キー: BigJewelryを取得しました！ ゴール演出をスキップしてResultシーンへ移行します。");
        GoalJewelry.isGoalReached = true;
        SceneManager.LoadScene("Result");
    }

#endif
}