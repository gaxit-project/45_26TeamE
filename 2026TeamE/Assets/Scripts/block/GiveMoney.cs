using UnityEngine;

public class GiveMoney : MonoBehaviour
{
    public MoneyManager moneyManager;
    [SerializeField] private int addValue = 10;

    private VoxelTerrain terrain;
    private int lastVertexCount = -1;

    void Start()
    {
        terrain = GetComponent<VoxelTerrain>();
        // 初期状態の全チャンクの頂点数を取得
        lastVertexCount = CountTotalVertices();
    }

    void Update()
    {
        if (terrain == null || moneyManager == null) return;

        // 全チャンクの合計頂点数をカウント
        int currentVertexCount = CountTotalVertices();

        // 頂点数が減っている（＝ブロックが消えた）場合
        if (currentVertexCount < lastVertexCount)
        {
            // 1ブロックあたり24頂点（またはそれ以上）として計算
            // メッシュ生成の仕様により、減った頂点数を24で割ることでブロック数を概算します
            int diff = lastVertexCount - currentVertexCount;
            int destroyedCount = Mathf.CeilToInt(diff / 24f);

            if (destroyedCount > 0)
            {
                int totalAdd = destroyedCount * addValue;
                moneyManager.MoneyOnHandIncrease(totalAdd);
            }
        }

        lastVertexCount = currentVertexCount;
    }

    // 子オブジェクト（各チャンク）の全メッシュ頂点数を合計する
    private int CountTotalVertices()
    {
        int total = 0;
        // VoxelTerrainの子要素にある全てのMeshFilterから頂点数を集計
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>();

        foreach (var mf in filters)
        {
            if (mf.sharedMesh != null)
            {
                total += mf.sharedMesh.vertexCount;
            }
        }
        return total;
    }
}
