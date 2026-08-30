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

    
    public void Collect()
    {
        if (isExposed)
        {
            Get();
        }
    }

    void Get()
    {
        if (isGot) return; 
        isGot = true;

        
        if (currentMarker != null)
        {
            Destroy(currentMarker);
        }

        
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

        yield return PickupAnimationUtil.PopAndFlash(transform);

        Instantiate(EfectPrefab, transform.position + new Vector3(5, 0, 0), Quaternion.Euler(-90, -90, 0));

        Destroy(gameObject);
    }
}
