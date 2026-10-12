using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoalJewelry : MonoBehaviour
{
    [SerializeField] private string SceneName;
    [Header("繧ｨ繝輔ぉ繧ｯ繝医・繝ｬ繝上ヶ")]
    public GameObject EfectPrefab;

    public static bool isGoalReached = false;
    private bool isProcessed = false; 

    void OnTriggerEnter(Collider other)
    {
        
        bool isPlayer = other.gameObject.CompareTag("Player") || 
                        (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player")) || 
                        other.transform.root.CompareTag("Player");

        if (isPlayer && !isProcessed)
        {
            isProcessed = true;

            // 即座にゴール到達フラグとセーブデータを書き込む
            isGoalReached = true;
            PlayerPrefs.SetInt("GoalReached", 1);
            PlayerPrefs.Save();
            Debug.LogWarning("[GoalJewelry] ゴール判定が発火しました！ isGoalReached = true, PlayerPrefs(GoalReached) = 1 を保存しました。");

            // タイマーを完全停止し、酸素残量を即座に記録
            if (TimerManager.Instance != null)
            {
                TimerManager.Instance.StopTimer();
                TimerManager.Instance.StopAllCoroutines();
                FinalResultManager.RecordOxygenRemaining(TimerManager.Instance.TotalTime);
                Debug.LogWarning($"[GoalJewelry] タイマー停止。最終フロアの残り酸素: {TimerManager.Instance.TotalTime} を記録しました。");
            }

            // ゴール時に振動を停止
            if (HapticsManager.Instance != null)
            {
                HapticsManager.Instance.Stop();
            }
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.StopLoopSE();
            }

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.DisablePlayerControl();
                player.currentState = PlayerController.PlayerState.GameClear;
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;
            }

            Animator playerAnim = other.GetComponentInParent<Animator>();
            if (playerAnim != null)
            {
                playerAnim.SetTrigger("Clear");
            }

            StartCoroutine(ClearSequence());
        }
    }

    IEnumerator ClearSequence()
    {
        
        yield return PickupAnimationUtil.PopAndFlash(transform);

        if (EfectPrefab != null)
        {
            Instantiate(EfectPrefab, transform.position, Quaternion.Euler(-90, -90, 0));
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("着水音");
        }

        
        
        yield return new WaitForSeconds(6.0f);

        Debug.LogWarning("[GoalJewelry] 6秒のクリア演出が完了しました。FinalResult シーンをロードします。");
        string targetScene = "FinalResult";
        SceneManager.LoadScene(targetScene);

        Destroy(gameObject);
    }
}

