using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoalJewelry : MonoBehaviour
{
    [SerializeField] private string SceneName;
    [Header("エフェクトプレハブ")]
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
            GameLogger.Event("GameClear", TimerManager.Instance != null ? $"timeLeft={TimerManager.Instance.TotalTime:F1}" : null);

            SoundManager.Instance.StopBGM();
            SoundManager.Instance.PlaySE("Clear");
            isGoalReached = true;

            if (TimerManager.Instance != null)
            {
                TimerManager.Instance.StopTimer();
                TimerManager.Instance.StopAllCoroutines();
                FinalResultManager.RecordOxygenRemaining(TimerManager.Instance.TotalTime);
            }

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
                player.FaceRight();
                player.currentState = PlayerController.PlayerState.GameClear;
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;
            }
            else
            {
                Transform root = other.transform.root;
                if (root != null && Mathf.Abs(Mathf.DeltaAngle(root.eulerAngles.y, 0f)) > 1f)
                {
                    root.rotation = Quaternion.Euler(0, 0, 0);
                }
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

        yield return new WaitForSeconds(8.0f);

        Debug.LogWarning("[GoalJewelry] 8秒のクリア演出が完了しました。FinalResult シーンをロードします。");
        string targetScene = "FinalResult";
        SceneManager.LoadScene(targetScene);

        Destroy(gameObject);
    }
}
