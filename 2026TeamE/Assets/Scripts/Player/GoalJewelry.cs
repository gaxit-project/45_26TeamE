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
        
        if (other.gameObject.CompareTag("Player") && !isProcessed)
        {
            isProcessed = true;

            // ゴール時に振動を停止
            if (HapticsManager.Instance != null)
            {
                HapticsManager.Instance.Stop();
            }

            // プレイヤーの操作を無効化
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.currentState = PlayerController.PlayerState.GameClear;
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;
            }

            Animator playerAnim = other.GetComponent<Animator>();
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

        
        Instantiate(EfectPrefab, transform.position + new Vector3(5, 0, 0), Quaternion.Euler(-90, -90, 0));

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("着水１");
        }

        
        
        yield return new WaitForSeconds(6.0f);

        
        isGoalReached = true;

        
        // 以前はタイトルに戻っていた処理を Result (FinalResult) へ変更
        string targetScene = "FinalResult";
        SceneManager.LoadScene(targetScene);

        
        Destroy(gameObject);
    }
}
