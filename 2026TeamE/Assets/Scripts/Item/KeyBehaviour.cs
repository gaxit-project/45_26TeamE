using UnityEngine;

public class KeyBehaviour : MonoBehaviour, ICollectible
{
    [SerializeField] private ItemData data;
    private bool isGot = false;

    /// <summary>
    /// 鍵を集める仕様は廃止したため、ゾーン番号は保持するだけで使用していない。
    /// プレハブ側の互換のためメソッドだけ残している。
    /// </summary>
    public void Setup(int zoneIndex)
    {
    }

    public void Collect()
    {
        if (isGot) return;
        isGot = true;

        if(SoundManager.Instance != null && data != null && !string.IsNullOrEmpty(data.seName))
        {
            SoundManager.Instance.PlaySE(data.seName);
        }
        if(data != null && data.effectPrefab != null)
        {
            Instantiate(data.effectPrefab, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}
