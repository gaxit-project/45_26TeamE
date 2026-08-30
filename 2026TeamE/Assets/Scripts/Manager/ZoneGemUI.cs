using UnityEngine;
using TMPro;

public class ZoneGemUI : MonoBehaviour
{
    private TextMeshProUGUI zoneText;
    private Transform player;

    
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
        
        
        LastMaxZoneValue = maxZoneValue;

        
        zoneText.text = $"{maxZoneValue:N0}";
    }
}
