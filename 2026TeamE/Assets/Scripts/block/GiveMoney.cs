using UnityEngine;

public class GiveMoney : MonoBehaviour
{
    public MoneyManager moneyManager;
    [SerializeField] private int addValue = 10;

    private VoxelTerrain terrain;
    private int lastBlockCount = -1;

    void Start()
    {
        terrain = GetComponent<VoxelTerrain>();
        // 最初のブロック数を数えておく
        lastBlockCount = CountCurrentBlocks();
    }

    void Update()
    {
        if (terrain == null || moneyManager == null) return;

        // 現在のブロック数をカウント
        int currentBlockCount = CountCurrentBlocks();

        // 前回のカウントより減っていたら、その分だけお金を増やす
        if (currentBlockCount < lastBlockCount)
        {
            int destroyedCount = lastBlockCount - currentBlockCount;
            int totalAdd = destroyedCount * addValue;

            moneyManager.MoneyOnHandIncrease(totalAdd);

            Debug.Log($"ブロックが {destroyedCount} 個消えたので {totalAdd} 円加算しました"); //消してもいいです
        }

        lastBlockCount = currentBlockCount;
    }

    // 現在ステージにある「空（0）じゃないブロック」を全部数える
    private int CountCurrentBlocks()
    {
        // Reflectionを使わずに、VoxelTerrainの内部データにアクセスできないため
        // mapDataを直接参照するか、VoxelTerrainにカウント用関数を追加するのが本来ですが
        // スクリプトを変えない制約のため、GetComponentのMesh情報から簡易計算します

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            // メッシュの頂点数からブロック数を概算（1ブロック=24頂点前後）
            // もしくは、より正確に判定するために terrain の Instance 経由でデータを読み取る等の工夫が必要
            return mf.sharedMesh.vertexCount;
        }
        return 0;
    }
}
