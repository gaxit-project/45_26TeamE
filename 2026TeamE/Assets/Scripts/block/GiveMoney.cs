using UnityEngine;

public class GiveMoney : MonoBehaviour
{
    public MoneyManager moneyManager;

    [Header("種類別の金額")]
    [SerializeField] private int dirtValue = 10; // 土 (SubMesh 0)
    [SerializeField] private int oreValue = 50;  // 鉱石 (SubMesh 1)

    private int lastDirtIndices = -1;
    private int lastOreIndices = -1;

    void Start()
    {
        // 初期状態のインデックス数を記録
        UpdateCounts(out lastDirtIndices, out lastOreIndices);
    }

    void Update()
    {
        if (moneyManager == null) return;

        int currentDirtIndices;
        int currentOreIndices;
        UpdateCounts(out currentDirtIndices, out currentOreIndices);

        // --- 土の判定 ---
        if (currentDirtIndices < lastDirtIndices && lastDirtIndices != -1)
        {
            // インデックス数から減少したブロック数を計算 (1面6枚のインデックス * 最大6面 = 36)
            // 概算で減少分を判定し、加算
            int diff = lastDirtIndices - currentDirtIndices;
            if (diff > 0) moneyManager.MoneyOnHandIncrease(dirtValue);
        }

        // --- 鉱石の判定 ---
        if (currentOreIndices < lastOreIndices && lastOreIndices != -1)
        {
            int diff = lastOreIndices - currentOreIndices;
            if (diff > 0) moneyManager.MoneyOnHandIncrease(oreValue);
        }

        lastDirtIndices = currentDirtIndices;
        lastOreIndices = currentOreIndices;
    }

    // 全チャンクのSubMeshごとのインデックス数を集計
    private void UpdateCounts(out int dirtTotal, out int oreTotal)
    {
        dirtTotal = 0;
        oreTotal = 0;

        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>();
        foreach (var mf in filters)
        {
            if (mf.sharedMesh != null && mf.sharedMesh.subMeshCount >= 2)
            {
                // SubMesh 0 (Dirt) のインデックス数を加算
                dirtTotal += (int)mf.sharedMesh.GetIndexCount(0);
                // SubMesh 1 (Ore) のインデックス数を加算
                oreTotal += (int)mf.sharedMesh.GetIndexCount(1);
            }
        }
    }
}
