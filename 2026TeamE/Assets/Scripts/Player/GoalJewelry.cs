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
        
        if (other.gameObject.CompareTag("Player") && !isProcessed)
        {
            isProcessed = true;

            // 繧ｴ繝ｼ繝ｫ譎ゅ↓謖ｯ蜍輔ｒ蛛懈ｭ｢
            if (HapticsManager.Instance != null)
            {
                HapticsManager.Instance.Stop();
            }

            // 繧ｿ繧､繝槭・繧貞●豁｢・医け繝ｪ繧｢貍泌・荳ｭ縺ｫ譎る俣蛻・ｌ縺ｫ縺ｪ繧九・繧帝亟縺撰ｼ・            if (TimerManager.Instance != null)
            {
                TimerManager.Instance.StopTimer();
            }

            // 繝励Ξ繧､繝､繝ｼ縺ｮ謫堺ｽ懊ｒ辟｡蜉ｹ蛹・            PlayerController player = other.GetComponent<PlayerController>();
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

        if (EfectPrefab != null)
        {
            Instantiate(EfectPrefab, transform.position, Quaternion.Euler(-90, -90, 0));
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("逹豌ｴ・・);
        }

        
        
        yield return new WaitForSeconds(6.0f);

        
        isGoalReached = true;
        PlayerPrefs.SetInt("GoalReached", 1);
        PlayerPrefs.Save();
        if (TimerManager.Instance != null) { FinalResultManager.RecordOxygenRemaining(TimerManager.Instance.TotalTime); }

        
        // 莉･蜑阪・繧ｿ繧､繝医Ν縺ｫ謌ｻ縺｣縺ｦ縺・◆蜃ｦ逅・ｒ Result (FinalResult) 縺ｸ螟画峩
        string targetScene = "FinalResult";
        SceneManager.LoadScene(targetScene);

        
        Destroy(gameObject);
    }
}
