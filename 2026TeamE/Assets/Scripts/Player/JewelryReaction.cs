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
    
    // 生成したマーカーを覚えておくための変数
    private GameObject currentMarker;

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
            
            // 古いマーカーが残っていたら消す（重複防止）
            if (currentMarker != null)
            {
                Destroy(currentMarker);
            }

            // 新しく生成して変数に保存しておく
            currentMarker = Instantiate(marker, ob);
            currentMarker.transform.localPosition = new Vector3(0, 0, 4);
            currentMarker.transform.localRotation = Quaternion.Euler(0, -90, 0);

            // 生成したマーカーを5秒後に自動で消す
            Destroy(currentMarker, 5f);
        }

        Invoke("ResetReaction", cooldownTime);
    }

    void ResetReaction()
    {
        isCoolingDown = false;
    }

    void Get()
    {
        // 宝石を取得した瞬間にマーカーを消す
        if (currentMarker != null)
        {
            Destroy(currentMarker);
        }

        MoneyManager.Instance.MoneyOnHandIncrease(300000);
        StartCoroutine(GetAnime());
    }

    IEnumerator GetAnime()
    {
        // 最初の位置を記録（Xのみ5に変更して画面手前に出す）
        Vector3 startPos = new Vector3(5, transform.position.y, transform.position.z);
        
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        
        float animDuration = 0.5f; 
        float flashInterval = 0.05f; 
        float popHeight = 1.5f; 
        float elapsedTime = 0f;

        while (elapsedTime < animDuration)
        {
            float t = elapsedTime / animDuration;
            
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            float currentY = startPos.y + (popHeight * easeOut);
            
            transform.position = new Vector3(startPos.x, currentY, startPos.z);

            bool isVisible = (elapsedTime % (flashInterval * 2)) < flashInterval;
            foreach (Renderer r in renderers)
            {
                if (r != null) r.enabled = isVisible;
            }

            elapsedTime += Time.deltaTime; 
            yield return null; 
        }

        Instantiate(EfectPrefab, transform.position + new Vector3(5, 0, 0), Quaternion.Euler(-90, -90, 0));
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("宝石獲得");
        }
        
        Destroy(gameObject);
    }
}
