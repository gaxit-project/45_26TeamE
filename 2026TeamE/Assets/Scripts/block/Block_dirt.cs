using UnityEngine;

public class Block_dirt : MonoBehaviour
{
    [SerializeField, Header("ブロックの体力")] int maxHP = 10;
    public int currentHP;

    [Header("段階ごとのマテリアル")]
    public Material matNormal;  // 傷なし
    public Material matCracked; // ちょっとヒビ
    public Material matBroken;  // 崩壊寸前

    private MeshRenderer meshRenderer;

    // 【追加】無限ループ防止用のフラグ
    private bool isDead = false;

    void Start()
    {
        currentHP = maxHP;
        meshRenderer = GetComponent<MeshRenderer>();
        UpdateAppearance();
    }

    public void TakeDamage(int damageAmount)
    {
        // 【重要】すでに死んでいる（破壊処理中）なら、ここで処理を強制終了！
        if (isDead) return;

        currentHP -= damageAmount;

        if (currentHP <= 0)
        {
            isDead = true; // 「もう死んでるよ」とマークをつける

            DestroyChain();
            Destroy(gameObject);
        }
        else
        {
            UpdateAppearance();
        }
    }

    private void UpdateAppearance()
    {
        // 割り算の判定を少しシンプルに修正しました
        float healthRatio = (float)currentHP / maxHP;

        if (healthRatio == 1.0f)
        {
            meshRenderer.material = matNormal;
        }
        else if (healthRatio > 0.33f) // 1/3より多ければ
        {
            meshRenderer.material = matCracked;
        }
        else
        {
            meshRenderer.material = matBroken;
        }
    }

    private void DestroyChain()
    {
        // 縦(Y軸)と横(Z軸)の4方向
        Vector3[] directions = { Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        float maxDistance = 0.55f;

        foreach (Vector3 dir in directions)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, dir, out hit, maxDistance))
            {
                if (hit.collider.CompareTag("Block_dirt"))
                {
                    Block_dirt targetBlock = hit.collider.GetComponent<Block_dirt>();

                    if (targetBlock != null)
                    {
                        // 相手の体力によって「連鎖」か「巻き添え」かを変える！
                        if (targetBlock.currentHP == 1)
                        {
                            // 相手が体力1なら、確実にとどめを刺して連鎖を繋ぐ！
                            targetBlock.TakeDamage(1);
                        }
                        else
                        {
                            // 相手が普通の体力（2以上）なら、巻き添えダメージを与えて連鎖ストップ！
                            // （例として最大HPの1/3のダメージを与えます）
                            targetBlock.TakeDamage(targetBlock.maxHP / 3);
                        }
                    }
                }
            }
        }
    }
}