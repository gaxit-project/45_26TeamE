using UnityEngine;

public class GiveMoney : MonoBehaviour
{
    public MoneyManager moneyManager;

    [Header("種類別の金額")]
    [SerializeField] private int dirtValue = 10;
    [SerializeField] private int oreValue = 50;

    private int lastDirtIndices = -1;
    private int lastOreIndices = -1;
    private bool isInitialized = false; // 初期化フラグ

    void Update()
    {
        if (moneyManager == null) return;

        int currentDirtIndices;
        int currentOreIndices;
        UpdateCounts(out currentDirtIndices, out currentOreIndices);

        // 最初のフレームは「現在の数」を記録するだけで、お金は増やさない
        if (!isInitialized)
        {
            if (currentDirtIndices > 0 || currentOreIndices > 0)
            {
                lastDirtIndices = currentDirtIndices;
                lastOreIndices = currentOreIndices;
                isInitialized = true;
            }
            return;
        }

        // --- 土の判定 ---
        if (currentDirtIndices < lastDirtIndices)
        {
            int diff = lastDirtIndices - currentDirtIndices;
            if (diff > 0) moneyManager.MoneyOnHandIncrease(dirtValue);
        }

        // --- 鉱石の判定 ---
        if (currentOreIndices < lastOreIndices)
        {
            int diff = lastOreIndices - currentOreIndices;
            if (diff > 0) moneyManager.MoneyOnHandIncrease(oreValue);
        }

        lastDirtIndices = currentDirtIndices;
        lastOreIndices = currentOreIndices;
    }

    private void UpdateCounts(out int dirtTotal, out int oreTotal)
    {
        dirtTotal = 0;
        oreTotal = 0;

        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>();
        foreach (var mf in filters)
        {
            if (mf.sharedMesh != null && mf.sharedMesh.subMeshCount >= 2)
            {
                dirtTotal += (int)mf.sharedMesh.GetIndexCount(0);
                oreTotal += (int)mf.sharedMesh.GetIndexCount(1);
            }
        }
    }
}
