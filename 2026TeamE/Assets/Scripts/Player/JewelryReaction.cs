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
    public bool IsGot => isGot;

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

        yield return PickupAnimationUtil.PopAndFlash(transform);

        Instantiate(EfectPrefab, transform.position + new Vector3(5, 0, 0), Quaternion.Euler(-90, -90, 0));

        Destroy(gameObject);
    }
}
