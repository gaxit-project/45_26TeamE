using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;





public class DebugItemManager : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD

    [Header("アイテムデータ設定（インスペクターで割り当てる）")]
    [SerializeField] private ItemData jewelryData;
    [SerializeField] private ItemData leatherBagData;
    [SerializeField] private ItemData GoldleatherBagData;
    [SerializeField] private ItemData keyData;

    [Header("デバッグ設定")]
    [SerializeField] private float addOxygenTime = 30f; 

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        
        if (kb.digit1Key.wasPressedThisFrame) AddDebugItem(ItemType.Jewelry, jewelryData);
        if (kb.digit2Key.wasPressedThisFrame) AddDebugItem(ItemType.LeatherBag, leatherBagData);
        if (kb.digit3Key.wasPressedThisFrame) AddDebugItem(ItemType.GoldLeatherBag, GoldleatherBagData);
        if (kb.digit4Key.wasPressedThisFrame) AddDebugItem(ItemType.Key, keyData);

        
        if (kb.digit5Key.wasPressedThisFrame)
        {
            if (TimerManager.Instance != null)
            {
                TimerManager.Instance.AddTime(addOxygenTime);
                Debug.Log($"[Debug] 酸素を取得しました。制限時間を {addOxygenTime} 秒延長！");
            }
        }

        
        if (kb.digit6Key.wasPressedThisFrame)
        {
            EnterCheckpointProcess();
        }

        
        if (kb.digit7Key.wasPressedThisFrame)
        {
            TriggerGoal();
        }

        
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

    
    private void EnterCheckpointProcess()
    {
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && CheckpointManager.Instance != null)
        {
            
            CheckpointManager.Instance.SaveCheckpoint(player.transform.position, 999);
        }

        
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
    

    private void TriggerGoal()
    {
        Debug.Log("[Debug] 7キー: BigJewelryを取得しました！ ゴール演出をスキップしてResultシーンへ移行します。");
        GoalJewelry.isGoalReached = true;
        SceneManager.LoadScene("Result");
    }

#endif
}