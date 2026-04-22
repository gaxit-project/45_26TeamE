using UnityEngine;

public class JewelryReaction : MonoBehaviour
{
    [Header("発生させるエコー波紋のプレハブ")]
    public GameObject visualEchoPrefab;
    [Header("入手エフェクトのプレハブ")]
    public GameObject EfectPrefab;

    [Header("一度反応してから次に反応できるようになるまでの時間")]
    public float cooldownTime = 1.0f;

    private bool isCoolingDown = false; // クールダウン中かどうか

    void OnTriggerEnter(Collider other)
    {
        // 名前が "sonar" を含み、かつクールダウン中でない場合のみ反応
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
        Debug.Log("宝石が検知されました！エコーを放ちます。");

        // エコー波紋を自分自身の位置に生成
        if (visualEchoPrefab != null)
        {
            Instantiate(visualEchoPrefab, transform.position, Quaternion.identity);
        }

        // 指定した秒数（cooldownTime）が経過した後に ResetReaction を呼び出す
        Invoke("ResetReaction", cooldownTime);
    }

    void ResetReaction()
    {
        isCoolingDown = false;
        Debug.Log("宝石が再び検知可能になりました。");
    }

    void Get()
    {
        MoneyManager.Instance.MoneyOnHandIncrease(300000);
        Instantiate(EfectPrefab, transform.position+new Vector3(5,0,0), Quaternion.Euler(-90,-90,0));
        SoundManager.Instance.PlaySE("宝石入手");
        Destroy(gameObject);
    }
}