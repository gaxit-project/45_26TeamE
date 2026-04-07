using UnityEngine;

public class Block_dirt : MonoBehaviour
{
    [SerializeField,Header("ブロックの体力")] int maxHP = 3;
    public int currentHP;

    [Header("段階ごとのマテリアル")]
    public Material matNormal;  // 傷なし
    public Material matCracked; // ちょっとヒビ
    public Material matBroken;  // 崩壊寸前

    private MeshRenderer meshRenderer;

    void Start()
    {
        currentHP = maxHP;
        // 自分の見た目を変更するためのコンポーネントを取得
        meshRenderer = GetComponent<MeshRenderer>();
        UpdateAppearance(); // 最初は「傷なし」にする
    }

    // プレイヤーから呼ばれるダメージ処理
    public void TakeDamage(int damageAmount)
    {
        currentHP -= damageAmount;

        if (currentHP <= 0)
        {
            // 体力が0以下になったら破壊！
            Destroy(gameObject);
        }
        else
        {
            // まだ体力が残っていれば見た目を更新
            UpdateAppearance();
        }
    }

    // 体力に合わせてマテリアルを張り替える処理
    private void UpdateAppearance()
    {
        if ((float)currentHP/(float)maxHP >= 1)//ダメージをうけなければこれ
        {
            meshRenderer.material = matNormal;
        }
        else if ((float)currentHP / (float)maxHP > (float)(1/3))//一度でもダメージを受けるとこれ
        {
            meshRenderer.material = matCracked;
        }
        else//３分の１より低くなるとこれ
        {
            meshRenderer.material = matBroken;
        }
    }
}
