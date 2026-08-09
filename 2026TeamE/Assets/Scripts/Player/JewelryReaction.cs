using System.Collections;
using UnityEngine;

public class JewelryReaction : BuriedItemBase, ICollectible
{
    [Header("アイテム情報")]
    public ItemType itemType = ItemType.Jewelry;
    [Tooltip("UI表示用のアイコンSprite（jewelry.pngなど）")]
    public Sprite uiIcon;
    [Tooltip("リザルト画面で宝箱を開けた時に加算される金額")]
    public int moneyValue = 300000;
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

    // アイテム取得時の共通関数
    public void Collect()
    {
        if (!isExposed || isGot) return;
        isGot = true;
        if (currentMarker != null)
        {
            Destroy(currentMarker);
        }
        if (ItemInventoryManager.Instance != null && uiIcon != null)
        {
            Vector3 capturedPos = transform.position;
            ItemInventoryManager.Instance.AddItem(itemType, uiIcon, capturedPos, moneyValue);
        }
        StartCoroutine(GetAnime());
    }

    IEnumerator GetAnime()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("宝石入手");
        }

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

        Destroy(gameObject);
    }
}
