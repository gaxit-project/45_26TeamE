using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoalJewelry : MonoBehaviour
{
    [SerializeField] private string SceneName;
    [Header("エフェクトプレハブ")]
    public GameObject EfectPrefab;

    public static bool isGoalReached = false;
    private bool isProcessed = false; // 重複実行防止用フラグ

    void OnTriggerEnter(Collider other)
    {
        // 重複処理を防ぐフラグチェックを追加
        if (other.gameObject.CompareTag("Player") && !isProcessed)
        {
            isProcessed = true;

            // プレイヤーのAnimatorを取得してアニメーションを実行
            Animator playerAnim = other.GetComponent<Animator>();
            if (playerAnim != null)
            {
                playerAnim.SetTrigger("Clear");
            }

            // シーン移動などの一連の処理を開始
            StartCoroutine(ClearSequence());
        }
    }

    IEnumerator ClearSequence()
    {
        // 1. ポップ＆フラッシュエフェクトを実行
        yield return PickupAnimationUtil.PopAndFlash(transform);

        // 2. エフェクトの生成とSE再生
        Instantiate(EfectPrefab, transform.position + new Vector3(5, 0, 0), Quaternion.Euler(-90, -90, 0));

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("着水１");
        }

        // 3. プレイヤー検知から合計6秒経つまで待機
        // (PopAndFlashの演出時間を差し引いた残りの時間を待機します)
        yield return new WaitForSeconds(6.0f);

        // 4. ゴール到達処理とシーン遷移
        isGoalReached = true;

        // インスペクターで指定されたSceneNameがあればそれを使い、なければ"Result"をロード
        string targetScene = string.IsNullOrEmpty(SceneName) ? "Result" : SceneName;
        SceneManager.LoadScene(targetScene);

        // 自身を削除
        Destroy(gameObject);
    }
}
