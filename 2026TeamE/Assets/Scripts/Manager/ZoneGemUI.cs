using UnityEngine;
using TMPro;

public class ZoneGemUI : MonoBehaviour
{
    private TextMeshProUGUI zoneText;
    private Transform player;

    // リザルト画面に引き継ぐための静的変数
    public static long LastMaxZoneValue { get; private set; }

    void Start()
    {
        zoneText = GetComponent<TextMeshProUGUI>();
        
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) 
        {
            player = p.transform;
        }
    }

    void Update()
    {
        if (player == null || VoxelTerrain.Instance == null || zoneText == null) return;

        Vector3 localPos = VoxelTerrain.Instance.transform.InverseTransformPoint(player.position);
        int py = Mathf.FloorToInt(localPos.y / VoxelTerrain.Instance.BlockSize);

        long maxZoneValue = VoxelTerrain.Instance.GetZoneInitialGemValue(py);
        
        // 常に最新の値を保存しておく（リザルト画面用）
        LastMaxZoneValue = maxZoneValue;

        // 画面に表示する（カンマ表記の数字のみ）
        zoneText.text = $"{maxZoneValue:N0}";
    }
}
