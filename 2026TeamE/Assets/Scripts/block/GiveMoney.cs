using UnityEngine;

public class GiveMoney : MonoBehaviour
{
    public MoneyManager moneyManager; // ※ここはインスペクターで空でも自動で探します

    [Header("種類別の金額")]
    [SerializeField] private int dirtValue = 10;
    [SerializeField] private int oreValue = 50;

    private int lastDirtIndices = -1;
    private int lastOreIndices = -1;
    private bool isInitialized = false;

    void Start()
    {
        // ★シーンを読み直した際、自動で MoneyManager.Instance をセットする
        if (moneyManager == null)
        {
            moneyManager = MoneyManager.Instance;
        }
    }

    void Update()
    {
        // マネージャーが見つからなければ何もしない
        if (moneyManager == null)
        {
            moneyManager = MoneyManager.Instance; // 念のためここでもチェック
            if (moneyManager == null) return;
        }

        int currentDirtIndices;
        int currentOreIndices;
        UpdateCounts(out currentDirtIndices, out currentOreIndices);

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

        if (currentDirtIndices < lastDirtIndices)
        {
            int diff = (lastDirtIndices - currentDirtIndices);
            // 減少を検知したときだけ加算
            if (diff > 0) moneyManager.MoneyOnHandIncrease(dirtValue);
        }

        if (currentOreIndices < lastOreIndices)
        {
            int diff = (lastOreIndices - currentOreIndices);
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
