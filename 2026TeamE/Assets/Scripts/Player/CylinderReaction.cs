using System.Collections;
using UnityEngine;

public class CylinderReaction : BuriedItemBase, ICollectible
{
    [Header("タイマー延長")]
    [Tooltip("取得時に制限時間に加算される秒数")]
    public float timeBonus = 10f;
    [Header("エコープレハブ")]
    public GameObject visualEchoPrefab;
    [Header("マーカー")]
    public GameObject marker;
    [Header("エフェクトプレハブ")]
    public GameObject EfectPrefab;

    private bool isGot = false;

    protected override (GameObject echoPrefab, GameObject markerPrefab) GetReactionPrefabs()
    {
        return (visualEchoPrefab, marker);
    }

    // アイテム取得時の共通関数（PlayerControllerがOnTriggerEnter/Stayで呼び出す）
    public void Collect()
    {
        if (isExposed)
        {
            Get();
        }
    }

    void Get()
    {
        if (isGot) return; // 既に取得済みなら何もしない
        isGot = true;

        // 宝石を取得した瞬間にマーカーを消す
        if (currentMarker != null)
        {
            Destroy(currentMarker);
        }

        // 制限時間を延長する
        if (TimerManager.Instance != null)
        {
            TimerManager.Instance.AddTime(timeBonus);
        }

        StartCoroutine(GetAnime());
    }

    IEnumerator GetAnime()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("宝石入手");
        }

        // 最初の位置を記録
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

        Destroy(gameObject);
    }
}
