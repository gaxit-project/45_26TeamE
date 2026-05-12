using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class JewelryReaction : MonoBehaviour
{
    [Header("エコープレハブ")]
    public GameObject visualEchoPrefab;
    [Header("対象")]
    public Transform ob;
    [Header("マーカー")]
    public GameObject marker;
    [Header("エフェクトプレハブ")]
    public GameObject EfectPrefab;

    [Header("クールダウン")]
    public float cooldownTime = 1.0f;

    private bool isCoolingDown = false;

    private void Start()
    {
        ob = transform;
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name.Contains("sonar") && !isCoolingDown)
        {
            ExecuteReaction();
        }
        if (other.gameObject.CompareTag("Player"))
        {
            Get();
        }
    }

    void ExecuteReaction()
    {
        isCoolingDown = true;

        if (visualEchoPrefab != null)
        {
            Instantiate(visualEchoPrefab, transform.position, Quaternion.identity);
            GameObject m = Instantiate(marker, ob);
            m.transform.localPosition = new Vector3(0, 0, 4);
            m.transform.localRotation = Quaternion.Euler(0, -90, 0);
        }

        Invoke("ResetReaction", cooldownTime);
    }

    void ResetReaction()
    {
        isCoolingDown = false;
    }

    void Get()
    {
        MoneyManager.Instance.MoneyOnHandIncrease(300000);
        StartCoroutine(GetAnime());
    }

    IEnumerator GetAnime()
    {
        // 最初の位置を記録（Xのみ5に変更して画面手前に出す）
        Vector3 startPos = new Vector3(5, transform.position.y, transform.position.z);
        
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        
        float animDuration = 0.5f; // アニメーションの長さ（秒）
        float flashInterval = 0.05f; // 点滅のスピード
        float popHeight = 1.5f; // ポップアップで浮き上がる高さ
        float elapsedTime = 0f;

        while (elapsedTime < animDuration)
        {
            // 0 から 1 に向かって進む進行度
            float t = elapsedTime / animDuration;
            
            // 徐々に減速しながら上に浮き上がる計算（Ease Out Cubic）
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            float currentY = startPos.y + (popHeight * easeOut);
            
            // XとZは固定し、Yだけ動かす
            transform.position = new Vector3(startPos.x, currentY, startPos.z);

            // 経過時間を使って点滅を計算する
            bool isVisible = (elapsedTime % (flashInterval * 2)) < flashInterval;
            foreach (Renderer r in renderers)
            {
                if (r != null) r.enabled = isVisible;
            }

            elapsedTime += Time.deltaTime; // 1フレーム分の時間を進める
            yield return null; // 1フレーム待つ（なめらかに動かすために必須）
        }

        // アニメーションが終わったらエフェクトを出す
        Instantiate(EfectPrefab, transform.position + new Vector3(5, 0, 0), Quaternion.Euler(-90, -90, 0));
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("宝石獲得");
        }
        
        Destroy(gameObject);
    }
}
